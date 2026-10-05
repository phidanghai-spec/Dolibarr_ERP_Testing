using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang chi tiết Báo giá thương mại (Commercial Proposal Detail).
/// URL: {BaseUrl}/comm/propal/card.php?id={id}
/// 
/// Hỗ trợ các nghiệp vụ:
/// - Đọc mã tham chiếu (Ref: PROV... hoặc PR...)
/// - Đọc trạng thái (Status badge: Draft, Open, Signed, Billed...)
/// - Đọc tổng tiền: Total HT (excl. tax), Total VAT, Total TTC (inc. tax)
/// - Thêm dòng sản phẩm (Predefined product) với mã sản phẩm và số lượng
/// - Xác thực báo giá (Validate -> Trạng thái Open)
/// - Đóng báo giá trạng thái Đã ký (Close as Signed)
/// - Nhấn tạo hóa đơn bán hàng (Create Invoice)
/// </summary>
public class ProposalDetailPage
{
    private readonly IWebDriver _driver;

    // ── Locators ────────────────────────────────────────────────────────────────
    private static readonly By RefTitle = By.CssSelector("div.titre, div.inline-block.valignmiddle.refid, .refidno");
    private static readonly By StatusBadge = By.CssSelector("span.badge-status, .statusref, div.statusref");
    private static readonly By ProductSelect = By.CssSelector("select#idprod, select[name='idprod']");
    private static readonly By QtyInput = By.CssSelector("input#qty, input[name='qty']");
    private static readonly By AddLineButton = By.CssSelector("input[type='submit'][name='addline'], input[type='submit'].button-add");
    private static readonly By ValidateButton = By.CssSelector("a.butAction[href*='action=valid'], a.butAction:not(.butActionRefused)");
    private static readonly By CloseButton = By.CssSelector("a.butAction[href*='action=close'], a.butAction[href*='action=statut']");
    private static readonly By CreateInvoiceButton = By.CssSelector("a.butAction[href*='action=create'][href*='origin=propal'], a.butAction[href*='compta/facture/card.php']");
    private static readonly By ConfirmButton = By.CssSelector("input.confirmvalidatebutton, input.button-confirm, button.ui-button:first-of-type, input[value='Yes'], input[name='confirm']");

    public ProposalDetailPage(IWebDriver driver) => _driver = driver;

    public void GoTo(string url)
    {
        _driver.Navigate().GoToUrl(url);
        WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 15);
    }

    /// <summary>Đọc mã tham chiếu của báo giá (PROV... hoặc PR...)</summary>
    public string GetReference()
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                var elem = _driver.FindElement(RefTitle);
                var match = Regex.Match(elem.Text, @"(PR\d{4}-\d{4,5}|PROV\d+)", RegexOptions.IgnoreCase);
                if (match.Success) return match.Value;
            }
            catch (Exception ex) when (ex is NoSuchElementException || ex is StaleElementReferenceException)
            {
                System.Threading.SpinWait.SpinUntil(() => false, 500);
            }
        }

        // Fallback đọc từ page title hoặc PageSource
        var pageSource = _driver.PageSource;
        var m = Regex.Match(pageSource, @"(PR\d{4}-\d{4,5}|PROV\d+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Value : string.Empty;
    }

    /// <summary>Đọc trạng thái hiện tại (Draft, Open, Signed, etc.)</summary>
    public string GetStatusText()
    {
        try
        {
            var elem = _driver.FindElement(StatusBadge);
            return elem.Text.Trim();
        }
        catch (NoSuchElementException)
        {
            // Fallback tìm phần tử chứa Draft / Open / Signed
            var badges = _driver.FindElements(By.CssSelector(".badge, .statusref, span[class*='status']"));
            foreach (var b in badges)
            {
                if (b.Displayed && !string.IsNullOrWhiteSpace(b.Text))
                    return b.Text.Trim();
            }
            return string.Empty;
        }
    }

    /// <summary>Đọc số tiền Amount (excl. tax) - Total HT</summary>
    public decimal GetAmountExclTax() => ExtractAmountByLabel("Amount (excl. tax)");

    /// <summary>Đọc số tiền Amount tax - Total VAT</summary>
    public decimal GetAmountTax() => ExtractAmountByLabel("Amount tax");

    /// <summary>Đọc số tiền Amount (inc. tax) - Total TTC</summary>
    public decimal GetAmountIncTax() => ExtractAmountByLabel("Amount (inc. tax)");

    private decimal ExtractAmountByLabel(string labelText)
    {
        try
        {
            var row = _driver.FindElement(By.XPath($"//tr[contains(., '{labelText}')]"));
            var cells = row.FindElements(By.TagName("td"));
            string text = cells.Count > 0 ? cells[^1].Text : row.Text;

            // Xóa ký tự tiền tệ €, dấu phẩy/chấm
            // Format Dolibarr: "150.00 €" hoặc "150,00 €"
            var match = Regex.Match(text, @"([\d\s,.]+)\s*€?");
            if (match.Success)
            {
                string clean = match.Groups[1].Value.Replace(" ", "").Replace("€", "").Trim();
                if (clean.Contains(',') && !clean.Contains('.'))
                {
                    clean = clean.Replace(',', '.');
                }
                else if (clean.Contains(',') && clean.Contains('.'))
                {
                    clean = clean.Replace(".", "").Replace(',', '.'); // dạng 1.234,56
                }

                if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
                    return val;
            }
        }
        catch (NoSuchElementException) { }
        return -1m;
    }

    /// <summary>
    /// Thêm dòng sản phẩm định sẵn (Predefined Product) vào báo giá.
    /// </summary>
    public void AddPredefinedProduct(string productCode, int quantity)
    {
        var js = (IJavaScriptExecutor)_driver;

        // 1. Đảm bảo chọn radio Predefined Product (nếu có)
        try
        {
            var radios = _driver.FindElements(By.XPath("//input[@type='radio'][contains(@id, 'predef') or @value='predef'] | //label[contains(., 'Predefined')]"));
            if (radios.Count > 0 && radios[0].Displayed)
            {
                radios[0].Click();
            }
        }
        catch { }

        // 2. Chọn sản phẩm trong select2/dropdown idprod
        var prodSelect = _driver.FindElements(ProductSelect);

        if (prodSelect.Count > 0)
        {
            // Dùng JS chọn option chứa productCode và trigger change
            bool selected = Convert.ToBoolean(js.ExecuteScript(@"
                var select = arguments[0];
                var code = arguments[1].trim().toLowerCase();
                for (var i = 0; i < select.options.length; i++) {
                    var optText = select.options[i].text.trim().toLowerCase();
                    if (optText.indexOf(code) !== -1) {
                        select.selectedIndex = i;
                        $(select).val(select.options[i].value).trigger('change');
                        return true;
                    }
                }
                return false;
            ", prodSelect[0], productCode));

            if (!selected)
            {
                // Fallback click vào select2-selection
                var container = _driver.FindElement(By.CssSelector("span.select2-selection[aria-labelledby*='select2-idprod']"));
                container.Click();
                var searchInput = WaitHelper.WaitVisible(_driver, By.CssSelector("input.select2-search__field"), timeoutSeconds: 5);
                searchInput.SendKeys(productCode);
                var resultOption = WaitHelper.WaitClickable(_driver, By.CssSelector("li.select2-results__option--highlighted"), timeoutSeconds: 5);
                resultOption.Click();
            }
        }

        // Chờ Dolibarr Ajax tải thông tin sản phẩm
        System.Threading.SpinWait.SpinUntil(() => false, 1500);

        // 3. Nhập số lượng Qty
        var qtyElem = WaitHelper.WaitPresent(_driver, QtyInput, timeoutSeconds: 5);
        qtyElem.Clear();
        qtyElem.SendKeys(quantity.ToString());

        // 4. Nhấn Add
        var addBtn = WaitHelper.WaitClickable(_driver, AddLineButton, timeoutSeconds: 5);
        addBtn.Click();

        // 5. Chờ dòng sản phẩm được ghi nhận trên bảng (chứa mã productCode)
        try
        {
            WaitHelper.WaitPresent(_driver, By.XPath($"//table[contains(@class, 'noborder')]//tr[contains(., '{productCode}')]"), timeoutSeconds: 10);
        }
        catch (WebDriverTimeoutException)
        {
            // Nếu bảng chưa xong thì chờ bảng noborder chung
            WaitHelper.WaitPresent(_driver, By.CssSelector("table.noborder"), timeoutSeconds: 5);
        }
    }

    /// <summary>
    /// Xác thực báo giá (Validate Proposal) -> Trạng thái Draft sang Open.
    /// </summary>
    public void ValidateProposal()
    {
        // Tìm nút Validate
        var validateBtn = WaitHelper.WaitClickable(_driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'Validate') or contains(@href, 'action=valid'))]"), timeoutSeconds: 10);
        validateBtn.Click();

        // Xác nhận popup/dialog
        ConfirmDialog();

        // Chờ trạng thái cập nhật (không còn Draft)
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d => !GetStatusText().Contains("Draft", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Đóng báo giá với trạng thái Chấp thuận/Đã ký (Signed).
    /// </summary>
    public void CloseAsSigned()
    {
        // Nhấn nút Close proposal
        var closeBtn = WaitHelper.WaitClickable(_driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'Close') or contains(@href, 'action=close') or contains(@href, 'action=statut'))]"), timeoutSeconds: 10);
        closeBtn.Click();

        // Trong form đóng báo giá: chọn radio hoặc select Signed (Chấp thuận)
        var js = (IJavaScriptExecutor)_driver;
        js.ExecuteScript(@"
            // Chọn Signed (thường value = 2 hoặc option Signed)
            var select = document.querySelector('select[name=""statut""]');
            if (select) {
                for (var i = 0; i < select.options.length; i++) {
                    if (select.options[i].text.toLowerCase().indexOf('signed') !== -1 || select.options[i].value == '2') {
                        select.selectedIndex = i;
                        $(select).trigger('change');
                        break;
                    }
                }
            }
        ");

        // Nhấn nút Confirm
        ConfirmDialog();

        // Chờ trạng thái cập nhật Signed
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d => GetStatusText().Contains("Signed", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Nhấn nút Create Invoice từ trang Proposal đã xác thực.
    /// </summary>
    public void ClickCreateInvoice()
    {
        var createInvBtn = WaitHelper.WaitClickable(_driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'Create invoice') or contains(@href, 'origin=propal'))]"), timeoutSeconds: 10);
        createInvBtn.Click();

        // Đợi chuyển hướng sang trang tạo hóa đơn (compta/facture/card.php?action=create)
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d => d.Url.Contains("compta/facture/card.php"));
    }

    private void ConfirmDialog()
    {
        try
        {
            var btn = WaitHelper.WaitClickable(_driver, ConfirmButton, timeoutSeconds: 5);
            btn.Click();
        }
        catch (WebDriverTimeoutException)
        {
            // Thử trigger nút Yes bằng JS
            var js = (IJavaScriptExecutor)_driver;
            js.ExecuteScript(@"
                var btns = document.querySelectorAll('input[type=""submit""], button');
                for (var i = 0; i < btns.length; i++) {
                    var val = (btns[i].value || btns[i].innerText || '').toLowerCase().trim();
                    if (val === 'yes' || val === 'oui' || btns[i].classList.contains('confirmvalidatebutton')) {
                        btns[i].click();
                        break;
                    }
                }
            ");
        }
    }
}

using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang Hóa đơn bán hàng (Customer Invoice Detail).
/// URL: {BaseUrl}/compta/facture/card.php?id={id}
/// 
/// Hỗ trợ các nghiệp vụ:
/// - Đọc mã tham chiếu hóa đơn (PROV... hoặc IN...)
/// - Đọc trạng thái (Draft, Unpaid, Paid, Cancelled)
/// - Đọc tổng tiền Total HT, VAT, Total TTC
/// - Xác thực hóa đơn (Validate Invoice -> Trạng thái Unpaid)
/// </summary>
public class InvoiceDetailPage
{
    private readonly IWebDriver _driver;

    // ── Locators ────────────────────────────────────────────────────────────────
    private static readonly By RefTitle = By.CssSelector("div.titre, div.inline-block.valignmiddle.refid, .refidno");
    private static readonly By StatusBadge = By.CssSelector("span.badge-status, .statusref, div.statusref");
    private static readonly By CreateDraftSubmit = By.CssSelector("input[type='submit'].button-save, input[type='submit'][value*='draft']");
    private static readonly By ValidateButton = By.CssSelector("a.butAction[href*='action=valid'], a.butAction:not(.butActionRefused)");
    private static readonly By ConfirmButton = By.XPath("//div[contains(@class, 'ui-dialog-buttonset')]//button[contains(., 'Yes') or contains(., 'Oui')] | //div[contains(@class, 'ui-dialog-buttonset')]//button[1] | //input[@value='Yes' or @value='Oui'] | //input[contains(@class, 'confirmvalidatebutton')]");

    public InvoiceDetailPage(IWebDriver driver) => _driver = driver;

    public void GoTo(string url)
    {
        _driver.Navigate().GoToUrl(url);
        WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 15);
    }

    /// <summary>
    /// Thao tác tạo hóa đơn nháp từ màn hình create hóa đơn (sau khi nhấn Create invoice từ Proposal).
    /// </summary>
    public void SubmitCreateInvoiceDraft()
    {
        // Click nút Now của Invoice date
        try
        {
            var nowBtn = WaitHelper.WaitClickable(_driver, By.XPath("//tr[contains(., 'Invoice date')]//a[contains(., 'Now')] | //tr[contains(., 'Invoice date')]//button[contains(., 'Now')]"), timeoutSeconds: 5);
            nowBtn.Click();
        }
        catch (WebDriverTimeoutException)
        {
            var js = (IJavaScriptExecutor)_driver;
            js.ExecuteScript(@"
                var links = document.querySelectorAll('a, button');
                for (var i = 0; i < links.length; i++) {
                    if (links[i].innerText && links[i].innerText.trim() === 'Now') {
                        links[i].click();
                        break;
                    }
                }
            ");
        }

        var submitBtn = WaitHelper.WaitClickable(_driver, CreateDraftSubmit, timeoutSeconds: 10);
        submitBtn.Click();

        // Chờ chuyển về trang chi tiết hóa đơn (compta/facture/card.php?facid=\d+ hoặc id=\d+)
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        wait.Until(d => d.Url.Contains("compta/facture/card.php") && !d.Url.Contains("action=create"));
    }

    /// <summary>Đọc mã tham chiếu hóa đơn (PROV... hoặc IN...)</summary>
    public string GetReference()
    {
        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            return wait.Until(d =>
            {
                var elem = d.FindElement(RefTitle);
                var match = Regex.Match(elem.Text, @"(IN\d{4}-\d{4,5}|IC\d{4}-\d{4,5}|AV\d{4}-\d{4,5}|PROV\d+)", RegexOptions.IgnoreCase);
                return match.Success ? match.Value : null;
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            var pageSource = _driver.PageSource;
            var m = Regex.Match(pageSource, @"(IN\d{4}-\d{4,5}|IC\d{4}-\d{4,5}|AV\d{4}-\d{4,5}|PROV\d+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Value : string.Empty;
        }
    }

    /// <summary>Đọc trạng thái của hóa đơn (Draft, Unpaid, Paid...)</summary>
    public string GetStatusText()
    {
        try
        {
            var elem = _driver.FindElement(StatusBadge);
            return elem.Text.Trim();
        }
        catch (NoSuchElementException)
        {
            var badges = _driver.FindElements(By.CssSelector(".badge, .statusref, span[class*='status']"));
            foreach (var b in badges)
            {
                if (b.Displayed && !string.IsNullOrWhiteSpace(b.Text))
                    return b.Text.Trim();
            }
            return string.Empty;
        }
    }

    /// <summary>Đọc số tiền Amount (inc. tax) - Total TTC của hóa đơn</summary>
    public decimal GetAmountIncTax()
    {
        try
        {
            var row = _driver.FindElement(By.XPath("//tr[contains(., 'Amount (inc. tax)') or contains(., 'Total (inc. tax)') or contains(., 'Total TTC')]"));
            var cells = row.FindElements(By.TagName("td"));
            string text = cells.Count > 0 ? cells[^1].Text : row.Text;

            var match = Regex.Match(text, @"([\d\s,.]+)\s*€?");
            if (match.Success)
            {
                string clean = match.Groups[1].Value.Replace(" ", "").Replace("€", "").Trim();
                if (clean.Contains(',') && !clean.Contains('.'))
                    clean = clean.Replace(',', '.');
                else if (clean.Contains(',') && clean.Contains('.'))
                    clean = clean.Replace(".", "").Replace(',', '.');

                if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
                    return val;
            }
        }
        catch (NoSuchElementException) { }
        return -1m;
    }

    /// <summary>
    /// Xác thực hóa đơn (Validate Invoice) -> chuyển trạng thái từ Draft sang Unpaid.
    /// Kích hoạt cơ chế tự động trừ kho nếu rule kho đã được bật.
    /// </summary>
    public void ValidateInvoice()
    {
        var validateBtn = WaitHelper.WaitClickable(_driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'Validate') or contains(@href, 'action=valid'))]"), timeoutSeconds: 10);
        validateBtn.Click();

        ConfirmDialog();

        // Chờ trạng thái cập nhật (không còn Draft)
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(d => !GetStatusText().Contains("Draft", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ghi nhận thanh toán cho hóa đơn (Enter payment). Mặc định payAmount = 0 sẽ tự động thanh toán 100% (AutoFill).
    /// </summary>
    public void EnterPayment(decimal payAmount = 0)
    {
        var matchFacId = Regex.Match(_driver.Url, @"facid=(\d+)");
        string facId = matchFacId.Success ? matchFacId.Groups[1].Value : string.Empty;

        var payBtn = WaitHelper.WaitClickable(_driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'ENTER PAYMENT') or contains(text(), 'Enter payment') or contains(@href, 'compta/paiement.php'))]"), 10);
        payBtn.Click();

        WaitHelper.WaitVisible(_driver, By.CssSelector("form#payment_form, form[action*='paiement.php'], div.fiche"), 15);

        var js = (IJavaScriptExecutor)_driver;
        js.ExecuteScript(@"
            // Chọn ngày thanh toán hiện tại
            var nowBtn = document.querySelector('#reButtonNow, button.datenowlink');
            if (nowBtn) {
                nowBtn.click();
            } else {
                var dInput = document.querySelector('#re, input[name=""re""]');
                if (dInput) {
                    var now = new Date();
                    var dd = String(now.getDate()).padStart(2, '0');
                    var mm = String(now.getMonth() + 1).padStart(2, '0');
                    var yyyy = now.getFullYear();
                    dInput.value = mm + '/' + dd + '/' + yyyy;
                    $(dInput).trigger('change');
                }
            }

            // Chọn Payment mode: Cash (LIQ) hoặc bất kỳ phương thức nào có sẵn
            var pSelect = document.querySelector('#selectpaiementcode, select[name=""paiementcode""]');
            if (pSelect && pSelect.options.length > 1) {
                pSelect.value = 'LIQ';
                if (!pSelect.value) pSelect.selectedIndex = 1;
                $(pSelect).trigger('change');
            }
        ");

        js.ExecuteScript(@"
            var targetFacId = arguments[0];
            var amountToPay = arguments[1];

            if (targetFacId) {
                if (amountToPay <= 0) {
                    var autoBtn = document.querySelector('button[data-rowname=""amount_' + targetFacId + '""]');
                    if (autoBtn) autoBtn.click();
                } else {
                    var inp = document.querySelector('input[name=""amount_' + targetFacId + '""]');
                    if (inp) {
                        inp.value = amountToPay;
                        $(inp).trigger('change');
                    }
                }
            } else {
                var autoBtns = document.querySelectorAll('button.AutoFillAmount');
                if (amountToPay <= 0 && autoBtns.length > 0) {
                    autoBtns[autoBtns.length - 1].click();
                } else {
                    var inps = document.querySelectorAll('input.amount');
                    if (inps.length > 0) {
                        var targetInp = inps[inps.length - 1];
                        if (amountToPay > 0) targetInp.value = amountToPay;
                        $(targetInp).trigger('change');
                    }
                }
            }
        ", facId, payAmount > 0 ? payAmount.ToString(CultureInfo.InvariantCulture) : 0);

        var savePayBtn = WaitHelper.WaitClickable(_driver, By.CssSelector("form#payment_form input[type='submit'][value='Pay'], input[type='submit'].reposition"), 10);
        savePayBtn.Click();

        WaitHelper.WaitVisible(_driver, By.CssSelector("input.confirmvalidatebutton, input[type='submit'][value='Validate'], form[action*='paiement']"), 10);

        var validateBtn = WaitHelper.WaitClickable(_driver, By.CssSelector("input.confirmvalidatebutton, input[type='submit'][value='Validate']"), 5);
        validateBtn.Click();

        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(d =>
            !d.Url.Contains("confirm_paiement", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Đọc số tiền còn lại phải trả (Remains to pay). Trả về 0 nếu đã trả hết hoặc không còn nợ.
    /// </summary>
    public decimal GetRemainingAmount()
    {
        try
        {
            var row = _driver.FindElement(By.XPath("//tr[contains(., 'Remains to pay') or contains(., 'Remaining unpaid') or contains(., 'Reste à payer')]"));
            var cells = row.FindElements(By.TagName("td"));
            string text = cells.Count > 0 ? cells[^1].Text : row.Text;

            var match = Regex.Match(text, @"([\d\s,.]+)\s*€?");
            if (match.Success)
            {
                string clean = match.Groups[1].Value.Replace(" ", "").Replace("€", "").Trim();
                if (clean.Contains(',') && !clean.Contains('.'))
                    clean = clean.Replace(',', '.');
                else if (clean.Contains(',') && clean.Contains('.'))
                    clean = clean.Replace(".", "").Replace(',', '.');

                if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
                    return val;
            }
        }
        catch (NoSuchElementException) { }
        return 0m;
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

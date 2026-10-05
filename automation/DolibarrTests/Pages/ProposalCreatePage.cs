using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang tạo mới Báo giá thương mại (Commercial Proposal).
/// URL: {BaseUrl}/comm/propal/card.php?action=create
///
/// Locators xác nhận từ DOM thật của Dolibarr 22.0.4:
/// - Khách hàng: select id="socid" name="socid" (Select2)
/// - Điều khoản thanh toán: select id="cond_reglement_id" name="cond_reglement_id"
/// - Nút Tạo nháp: input.button-save (name="save", value="Create draft")
/// - Nút Hủy: input.button-cancel (name="cancel")
/// </summary>
public class ProposalCreatePage
{
    private readonly IWebDriver _driver;

    // ── Locators ────────────────────────────────────────────────────────────────
    private static readonly By CustomerSelect = By.Id("socid");
    private static readonly By CreateDraftButton = By.CssSelector("input[type='submit'].button-save");
    private static readonly By CancelButton = By.CssSelector("input.button-cancel");
    private static readonly By ErrorMessage = By.CssSelector("div.jnotify-message, div.error");

    public ProposalCreatePage(IWebDriver driver) => _driver = driver;

    // ── Điều hướng ──────────────────────────────────────────────────────────────
    public void GoTo()
    {
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/comm/propal/card.php?action=create");
        WaitHelper.WaitVisible(_driver, CreateDraftButton, timeoutSeconds: 15);
    }

    public bool IsOnCreatePage()
    {
        return _driver.Url.Contains("comm/propal/card.php") &&
               _driver.Url.Contains("action=create");
    }

    // ── Thao tác ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Chọn khách hàng theo tên trong dropdown Select2 của Dolibarr.
    /// Sử dụng JavaScript trigger change để tương thích hoàn toàn với Select2.
    /// </summary>
    public void SelectCustomerByName(string customerName)
    {
        var selectElem = WaitHelper.WaitPresent(_driver, CustomerSelect, timeoutSeconds: 10);
        var js = (IJavaScriptExecutor)_driver;

        // Tìm option có chứa tên khách hàng và trigger change
        bool selected = Convert.ToBoolean(js.ExecuteScript(@"
            var select = arguments[0];
            var name = arguments[1].trim().toLowerCase();
            for (var i = 0; i < select.options.length; i++) {
                var optText = select.options[i].text.trim().toLowerCase();
                if (optText.indexOf(name) !== -1) {
                    select.selectedIndex = i;
                    $(select).val(select.options[i].value).trigger('change');
                    return true;
                }
            }
            return false;
        ", selectElem, customerName));

        if (!selected)
        {
            // Fallback: click vào select2-selection và gõ tìm kiếm
            var container = _driver.FindElement(By.CssSelector("span.select2-selection[aria-labelledby*='select2-socid']"));
            container.Click();
            var searchInput = WaitHelper.WaitVisible(_driver, By.CssSelector("input.select2-search__field"), timeoutSeconds: 5);
            searchInput.SendKeys(customerName);
            var resultOption = WaitHelper.WaitClickable(_driver, By.CssSelector("li.select2-results__option--highlighted"), timeoutSeconds: 5);
            resultOption.Click();
        }
    }

    /// <summary>
    /// Click vào nút 'Now' của trường 'Date of proposal' để chọn ngày hiện tại.
    /// Selector nhắm chính xác vào thẻ tr chứa nhãn 'Date of proposal'.
    /// </summary>
    public void SetProposalDateNow()
    {
        try
        {
            var nowBtn = WaitHelper.WaitClickable(_driver, By.XPath("//tr[contains(., 'Date of proposal')]//a[contains(text(), 'Now') or contains(., 'Now')] | //tr[contains(., 'Date of proposal')]//button[contains(., 'Now')]"), timeoutSeconds: 5);
            nowBtn.Click();
        }
        catch (WebDriverTimeoutException)
        {
            // Fallback: tìm bất kỳ link/button Now nào trong form
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
    }

    /// <summary>Nhấn nút Create draft để tạo báo giá nháp.</summary>
    public void ClickCreateDraft()
    {
        var btn = WaitHelper.WaitClickable(_driver, CreateDraftButton, timeoutSeconds: 10);
        btn.Click();
    }

    /// <summary>Lấy thông báo lỗi nếu có.</summary>
    public string GetErrorMessage()
    {
        try
        {
            var el = _driver.FindElement(ErrorMessage);
            return el.Displayed ? el.Text.Trim() : string.Empty;
        }
        catch (NoSuchElementException)
        {
            return string.Empty;
        }
    }
}

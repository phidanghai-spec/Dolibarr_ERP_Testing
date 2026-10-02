using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang chi tiet / xem khach hang (Third party) sau khi tao.
/// URL thanh cong: {BaseUrl}/societe/card.php?socid={id} hoac ?id={id}
/// </summary>
public class CustomerDetailPage
{
    private readonly IWebDriver _driver;

    // -- Locators chinh --
    private static readonly By SuccessMessage = By.CssSelector("div.ok");
    private static readonly By CustomerNameDisplay = By.CssSelector("div.refid");

    // -- Locators: Delete flow --
    // Dolibarr 22.0.4:
    // Nut id="action-delete" mo jQuery UI Dialog id="dialog-confirm-action-delete"
    // Dialog chua cac nut "Yes" va "No" trong .ui-dialog-buttonset
    private static readonly By DeleteButton = By.Id("action-delete");
    private static readonly By DeleteButtonNoAjax = By.Id("action-delete-no-ajax");
    private static readonly By DeleteConfirmDialog = By.Id("dialog-confirm-action-delete");
    private static readonly By DialogYesButton = By.XPath("//div[contains(@class,'ui-dialog') and not(contains(@style,'display: none'))]//button[normalize-space()='Yes' or contains(., 'Yes')]");
    private static readonly By ConfirmSelect = By.Id("confirm");
    private static readonly By ConfirmValidateButton = By.CssSelector("input.confirmvalidatebutton");

    // -- Locators: Edit flow --
    // Dolibarr 22: nut "Sua" (Modifier) id="action-edit" class="butAction"
    private static readonly By EditButton = By.CssSelector("a.butAction[href*='action=edit']");
    private static readonly By NameEditInput = By.Id("name");
    private static readonly By SaveEditButton = By.CssSelector("input[type='submit'].button-save");

    public CustomerDetailPage(IWebDriver driver) => _driver = driver;

    // -- Kiem tra trang thai --

    public static bool IsCustomerCreatedSuccessfully(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Regex.IsMatch(url, @"societe/card\.php\?.*(?<!\w)(socid|id)=\d+", RegexOptions.IgnoreCase);
    }

    public bool WaitForRedirectAfterSave(int? timeoutSeconds = null)
    {
        int timeout = timeoutSeconds ?? TestConfig.TimeoutSeconds;
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeout));
        try { return wait.Until(d => IsCustomerCreatedSuccessfully(d.Url)); }
        catch (WebDriverTimeoutException) { return false; }
    }

    public string GetSuccessMessage(int? timeoutSeconds = null)
    {
        try { return WaitHelper.WaitVisible(_driver, SuccessMessage, timeoutSeconds).Text.Trim(); }
        catch (WebDriverTimeoutException) { return string.Empty; }
    }

    /// <summary>
    /// Doc chinh xac ten KH tren trang chi tiet (loai bo phan tu con chua dia chi/quoc gia).
    /// </summary>
    public string GetDisplayedName()
    {
        IWebElement? targetElement = null;

        try { targetElement = WaitHelper.WaitVisible(_driver, CustomerNameDisplay, timeoutSeconds: 5); }
        catch (WebDriverTimeoutException) { }

        if (targetElement == null)
        {
            try { targetElement = _driver.FindElement(By.CssSelector("#id-right h1")); }
            catch (NoSuchElementException) { }
        }

        if (targetElement == null) return string.Empty;

        var js = (IJavaScriptExecutor)_driver;
        string script = @"
            var el = arguments[0];
            var clone = el.cloneNode(true);
            var toRemove = clone.querySelectorAll('.refidno, .refaddress, a, .opacitymedium, .statusref, .badge');
            for (var i = 0; i < toRemove.length; i++) { toRemove[i].remove(); }
            return clone.textContent.trim();
        ";

        string extractedName = (js.ExecuteScript(script, targetElement) as string)?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(extractedName))
        {
            string fallbackScript = @"
                var el = arguments[0]; var txt = '';
                for (var i = 0; i < el.childNodes.length; i++) {
                    if (el.childNodes[i].nodeType === 3) { txt += el.childNodes[i].nodeValue; }
                }
                return txt.trim();
            ";
            extractedName = (js.ExecuteScript(fallbackScript, targetElement) as string)?.Trim() ?? string.Empty;
        }

        if (extractedName.Contains('\n') || extractedName.Contains("Vietnam", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Locator lay ten van con dinh du lieu khac, can kiem tra lai HTML. Gia tri lay duoc: '{extractedName}'");

        return extractedName;
    }

    public string GetCustomerNameContainerHtml()
    {
        try { return WaitHelper.WaitVisible(_driver, CustomerNameDisplay, timeoutSeconds: 5).GetAttribute("innerHTML") ?? string.Empty; }
        catch (WebDriverTimeoutException) { }
        try { return _driver.FindElement(By.CssSelector("#id-right h1")).GetAttribute("innerHTML") ?? string.Empty; }
        catch (NoSuchElementException) { }
        return string.Empty;
    }

    public string GetCurrentUrl() => _driver.Url;

    /// <summary>Trich xuat socid tu URL hien tai. Tra ve chuoi so hoac rong neu khong tim thay.</summary>
    public string GetSocId()
    {
        var match = Regex.Match(_driver.Url, @"(?:socid|id)=(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    // -- Delete Customer --

    /// <summary>
    /// Xoa KH hien tai tren trang chi tiet.
    /// Luong Dolibarr 22 (JS bat): Click action-delete -> dialog -> chon yes -> click confirmvalidatebutton
    /// Fallback (JS tat): Click action-delete-no-ajax -> form inline -> chon yes -> submit
    /// Tra ve true neu xoa thanh cong.
    /// </summary>
    public bool DeleteCustomer(int timeoutSeconds = 15)
    {
        string originalUrl = _driver.Url;
        try
        {
            var deleteBtn = WaitHelper.WaitClickable(_driver, DeleteButton, timeoutSeconds: 10);
            deleteBtn.Click();

            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            IWebElement? dialogEl = null;
            try
            {
                dialogEl = wait.Until(d =>
                {
                    try
                    {
                        var el = d.FindElement(DeleteConfirmDialog);
                        return el.Displayed ? el : null;
                    }
                    catch (NoSuchElementException) { return null; }
                    catch (StaleElementReferenceException) { return null; }
                });
            }
            catch (WebDriverTimeoutException) { }

            if (dialogEl != null && dialogEl.Displayed)
                return ConfirmDeleteViaDialog(originalUrl, timeoutSeconds);
        }
        catch (WebDriverTimeoutException) { }
        catch (NoSuchElementException) { }

        return DeleteCustomerNoAjax(originalUrl, timeoutSeconds);
    }

    private bool ConfirmDeleteViaDialog(string originalUrl, int timeoutSeconds)
    {
        try
        {
            var yesBtn = WaitHelper.WaitClickable(_driver, DialogYesButton, timeoutSeconds: 5);
            yesBtn.Click();
        }
        catch (Exception)
        {
            // Fallback: Click nut Yes qua jQuery
            try
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript(
                    "var btn = $('.ui-dialog:not([style*=\"display: none\"]) .ui-dialog-buttonset button').first(); if(btn.length) btn.click();");
            }
            catch { }
        }

        return WaitForDeleteRedirect(originalUrl, timeoutSeconds);
    }

    private bool DeleteCustomerNoAjax(string originalUrl, int timeoutSeconds)
    {
        try
        {
            WaitHelper.WaitClickable(_driver, DeleteButtonNoAjax, timeoutSeconds: 8).Click();
            try { new SelectElement(WaitHelper.WaitVisible(_driver, ConfirmSelect, timeoutSeconds: 5)).SelectByValue("yes"); }
            catch { }
            try { WaitHelper.WaitClickable(_driver, ConfirmValidateButton, timeoutSeconds: 5).Click(); }
            catch { }
            return WaitForDeleteRedirect(originalUrl, timeoutSeconds);
        }
        catch (Exception) { return false; }
    }

    private bool WaitForDeleteRedirect(string originalUrl, int timeoutSeconds)
    {
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return wait.Until(d =>
            {
                string url = d.Url;
                bool onList = url.Contains("societe/list.php", StringComparison.OrdinalIgnoreCase);
                bool navigatedAway = !url.Equals(originalUrl, StringComparison.OrdinalIgnoreCase)
                                   && !url.Contains("action=delete", StringComparison.OrdinalIgnoreCase);
                return onList || navigatedAway;
            });
        }
        catch (WebDriverTimeoutException) { return false; }
    }

    // -- Edit Customer --

    /// <summary>
    /// Click nut "Sua" (Modifier/Edit) tren trang chi tiet.
    /// Dolibarr 22: nut id="action-edit" class="butAction".
    /// </summary>
    public void ClickEdit()
    {
        WaitHelper.WaitClickable(_driver, EditButton, timeoutSeconds: 10).Click();
        WaitHelper.WaitVisible(_driver, NameEditInput, timeoutSeconds: 10);
    }

    public void EnterName(string newName)
    {
        var el = WaitHelper.WaitVisible(_driver, NameEditInput, timeoutSeconds: 5);
        el.Clear();
        el.SendKeys(newName);
    }

    public void ClickSave()
    {
        WaitHelper.WaitClickable(_driver, SaveEditButton).Click();
    }

    public string GetNameInputValue()
    {
        return WaitHelper.WaitVisible(_driver, NameEditInput, timeoutSeconds: 5).GetAttribute("value") ?? string.Empty;
    }
}


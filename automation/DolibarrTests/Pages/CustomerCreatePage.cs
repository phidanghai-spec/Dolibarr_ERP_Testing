using DolibarrTests.Helpers;
using OpenQA.Selenium;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang tạo mới khách hàng (Third party).
/// URL: {BaseUrl}/societe/card.php?action=create
///
/// Locator xác nhận từ mã nguồn Dolibarr 22.0.4 (htdocs/societe/card.php):
/// - Tên:      input id="name" name="name" maxlength="128"
/// - KH type:  input id="customerinput" class="checkforselect" name="customer" value="1" (checkbox)
/// - Nút Lưu:  input.button-save (class cố định, html.form.class.php sinh name='save')
/// - Lỗi:      div.jnotify-message
/// </summary>
public class CustomerCreatePage
{
    private readonly IWebDriver _driver;

    // ── Locators (tất cả xác nhận từ source, không đoán) ───────────────────────

    /// <summary>Ô nhập tên — maxlength="128" theo card.php dòng ~1460.</summary>
    private static readonly By NameInput = By.Id("name");

    /// <summary>Ô nhập SĐT (phone) — name="phone" hoặc id="phone".</summary>
    private static readonly By PhoneInput = By.CssSelector("input[name='phone'], input#phone");

    /// <summary>
    /// Checkbox "Khách hàng".
    /// card.php dòng 1486: input id="customerinput" name="customer" value="1" type="checkbox"
    /// </summary>
    private static readonly By CustomerCheckbox = By.Id("customerinput");

    /// <summary>
    /// Nút Lưu — html.form.class.php: buttonsSaveCancel('AddThirdParty',...) → name='save'
    /// HTML: &lt;input type="submit" class="button button-save" name="save" value="..."&gt;
    /// Dùng class button-save để độc lập ngôn ngữ.
    /// </summary>
    private static readonly By SaveButton = By.CssSelector("input[type='submit'].button-save");

    /// <summary>Thông báo lỗi dạng jnotify (dùng chung toàn site).</summary>
    private static readonly By ErrorMessage = By.CssSelector("div.jnotify-message");

    // ── Constructor ─────────────────────────────────────────────────────────────
    public CustomerCreatePage(IWebDriver driver) => _driver = driver;

    // ── Điều hướng ──────────────────────────────────────────────────────────────

    /// <summary>Điều hướng đến trang tạo KH mới, chờ ô tên xuất hiện.</summary>
    public void GoTo()
    {
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/card.php?action=create");
        WaitHelper.WaitVisible(_driver, NameInput);
    }

    // ── Thao tác ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nhập tên KH vào ô "name". Clear trước.
    /// Dùng SendKeys thông thường (không JS) để browser áp maxlength đúng cách.
    /// </summary>
    public void EnterName(string name)
    {
        var el = WaitHelper.WaitVisible(_driver, NameInput);
        el.Clear();
        el.SendKeys(name);
    }

    /// <summary>
    /// Nhập SĐT vào ô phone. Clear trước.
    /// </summary>
    public void EnterPhone(string phone)
    {
        var el = WaitHelper.WaitVisible(_driver, PhoneInput);
        el.Clear();
        el.SendKeys(phone);
    }

    /// <summary>
    /// Check checkbox "Khách hàng" (id="customerinput") nếu chưa được chọn.
    /// </summary>
    public void SelectCustomerType()
    {
        var cb = WaitHelper.WaitVisible(_driver, CustomerCheckbox);
        if (!cb.Selected)
            cb.Click();
    }

    /// <summary>Nhấn nút Lưu (button-save).</summary>
    public void ClickSave()
    {
        WaitHelper.WaitClickable(_driver, SaveButton).Click();
    }

    // ── Đọc giá trị ────────────────────────────────────────────────────────────

    /// <summary>Đọc giá trị attribute "value" hiện tại của ô tên.</summary>
    public string GetNameInputValue()
    {
        var el = WaitHelper.WaitVisible(_driver, NameInput);
        return el.GetAttribute("value") ?? string.Empty;
    }

    /// <summary>Đọc maxlength attribute của ô tên (mong đợi "128").</summary>
    public string GetNameMaxLength()
    {
        var el = WaitHelper.WaitVisible(_driver, NameInput);
        return el.GetAttribute("maxlength") ?? string.Empty;
    }

    /// <summary>Kiểm tra xem ô Tên có thuộc tính HTML5 'required' hay không.</summary>
    public bool IsNameInputRequired()
    {
        var el = WaitHelper.WaitVisible(_driver, NameInput);
        var req = el.GetAttribute("required");
        return req != null && (req.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                               req.Equals("required", StringComparison.OrdinalIgnoreCase) ||
                               req == string.Empty);
    }

    /// <summary>Đọc validationMessage của ô Tên qua JavaScript.</summary>
    public string GetNameValidationMessage()
    {
        var el = WaitHelper.WaitVisible(_driver, NameInput);
        var js = (IJavaScriptExecutor)_driver;
        var msg = js.ExecuteScript("return arguments[0].validationMessage;", el);
        return msg?.ToString() ?? string.Empty;
    }

    // ── Kiểm tra trạng thái ─────────────────────────────────────────────────────

    /// <summary>Trả về true nếu ô tên đang visible (đang ở form create).</summary>
    public bool IsOnCreatePage()
    {
        try { return _driver.FindElement(NameInput).Displayed; }
        catch (NoSuchElementException) { return false; }
    }

    /// <summary>
    /// Trả về nội dung lỗi hiển thị (jnotify hoặc inline error), rỗng nếu không có.
    /// </summary>
    public string GetErrorMessage()
    {
        var selectors = new[]
        {
            By.CssSelector("div.jnotify-message"),
            By.CssSelector("div.error"),
            By.CssSelector("div.jnotify-container"),
            By.CssSelector(".fiche .error")
        };

        foreach (var sel in selectors)
        {
            try
            {
                var el = WaitHelper.WaitVisible(_driver, sel, timeoutSeconds: 3);
                var text = el.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            catch (WebDriverTimeoutException) { }
            catch (NoSuchElementException) { }
        }

        return string.Empty;
    }
}

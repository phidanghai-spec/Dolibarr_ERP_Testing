using DolibarrTests.Helpers;
using OpenQA.Selenium;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang đăng nhập Dolibarr.
/// URL: http://localhost/dolibarr/index.php
/// 
/// Locator xác nhận từ HTML thật (curl + browser probe 2026-09-21):
/// - username: input id="username" name="username"
/// - password: input id="password" name="password"
/// - submit:   input[type=submit] class="button" (không có id riêng)
/// - form:     form id="login"
/// - error:    div.jnotify-container > div.jnotify-item-error > div.jnotify-message
///             Xuất hiện sau khi submit sai mật khẩu, text = "Bad value for login or password"
/// </summary>
public class LoginPage
{
    private readonly IWebDriver _driver;

    // Locator — chỉ định nghĩa ở đây, test class không được dùng By trực tiếp
    private static readonly By UsernameInput = By.Id("username");
    private static readonly By PasswordInput = By.Id("password");
    private static readonly By SubmitButton = By.CssSelector("form#login input[type='submit']");
    private static readonly By LoginForm = By.Id("login");
    private static readonly By ErrorMessage = By.CssSelector("div.jnotify-message");

    public LoginPage(IWebDriver driver) => _driver = driver;

    /// <summary>Điều hướng đến trang đăng nhập.</summary>
    public void GoTo()
    {
        _driver.Navigate().GoToUrl(TestConfig.LoginUrl);
        WaitHelper.WaitVisible(_driver, LoginForm);
    }

    /// <summary>Đăng nhập với username và password cho trước.</summary>
    public void LoginAs(string username, string password)
    {
        var userField = WaitHelper.WaitVisible(_driver, UsernameInput);
        userField.Clear();
        userField.SendKeys(username);

        var passField = WaitHelper.WaitVisible(_driver, PasswordInput);
        passField.Clear();
        passField.SendKeys(password);

        WaitHelper.WaitClickable(_driver, SubmitButton).Click();
    }

    /// <summary>
    /// Trả về nội dung thông báo lỗi, hoặc string.Empty nếu không có lỗi.
    /// Dolibarr hiển thị lỗi trong div.login_main_message sau khi submit sai mật khẩu.
    /// </summary>
    public string GetErrorMessage()
    {
        try
        {
            // Chờ tối đa 5 giây — nếu không thấy thì không có lỗi
            var errEl = WaitHelper.WaitVisible(_driver, ErrorMessage, timeoutSeconds: 5);
            return errEl.Text.Trim();
        }
        catch (WebDriverTimeoutException)
        {
            return string.Empty;
        }
    }

    /// <summary>Kiểm tra đang ở trang đăng nhập (form login tồn tại và visible).</summary>
    public bool IsOnLoginPage()
    {
        try
        {
            var form = _driver.FindElement(LoginForm);
            return form.Displayed;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }
}

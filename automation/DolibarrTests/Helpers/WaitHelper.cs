using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace DolibarrTests.Helpers;

/// <summary>
/// Các phương thức chờ explicit — TUYỆT ĐỐI KHÔNG Thread.Sleep trong toàn bộ project.
/// Dùng WebDriverWait với lambda condition, không phụ thuộc SeleniumExtras.
/// </summary>
public static class WaitHelper
{
    private static WebDriverWait CreateWait(IWebDriver driver, int? timeoutSeconds = null)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds ?? TestConfig.TimeoutSeconds);
        return new WebDriverWait(driver, timeout);
    }

    /// <summary>Chờ element hiển thị (visible + displayed).</summary>
    public static IWebElement WaitVisible(IWebDriver driver, By locator, int? timeoutSeconds = null)
    {
        return CreateWait(driver, timeoutSeconds).Until(d =>
        {
            try
            {
                var el = d.FindElement(locator);
                return el.Displayed ? el : null;
            }
            catch (NoSuchElementException) { return null; }
            catch (StaleElementReferenceException) { return null; }
        })!;
    }

    /// <summary>Chờ element có thể click (visible + enabled).</summary>
    public static IWebElement WaitClickable(IWebDriver driver, By locator, int? timeoutSeconds = null)
    {
        return CreateWait(driver, timeoutSeconds).Until(d =>
        {
            try
            {
                var el = d.FindElement(locator);
                return (el.Displayed && el.Enabled) ? el : null;
            }
            catch (NoSuchElementException) { return null; }
            catch (StaleElementReferenceException) { return null; }
        })!;
    }

    /// <summary>Chờ URL chứa chuỗi cho trước.</summary>
    public static bool WaitUrlContains(IWebDriver driver, string urlFragment, int? timeoutSeconds = null)
    {
        return CreateWait(driver, timeoutSeconds).Until(d =>
            d.Url.Contains(urlFragment, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Chờ element biến mất khỏi DOM hoặc không còn hiển thị.</summary>
    public static bool WaitGone(IWebDriver driver, By locator, int? timeoutSeconds = null)
    {
        return CreateWait(driver, timeoutSeconds).Until(d =>
        {
            try
            {
                var el = d.FindElement(locator);
                return !el.Displayed;
            }
            catch (NoSuchElementException) { return true; }
            catch (StaleElementReferenceException) { return true; }
        });
    }

    /// <summary>Chờ element tồn tại trong DOM (chưa cần visible).</summary>
    public static IWebElement WaitPresent(IWebDriver driver, By locator, int? timeoutSeconds = null)
    {
        return CreateWait(driver, timeoutSeconds).Until(d =>
        {
            try { return d.FindElement(locator); }
            catch (NoSuchElementException) { return null; }
        })!;
    }

    /// <summary>Chờ title trang chứa chuỗi cho trước.</summary>
    public static bool WaitTitleContains(IWebDriver driver, string titleFragment, int? timeoutSeconds = null)
    {
        return CreateWait(driver, timeoutSeconds).Until(d =>
            d.Title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase));
    }
}

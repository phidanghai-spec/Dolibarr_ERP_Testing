using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace DolibarrTests.Helpers;

/// <summary>
/// Factory tạo ChromeDriver. KHÔNG dùng ImplicitWait — dùng WebDriverWait qua WaitHelper.
/// Selenium Manager tự tải ChromeDriver phù hợp với Chrome đã cài.
/// </summary>
public static class DriverFactory
{
    /// <summary>
    /// Tạo ChromeDriver với chế độ incognito, cửa sổ maximize.
    /// Headless được bật nếu cấu hình Browser:Headless = true.
    /// </summary>
    public static IWebDriver CreateChromeDriver(bool? headless = null)
    {
        var options = new ChromeOptions();

        // Incognito: đảm bảo không có session cũ
        options.AddArgument("--incognito");
        options.AddArgument("--start-maximized");

        // Tắt thông báo "Chrome is being controlled by automation"
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

        bool useHeadless = headless ?? TestConfig.Headless;
        if (useHeadless)
        {
            options.AddArgument("--headless=new");
            options.AddArgument("--window-size=1920,1080");
        }

        var driver = new ChromeDriver(options);

        // KHÔNG đặt ImplicitWait — dùng WebDriverWait qua WaitHelper
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);

        return driver;
    }
}

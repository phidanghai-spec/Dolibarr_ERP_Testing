using DolibarrTests.Helpers;
using OpenQA.Selenium;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho Dashboard (trang chủ sau đăng nhập) của Dolibarr.
///
/// Locator xác nhận từ HTML thật (cần probe sau khi đăng nhập thành công).
/// Dolibarr 22.x sau login hiển thị:
/// - div id="id-right"     : vùng nội dung chính (luôn có sau login)
/// - div id="id_top"       : top navigation bar
/// - span id="topmenu-home": menu item Home trong top bar
/// Dùng id="id-right" làm indicator chính vì nó không xuất hiện ở trang login.
/// QUAN TRỌNG: Nếu locator sai, chạy probe script để lấy HTML thật và cập nhật ở đây.
/// </summary>
public class DashboardPage
{
    private readonly IWebDriver _driver;

    // Locator đặc trưng của dashboard — PHẢI xác nhận từ HTML thật
    // Dolibarr 22.x: div id="id-right" là wrapper nội dung chính, chỉ xuất hiện sau login
    private static readonly By MainContentArea = By.Id("id-right");

    public DashboardPage(IWebDriver driver) => _driver = driver;

    /// <summary>
    /// Trả về true nếu Dashboard đã load (vùng nội dung chính hiển thị).
    /// Chờ tối đa TimeoutSeconds giây.
    /// </summary>
    public bool IsLoaded()
    {
        try
        {
            WaitHelper.WaitVisible(_driver, MainContentArea);
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }
}

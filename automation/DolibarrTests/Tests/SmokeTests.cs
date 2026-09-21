using DolibarrTests.Helpers;
using DolibarrTests.Pages;

namespace DolibarrTests.Tests;

/// <summary>
/// Smoke tests — kiểm tra chức năng đăng nhập cơ bản.
/// SMOKE_01: Đăng nhập đúng → vào Dashboard.
/// SMOKE_02: Đăng nhập sai → ở lại trang login, có thông báo lỗi.
/// </summary>
[TestClass]
public class SmokeTests : BaseTest
{
    /// <summary>
    /// SMOKE_01: Đăng nhập admin đúng mật khẩu.
    /// Expected: IsLoaded() = true, URL không còn chứa chuỗi đăng nhập.
    /// </summary>
    [TestMethod]
    [TestCategory("Smoke")]
    [Description("TC_CRM_001 proxy — Đăng nhập admin đúng mật khẩu → Dashboard load")]
    public void SMOKE_01_LoginAdmin_ValidCredentials_ShouldNavigateToDashboard()
    {
        // Arrange
        var loginPage = new LoginPage(Driver);
        var dashboardPage = new DashboardPage(Driver);

        loginPage.GoTo();
        Assert.IsTrue(loginPage.IsOnLoginPage(), "Phải đang ở trang đăng nhập trước khi test");

        // Act — đọc mật khẩu từ env var (ném lỗi rõ ràng nếu chưa đặt)
        loginPage.LoginAs(TestConfig.AdminUsername, TestConfig.AdminPassword);

        // Assert 1: Dashboard đã load (phần tử id="id-right" hiển thị)
        bool dashboardLoaded = dashboardPage.IsLoaded();
        Assert.IsTrue(dashboardLoaded,
            "Dashboard phải load sau khi đăng nhập đúng (id='id-right' không thấy)");

        // Assert 2: URL không còn ở trang login — đã redirect sang home
        bool urlChanged = !Driver.Url.Contains("actionlogin=login", StringComparison.OrdinalIgnoreCase)
                          && !loginPage.IsOnLoginPage();
        Assert.IsTrue(urlChanged,
            $"URL phải rời khỏi trang login sau khi đăng nhập. URL hiện tại: {Driver.Url}");
    }

    /// <summary>
    /// SMOKE_02: Đăng nhập sai mật khẩu.
    /// Expected: vẫn ở trang login, thông báo lỗi khác rỗng.
    /// Mật khẩu sai dùng chuỗi giả cố định — không liên quan đến mật khẩu thật.
    /// </summary>
    [TestMethod]
    [TestCategory("Smoke")]
    [Description("Đăng nhập sai mật khẩu → ở lại login, có lỗi")]
    public void SMOKE_02_LoginAdmin_WrongPassword_ShouldShowError()
    {
        // Arrange
        var loginPage = new LoginPage(Driver);

        loginPage.GoTo();
        Assert.IsTrue(loginPage.IsOnLoginPage(), "Phải đang ở trang đăng nhập trước khi test");

        // Act — dùng mật khẩu giả, KHÔNG liên quan đến DOLIBARR_ADMIN_PASSWORD
        loginPage.LoginAs(TestConfig.AdminUsername, "sai-mat-khau-xyz");

        // Assert 1: Vẫn ở trang đăng nhập sau khi đăng nhập thất bại
        Assert.IsTrue(loginPage.IsOnLoginPage(),
            "Phải ở lại trang đăng nhập khi mật khẩu sai");

        // Assert 2: Thông báo lỗi phải khác rỗng (Dolibarr hiển thị lỗi rõ ràng)
        var errorMsg = loginPage.GetErrorMessage();
        Assert.IsFalse(string.IsNullOrWhiteSpace(errorMsg),
            "Thông báo lỗi đăng nhập phải hiển thị khi mật khẩu sai (GetErrorMessage() trả về rỗng)");

        TestContext.WriteLine($"[SMOKE_02] Thông báo lỗi: {errorMsg}");
    }
}

using DolibarrTests.Helpers;
using OpenQA.Selenium;

namespace DolibarrTests.Tests;

/// <summary>
/// Base class cho mọi test class.
/// [TestInitialize]: tạo driver mới → mỗi test có browser riêng.
/// [TestCleanup]: chụp ảnh khi Fail → quit driver.
/// </summary>
[TestClass]
public abstract class BaseTest
{
    protected IWebDriver Driver { get; private set; } = null!;

    /// <summary>TestContext được MSTest inject tự động — dùng để lấy tên test và outcome.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void InitializeTest()
    {
        Driver = DriverFactory.CreateChromeDriver();

        // Khởi tạo ExtentTest node cho báo cáo HTML
        string testName = TestContext.TestName ?? "UnknownTest";
        var extentTest = ExtentReportManager.CreateTest(testName);

        // Gán Category và Author cho báo cáo
        string className = TestContext.FullyQualifiedTestClassName ?? string.Empty;
        if (className.Contains("Crm", StringComparison.OrdinalIgnoreCase))
            extentTest.AssignCategory("CRM");
        else if (className.Contains("Sales", StringComparison.OrdinalIgnoreCase))
            extentTest.AssignCategory("Sales & Invoicing");
        else if (className.Contains("Stock", StringComparison.OrdinalIgnoreCase) || className.Contains("Special", StringComparison.OrdinalIgnoreCase))
            extentTest.AssignCategory("Stock & Invoicing Special");
        else
            extentTest.AssignCategory("General");

        extentTest.AssignAuthor("Dang Hai Phi (23DH112608)");
        extentTest.Info($"Bắt đầu thực thi: {testName}");
    }

    [TestCleanup]
    public void CleanupTest()
    {
        var extentTest = ExtentReportManager.CurrentTest;
        try
        {
            if (TestContext.CurrentTestOutcome == UnitTestOutcome.Passed)
            {
                extentTest?.Pass("Kiểm thử thành công (PASS)");
            }
            else if (TestContext.CurrentTestOutcome == UnitTestOutcome.Failed)
            {
                // Chụp ảnh khi test FAIL và đính kèm vào ExtentReport
                var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "unknown");
                if (screenshotPath != null)
                {
                    TestContext.WriteLine($"[Screenshot khi Fail] {screenshotPath}");
                    extentTest?.AddScreenCaptureFromPath(screenshotPath, "Minh chứng lỗi khi Fail");
                }
                extentTest?.Fail("Kiểm thử thất bại (FAIL)");
            }
            else
            {
                extentTest?.Skip("Kiểm thử bị bỏ qua (SKIPPED)");
            }
        }
        catch (Exception ex)
        {
            extentTest?.Warning($"Lỗi trong quá trình ghi log ExtentReport: {ex.Message}");
        }
        finally
        {
            try
            {
                ExtentReportManager.Flush();
            }
            catch { }

            Driver?.Quit();
        }
    }
}

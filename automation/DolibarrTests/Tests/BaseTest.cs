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
    }

    [TestCleanup]
    public void CleanupTest()
    {
        try
        {
            // Chụp ảnh khi test FAIL
            if (TestContext.CurrentTestOutcome == UnitTestOutcome.Failed)
            {
                var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "unknown");
                if (screenshotPath != null)
                {
                    TestContext.WriteLine($"[Screenshot khi Fail] {screenshotPath}");
                }
            }
        }
        finally
        {
            Driver?.Quit();
        }
    }
}

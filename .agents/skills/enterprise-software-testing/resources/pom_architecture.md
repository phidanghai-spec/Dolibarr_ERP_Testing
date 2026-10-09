# Kiến Trúc Code Automation Chuẩn Mực (POM Architecture Blueprint)

Tài liệu cung cấp khung mã nguồn mẫu cho hệ thống kiểm thử tự động Page Object Model (POM), đảm bảo tính ổn định, dễ bảo trì và loại bỏ hoàn toàn hiện tượng Flaky Test.

---

## 1. Helper Chống Flaky Vàng: `WaitHelper.cs` (Tuyệt đối không `Thread.Sleep`)

```csharp
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EnterpriseTesting.Helpers;

public static class WaitHelper
{
    public static IWebElement WaitVisible(IWebDriver driver, By locator, int timeoutSeconds = 15)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return wait.Until(d =>
        {
            try
            {
                var el = d.FindElement(locator);
                return el.Displayed ? el : null;
            }
            catch (StaleElementReferenceException) { return null; }
            catch (NoSuchElementException) { return null; }
        })!;
    }

    public static IWebElement WaitClickable(IWebDriver driver, By locator, int timeoutSeconds = 15)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return wait.Until(d =>
        {
            try
            {
                var el = d.FindElement(locator);
                return (el.Displayed && el.Enabled) ? el : null;
            }
            catch (StaleElementReferenceException) { return null; }
            catch (NoSuchElementException) { return null; }
        })!;
    }

    public static bool WaitGone(IWebDriver driver, By locator, int timeoutSeconds = 10)
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(d =>
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
        catch (WebDriverTimeoutException) { return false; }
    }
}
```

---

## 2. Lớp Cơ Sở Quản Lý Vòng Đời Test: `BaseTest.cs`

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace EnterpriseTesting.Tests;

[TestClass]
public abstract class BaseTest
{
    protected IWebDriver Driver = null!;
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public virtual void SetUp()
    {
        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--incognito");
        options.AddArgument("--disable-search-engine-choice-screen");

        Driver = new ChromeDriver(options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(0); // Luôn dùng Explicit Wait
    }

    [TestCleanup]
    public virtual void TearDown()
    {
        try
        {
            // Tự động chụp ảnh nếu test Fail
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
            {
                string screenshotPath = Path.Combine("TestResults", "Screenshots", $"{TestContext.TestName}_FAIL.png");
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(screenshotPath);
                TestContext.WriteLine($"[FAIL CAPTURED] Screenshot: {screenshotPath}");
            }
        }
        finally
        {
            Driver?.Quit();
            Driver?.Dispose();
        }
    }
}
```

---

## 3. Mẫu Thiết Kế Page Object: `CustomerCreatePage.cs`

```csharp
using OpenQA.Selenium;
using EnterpriseTesting.Helpers;

namespace EnterpriseTesting.Pages;

public class CustomerCreatePage
{
    private readonly IWebDriver _driver;

    // Locators định nghĩa tập trung
    private readonly By _nameInput = By.Name("name");
    private readonly By _phoneInput = By.Name("phone");
    private readonly By _submitButton = By.CssSelector("input[type='submit'][value='Create third party']");
    private readonly By _errorMessage = By.CssSelector("div.error, div.warning");

    public CustomerCreatePage(IWebDriver driver)
    {
        _driver = driver;
    }

    public CustomerCreatePage EnterCustomerName(string name)
    {
        var input = WaitHelper.WaitVisible(_driver, _nameInput);
        input.Clear();
        input.SendKeys(name);
        return this;
    }

    public CustomerCreatePage EnterPhone(string phone)
    {
        var input = WaitHelper.WaitVisible(_driver, _phoneInput);
        input.Clear();
        input.SendKeys(phone);
        return this;
    }

    public void ClickSubmit()
    {
        WaitHelper.WaitClickable(_driver, _submitButton).Click();
    }

    public string GetErrorMessage()
    {
        return WaitHelper.WaitVisible(_driver, _errorMessage).Text.Trim();
    }
}
```

---

## 4. Mẫu Viết Test Method: `CustomerTests.cs`

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EnterpriseTesting.Pages;

namespace EnterpriseTesting.Tests;

[TestClass]
[DoNotParallelize] // Chạy tuần tự an toàn, chống race condition
public class CustomerTests : BaseTest
{
    [TestMethod]
    [TestCategory("CRM_Boundary")]
    [Description("TC_CRM_005: Kiểm tra lưu thành công khách hàng với tên đạt chuẩn biên 128 ký tự")]
    public void TC_CRM_005_CreateCustomer_NameExactly128Chars_ShouldSucceed()
    {
        // 1. Arrange
        string valid128Name = "AUTO_" + new string('A', 123);
        var createPage = new CustomerCreatePage(Driver);

        // 2. Act
        createPage.EnterCustomerName(valid128Name)
                  .EnterPhone("0901234567");
        createPage.ClickSubmit();

        // 3. Assert (Định lượng giá trị cụ thể)
        Assert.IsTrue(Driver.Url.Contains("socid="), "Hệ thống phải lưu thành công và điều hướng tới trang chi tiết có chứa socid.");
        StringAssert.DoesNotMatch(Driver.Url, new System.Text.RegularExpressions.Regex(@"__ID__"), "URL không được chứa placeholder lỗi __ID__.");
    }
}
```

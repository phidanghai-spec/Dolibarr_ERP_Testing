using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using OpenQA.Selenium;

namespace DolibarrTests.Tests;

/// <summary>
/// Chạy thực nghiệm trực tiếp 3 ca kiểm thử:
/// - TC_CRM_019: SĐT 10 chữ số
/// - TC_CRM_020: SĐT chứa ký tự chữ cái (Finding: thiếu validation)
/// - TC_CRM_022: Tên khách hàng trùng lặp ("Cong ty ABC")
/// </summary>
[TestClass]
[DoNotParallelize]
public class CrmQuickVerificationTests : BaseTest
{
    private string? _createdCustomerUrl;

    private void Login()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.GoTo();
        loginPage.LoginAs(TestConfig.AdminUsername, TestConfig.AdminPassword);
        var dashboard = new DashboardPage(Driver);
        Assert.IsTrue(dashboard.IsLoaded(), "Đăng nhập admin phải thành công.");
    }

    private void CleanupCustomer()
    {
        if (string.IsNullOrEmpty(_createdCustomerUrl)) return;
        try
        {
            string cleanUrl = CustomerDetailPage.NormalizeCustomerUrl(_createdCustomerUrl);
            Driver.Navigate().GoToUrl(cleanUrl);
            var detailPage = new CustomerDetailPage(Driver);
            detailPage.DeleteCustomer(timeoutSeconds: 10);
        }
        catch { }
        finally { _createdCustomerUrl = null; }
    }

    [TestMethod]
    [TestCategory("CRM_Verify")]
    [Description("TC_CRM_019 — Tạo khách hàng với SĐT hợp lệ 10 số")]
    public void TC_CRM_019_VerifyPhone10Digits()
    {
        _createdCustomerUrl = null;
        try
        {
            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            createPage.SelectCustomerType();
            string testName = "TC019_Phone10_Test_" + Guid.NewGuid().ToString("N")[..4];
            createPage.EnterName(testName);
            createPage.EnterPhone("0912345678");
            createPage.ClickSave();

            bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 15);
            _createdCustomerUrl = Driver.Url;

            Assert.IsTrue(redirected, $"[TC_CRM_019] Phải lưu thành công và redirect. URL hiện tại: {_createdCustomerUrl}");
            
            // Xác thực SĐT có xuất hiện trên trang chi tiết
            string pageSource = Driver.PageSource;
            bool phoneFound = pageSource.Contains("0912345678");
            TestContext.WriteLine($"[TC_CRM_019 ACTUAL] Lưu thành công: redirected={redirected}, SĐT 0912345678 hiển thị={phoneFound}, URL={_createdCustomerUrl}");
            Assert.IsTrue(phoneFound, "[TC_CRM_019] SĐT '0912345678' phải hiển thị trên trang chi tiết.");
        }
        finally
        {
            CleanupCustomer();
        }
    }

    [TestMethod]
    [TestCategory("CRM_Verify")]
    [Description("TC_CRM_020 — Tạo khách hàng với SĐT lẫn ký tự chữ 09ABCD5678")]
    public void TC_CRM_020_VerifyPhoneInvalidChars()
    {
        _createdCustomerUrl = null;
        try
        {
            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            createPage.SelectCustomerType();
            string testName = "TC020_PhoneInvalid_Test_" + Guid.NewGuid().ToString("N")[..4];
            createPage.EnterName(testName);
            createPage.EnterPhone("09ABCD5678");
            createPage.ClickSave();

            bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 10);
            if (redirected)
            {
                _createdCustomerUrl = Driver.Url;
                string pageSource = Driver.PageSource;
                bool phoneFound = pageSource.Contains("09ABCD5678");
                TestContext.WriteLine($"[TC_CRM_020 ACTUAL] Dolibarr KHÔNG chặn ký tự chữ: Lưu thành công URL={_createdCustomerUrl}, SĐT '09ABCD5678' hiển thị={phoneFound}");
                // Kết luận: Dolibarr lưu nguyên văn, trường Phone là free-text (thiếu format validation)
                Assert.IsTrue(redirected, "Dolibarr mặc định cho phép lưu SĐT free-text.");
            }
            else
            {
                string errMsg = createPage.GetErrorMessage();
                TestContext.WriteLine($"[TC_CRM_020 ACTUAL] Dolibarr CÓ validation và chặn lại: Lỗi='{errMsg}'");
            }
        }
        finally
        {
            CleanupCustomer();
        }
    }

    [TestMethod]
    [TestCategory("CRM_Verify")]
    [Description("TC_CRM_022 — Tạo khách hàng với Tên trùng tuyệt đối 'Cong ty ABC'")]
    public void TC_CRM_022_VerifyDuplicateCustomerName()
    {
        _createdCustomerUrl = null;
        try
        {
            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            createPage.SelectCustomerType();
            createPage.EnterName("Cong ty ABC");
            createPage.ClickSave();

            bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 10);
            if (redirected)
            {
                _createdCustomerUrl = Driver.Url;
                string newSocId = detailPage.GetSocId();
                TestContext.WriteLine($"[TC_CRM_022 ACTUAL] Dolibarr KHÔNG ràng buộc UNIQUE tên: Tạo thành công bản ghi trùng tên với ID mới = {newSocId}, URL={_createdCustomerUrl}");
                Assert.IsTrue(redirected, "Dolibarr mặc định cho phép tạo nhiều khách hàng cùng tên.");
            }
            else
            {
                string errMsg = createPage.GetErrorMessage();
                TestContext.WriteLine($"[TC_CRM_022 ACTUAL] Dolibarr CHẶN trùng tên: Lỗi='{errMsg}'");
            }
        }
        finally
        {
            CleanupCustomer();
        }
    }
}

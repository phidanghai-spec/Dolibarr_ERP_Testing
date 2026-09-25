using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using OpenQA.Selenium;

namespace DolibarrTests.Tests;

/// <summary>
/// TC_CRM — Tạo khách hàng mới (Third Party / Société) — BVA tên.
///
/// Dữ liệu: đọc từ sheet "Test Data" trong Dolibarr_TestCases.xlsx qua ExcelDataReader.
///   TC_CRM_005: tên 128 ký tự (biên đúng N)
///   TC_CRM_006: tên 129 ký tự (biên N+1 — browser cắt còn 128)
///
/// Cleanup: xóa khách hàng vừa tạo sau mỗi test (tránh tích lũy rác trong DB).
///   Dolibarr cho phép xóa third party qua: /societe/card.php?id={id}&action=delete
///   kèm token CSRF. Cách đơn giản nhất: điều hướng + click nút xóa trên trang chi tiết.
/// </summary>
[TestClass]
[DoNotParallelize] // CRM tests dùng chung session Dolibarr — chạy song song gây race condition login
public class CrmCustomerTests : BaseTest
{
    // ── Hằng nghiệp vụ ───────────────────────────────────────────────────────────
    private const int MaxNameLength = 128; // varchar(128) + maxlength=128 trong card.php

    // ── ID khách hàng đã tạo (dùng để cleanup) ───────────────────────────────────
    // Reset về null trước mỗi test, gán sau khi redirect thành công.
    private string? _createdCustomerUrl;

    // ── Helper: đăng nhập ─────────────────────────────────────────────────────────
    private void Login()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.GoTo();
        loginPage.LoginAs(TestConfig.AdminUsername, TestConfig.AdminPassword);
        var dashboard = new DashboardPage(Driver);
        Assert.IsTrue(dashboard.IsLoaded(),
            "Đăng nhập admin phải thành công. Kiểm tra Dolibarr đang chạy và " +
            "biến môi trường DOLIBARR_ADMIN_PASSWORD.");
    }

    // ── Helper: xóa khách hàng sau test (cleanup) ────────────────────────────────
    /// <summary>
    /// Điều hướng đến trang chi tiết KH vừa tạo và xóa.
    /// Dolibarr yêu cầu qua 2 bước: (1) click nút Xóa → (2) xác nhận popup confirm.
    /// Nếu xóa thất bại → log cảnh báo, KHÔNG fail test (cleanup không phải mục tiêu test).
    /// </summary>
    private void CleanupCreatedCustomer()
    {
        if (string.IsNullOrEmpty(_createdCustomerUrl)) return;

        try
        {
            Driver.Navigate().GoToUrl(_createdCustomerUrl);

            // Xác nhận từ card.php dòng ~3440:
            // - id="action-delete"          (khi JS bật — click mở confirm popup)
            // - id="action-delete-no-ajax"  (khi JS tắt — link trực tiếp)
            // Ưu tiên id="action-delete" vì Dolibarr bật JS theo mặc định.
            var deleteBtn = By.Id("action-delete");
            var el = WaitHelper.WaitClickable(Driver, deleteBtn, timeoutSeconds: 8);
            el.Click();

            // Xác nhận native browser confirm dialog
            try
            {
                var alert = Driver.SwitchTo().Alert();
                alert.Accept();
                TestContext.WriteLine($"[Cleanup] Đã xóa KH: {_createdCustomerUrl}");
            }
            catch (OpenQA.Selenium.NoAlertPresentException)
            {
                // Dolibarr 22 dùng custom confirm form thay native alert trong một số trường hợp
                // Thử nút confirm trong form
                var confirmYes = By.CssSelector("input[name='confirm'][value='yes'], button.btnyes");
                WaitHelper.WaitClickable(Driver, confirmYes, timeoutSeconds: 5).Click();
                TestContext.WriteLine($"[Cleanup] Đã xóa KH (form confirm): {_createdCustomerUrl}");
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine(
                $"[Cleanup WARN] Không xóa được KH {_createdCustomerUrl}: {ex.Message}. " +
                "Xóa thủ công trong Dolibarr nếu cần.");
        }
        finally
        {
            _createdCustomerUrl = null;
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_005 — BVA biên đúng N (128 ký tự)
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_005: Tạo khách hàng với tên đúng 128 ký tự (giới hạn tối đa).
    ///
    /// Nguồn dữ liệu: sheet "Test Data", cột CustomerName (128 ký tự cố định làm template).
    /// Code thêm suffix GUID 6 ký tự hex + cắt bớt để tổng vẫn đúng 128 ký tự
    /// → mỗi lần chạy tên KH khác nhau, tránh trùng trong DB.
    ///
    /// Expected:
    ///   - maxlength attribute = "128"
    ///   - Ô nhập nhận đủ 128 ký tự (không bị cắt)
    ///   - Form lưu thành công (redirect về trang chi tiết, không action=create)
    ///   - Không có thông báo lỗi
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("BVA")]
    [Description("TC_CRM_005 — Tạo KH tên đúng 128 ký tự (biên N). Dữ liệu từ Excel Test Data.")]
    public void TC_CRM_005_CreateCustomer_NameExactly128Chars_ShouldSaveSuccessfully()
    {
        _createdCustomerUrl = null;
        try
        {
            // ── Arrange: đọc template từ Excel ───────────────────────────────────────
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_005", TestConfig.ExcelPath);
            string template = row["CustomerName"]; // 128 ký tự từ Excel

            // Tạo tên duy nhất: thay 6 ký tự cuối bằng suffix Guid để tránh trùng DB
            string suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
            string name128 = template[..(MaxNameLength - suffix.Length)] + suffix;

            Assert.AreEqual(MaxNameLength, name128.Length,
                $"[Arrange] Tên test phải đúng {MaxNameLength} ký tự, thực tế: {name128.Length}");

            int expectedLen = int.Parse(row["ExpectedNameLength"]);
            Assert.AreEqual(MaxNameLength, expectedLen,
                $"[Arrange] ExpectedNameLength trong Excel phải là {MaxNameLength}");

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            // ── Act ──────────────────────────────────────────────────────────────────
            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(),
                "Phải điều hướng được đến trang tạo KH mới.");

            // Assert maxlength attribute của ô tên
            string maxLenAttr = createPage.GetNameMaxLength();
            Assert.AreEqual("128", maxLenAttr,
                $"[TC_CRM_005] maxlength của ô tên phải là '128', thực tế: '{maxLenAttr}'");

            createPage.SelectCustomerType();
            createPage.EnterName(name128);

            // Assert độ dài trong ô SAU KHI nhập
            string actualInInput = createPage.GetNameInputValue();
            Assert.AreEqual(MaxNameLength, actualInInput.Length,
                $"[TC_CRM_005] Ô nhập phải chứa đúng {MaxNameLength} ký tự sau khi nhập, " +
                $"thực tế: {actualInInput.Length}");
            Assert.AreEqual(name128, actualInInput,
                "[TC_CRM_005] Giá trị ô nhập phải khớp chính xác chuỗi 128 ký tự.");

            createPage.ClickSave();

            // ── Assert: lưu thành công ────────────────────────────────────────────────
            bool redirected = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url; // Lưu URL để cleanup

            Assert.IsTrue(redirected,
                $"[TC_CRM_005] Form phải redirect về trang chi tiết sau khi lưu. " +
                $"URL hiện tại: {Driver.Url}");

            string errMsg = createPage.GetErrorMessage();
            Assert.AreEqual(string.Empty, errMsg,
                $"[TC_CRM_005] Không được có thông báo lỗi. Lỗi nhận được: '{errMsg}'");

            TestContext.WriteLine(
                $"[TC_CRM_005 PASS] Tên {name128.Length} ký tự lưu thành công. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_005");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            // Cleanup: xóa KH vừa tạo để không tích lũy rác trong DB
            CleanupCreatedCustomer();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_006 — BVA biên vượt N+1 (129 ký tự) — kiểm tra hành vi UI maxlength
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_006: Nhập tên 129 ký tự vào ô có maxlength=128.
    ///
    /// Nguồn dữ liệu: sheet "Test Data", cột CustomerName (129 ký tự).
    /// Code thay 6 ký tự để suffix Guid, tổng vẫn 129 ký tự.
    ///
    /// Hành vi UI thực tế: browser cắt tại maxlength=128, ký tự thứ 129 bị bỏ.
    /// Selenium SendKeys cũng bị giới hạn bởi maxlength attribute.
    ///
    /// Assert CHÍNH: độ dài GIÁ TRỊ Ô NHẬP = 128 (KHÔNG assert thông báo lỗi,
    /// vì đây là hành vi cắt của browser, không phải lỗi validation).
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("BVA")]
    [Description("TC_CRM_006 — Nhập 129 ký tự vào ô maxlength=128: browser cắt còn 128, form lưu được.")]
    public void TC_CRM_006_CreateCustomer_Name129Chars_BrowserShouldTruncateTo128()
    {
        _createdCustomerUrl = null;
        try
        {
            // ── Arrange: đọc template từ Excel ───────────────────────────────────────
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_006", TestConfig.ExcelPath);
            string template = row["CustomerName"]; // 129 ký tự từ Excel

            // Tạo tên duy nhất 129 ký tự: thay 6 ký tự cuối bằng suffix Guid
            string suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
            string name129 = template[..(129 - suffix.Length)] + suffix;

            Assert.AreEqual(129, name129.Length,
                $"[Arrange] Tên test phải đúng 129 ký tự, thực tế: {name129.Length}");

            int expectedLen = int.Parse(row["ExpectedNameLength"]); // Excel ghi 128
            Assert.AreEqual(MaxNameLength, expectedLen,
                $"[Arrange] ExpectedNameLength trong Excel phải là {MaxNameLength} " +
                "(browser cắt còn 128 sau khi nhập 129)");

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            // ── Act ──────────────────────────────────────────────────────────────────
            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(),
                "Phải điều hướng được đến trang tạo KH mới.");

            createPage.SelectCustomerType();
            createPage.EnterName(name129); // Browser + Selenium cắt tại maxlength=128

            // ── Assert 1: Ô nhập chứa đúng 128 ký tự (browser đã cắt ký tự 129) ─────
            string actualInInput = createPage.GetNameInputValue();
            Assert.AreEqual(MaxNameLength, actualInInput.Length,
                $"[TC_CRM_006] Sau khi nhập 129 ký tự vào ô maxlength=128, " +
                $"giá trị ô nhập PHẢI là {MaxNameLength} ký tự (browser cắt ký tự thứ 129). " +
                $"Thực tế: {actualInInput.Length} ký tự.");

            // Assert chuỗi trong ô = 128 ký tự đầu của name129
            string expected128Prefix = name129[..MaxNameLength];
            Assert.AreEqual(expected128Prefix, actualInInput,
                "[TC_CRM_006] Chuỗi trong ô phải là 128 ký tự đầu của chuỗi 129 ký tự đã nhập.");

            createPage.ClickSave();

            // ── Assert 2: Form lưu được với tên 128 ký tự, không báo lỗi ─────────────
            bool redirected = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;

            Assert.IsTrue(redirected,
                $"[TC_CRM_006] Form phải lưu thành công (tên đã bị cắt còn 128 ký tự). " +
                $"URL hiện tại: {Driver.Url}");

            string errMsg = createPage.GetErrorMessage();
            Assert.AreEqual(string.Empty, errMsg,
                $"[TC_CRM_006] Không được có thông báo lỗi sau khi lưu. Lỗi: '{errMsg}'");

            TestContext.WriteLine(
                $"[TC_CRM_006 PASS] Browser cắt còn {actualInInput.Length} ký tự. " +
                $"Lưu thành công. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_006");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }
}

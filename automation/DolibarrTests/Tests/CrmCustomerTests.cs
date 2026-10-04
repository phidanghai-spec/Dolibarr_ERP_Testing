using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Net;
using System.Text.RegularExpressions;

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
            // Loai bo tham so action=... (nhu action=edit) de dam bao ve trang chi tiet co nut Xoa
            string cleanUrl = Regex.Replace(_createdCustomerUrl, @"([&?])action=[^&]+(&|$)", "$1").TrimEnd('?', '&');
            Driver.Navigate().GoToUrl(cleanUrl);
            var detailPage = new CustomerDetailPage(Driver);
            bool deleted = detailPage.DeleteCustomer(timeoutSeconds: 15);
            if (deleted)
            {
                TestContext.WriteLine($"[Cleanup] Đã xóa KH: {cleanUrl}");
            }
            else
            {
                TestContext.WriteLine($"[Cleanup WARN] Không xóa được KH qua DeleteCustomer: {cleanUrl}");
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

    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_007 — Tên rỗng (Negative Testing)
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_007: Tạo khách hàng với tên rỗng.
    /// Hành vi thực tế quan sát từ log chạy 2026-10-03:
    ///   - Dolibarr KHÔNG có HTML5 required attribute trên ô tên.
    ///   - Server chặn submit, trả về error: "Field 'Third-party name' is required".
    ///   - Không redirect thành công (WaitForRedirectAfterSave = false).
    /// → Chốt 1 hành vi: server-side chặn.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Negative")]
    [Description("TC_CRM_007 — Tạo KH với tên rỗng: server-side chặn, trả về lỗi 'Third-party name is required'.")]
    public void TC_CRM_007_CreateCustomer_EmptyName_ShouldBeBlocked()
    {
        _createdCustomerUrl = null;
        try
        {
            // Test data: tên rỗng (không nhập gì)
            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            // Xac nhan: Dolibarr KHONG co HTML5 required (quan sat 2026-10-03)
            bool hasRequired = createPage.IsNameInputRequired();
            TestContext.WriteLine($"[TC_CRM_007] required attribute: {hasRequired} (expected=False)");
            Assert.IsFalse(hasRequired,
                "[TC_CRM_007] Dolibarr 22.0.4 KHONG co HTML5 required tren input name — neu fail, UI da thay doi.");

            createPage.SelectCustomerType();
            // Khong nhap ten (de rong)

            createPage.ClickSave();

            // Assert 1: Server chặn — KHÔNG redirect thành công
            bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 5);
            TestContext.WriteLine($"[TC_CRM_007] Redirected={redirected} (expected=False)");
            Assert.IsFalse(redirected,
                $"[TC_CRM_007] Server phai chan submit ten rong, KHONG duoc redirect ve trang chi tiet. URL: {Driver.Url}");

            // Assert 2: Thông báo lỗi phải chứa "required" (quan sát: "Field 'Third-party name' is required")
            string errMsg = createPage.GetErrorMessage();
            TestContext.WriteLine($"[TC_CRM_007] ErrorMessage='{errMsg}'");
            Assert.IsFalse(string.IsNullOrWhiteSpace(errMsg),
                "[TC_CRM_007] Server phai hien thi thong bao loi khi ten rong.");
            Assert.IsTrue(
                errMsg.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                errMsg.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase) ||
                errMsg.Contains("obligatoire", StringComparison.OrdinalIgnoreCase),
                $"[TC_CRM_007] Thong bao loi phai chua tu khoa yeu cau nhap ten ('required'/'obligatoire'/'bat buoc'). Nhan duoc: '{errMsg}'");

            TestContext.WriteLine($"[TC_CRM_007 PASS] Server chan dung. ErrorMessage='{errMsg}'");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_007");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }


    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_008 — Tên 1 ký tự (BVA Biên tối thiểu N=1)
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_008: Tạo khách hàng với tên đúng 1 ký tự ("A" đọc từ Excel).
    /// Expected: Lưu thành công, redirect về card.php, tên hiển thị khớp "A", không báo lỗi.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("BVA")]
    [Description("TC_CRM_008 — Tạo KH với tên 1 ký tự ('A' từ Excel): lưu thành công.")]
    public void TC_CRM_008_CreateCustomer_SingleChar_ShouldSaveSuccessfully()
    {
        _createdCustomerUrl = null;
        try
        {
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_008", TestConfig.ExcelPath);
            string singleCharName = row["Test data"];
            Assert.AreEqual("A", singleCharName, "[Arrange] Dữ liệu từ Excel phải là 'A'");

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            createPage.SelectCustomerType();
            createPage.EnterName(singleCharName);

            string actualInInput = createPage.GetNameInputValue();
            Assert.AreEqual("A", actualInInput,
                $"[TC_CRM_008] Ô nhập phải nhận đúng 'A', thực tế: '{actualInInput}'");

            createPage.ClickSave();

            bool redirected = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;

            Assert.IsTrue(redirected,
                $"[TC_CRM_008] Form phải redirect về trang chi tiết sau khi lưu. URL: {Driver.Url}");

            string errMsg = createPage.GetErrorMessage();
            Assert.AreEqual(string.Empty, errMsg,
                $"[TC_CRM_008] Không được có thông báo lỗi. Nhận được: '{errMsg}'");

            string displayedName = detailPage.GetDisplayedName();
            Assert.AreEqual("A", displayedName,
                $"[TC_CRM_008] Tên hiển thị trên trang chi tiết phải là 'A', thực tế: '{displayedName}'");

            TestContext.WriteLine(
                $"[TC_CRM_008 PASS] Lưu thành công KH tên 1 ký tự '{singleCharName}'. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_008");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_009 — Tên chỉ gồm khoảng trắng (Edge Case)
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_009: Nhập tên chỉ gồm 10 khoảng trắng.
    /// Hành vi thực tế quan sát 2026-10-03:
    ///   - Input trước submit: length=10, raw='          '.
    ///   - Server trim khoảng trắng → xem là tên rỗng → chặn giống TC_CRM_007.
    ///   - ErrorMessage: "Field 'Third-party name' is required".
    ///   - Redirected=False, SavedNameLength=0.
    /// Assert chốt theo hành vi thật này.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("EdgeCase")]
    [Description("TC_CRM_009 — Tên chỉ gồm khoảng trắng: Dolibarr trim → server chặn như tên rỗng.")]
    public void TC_CRM_009_CreateCustomer_WhitespaceOnly_ShouldBeBlocked()
    {
        _createdCustomerUrl = null;
        try
        {
            // 10 khoảng trắng — dữ liệu cố định (không cần đọc Excel vì logic đã xác định)
            string whitespaceName = new string(' ', 10);

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            createPage.SelectCustomerType();
            createPage.EnterName(whitespaceName);

            // Ghi nhận giá trị input trước submit
            string actualInInput = createPage.GetNameInputValue();
            Assert.AreEqual(10, actualInInput.Length,
                $"[TC_CRM_009] Ô nhập phải chứa 10 ký tự khoảng trắng, thực tế: {actualInInput.Length}");
            TestContext.WriteLine($"[TC_CRM_009] Input trước submit: length={actualInInput.Length}");

            createPage.ClickSave();

            // Assert 1: Server chặn — KHÔNG redirect
            bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 5);
            TestContext.WriteLine($"[TC_CRM_009] Redirected={redirected} (expected=False)");
            Assert.IsFalse(redirected,
                $"[TC_CRM_009] Server phai chan ten chi gom khoang trang (trim → rong). URL: {Driver.Url}");

            // Assert 2: Thông báo lỗi giống tên rỗng
            string errMsg = createPage.GetErrorMessage();
            TestContext.WriteLine($"[TC_CRM_009] ErrorMessage='{errMsg}' (expected chua 'required')");
            Assert.IsFalse(string.IsNullOrWhiteSpace(errMsg),
                "[TC_CRM_009] Server phai hien thi thong bao loi khi ten chi gom khoang trang.");
            Assert.IsTrue(
                errMsg.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                errMsg.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase) ||
                errMsg.Contains("obligatoire", StringComparison.OrdinalIgnoreCase),
                $"[TC_CRM_009] Loi phai chua 'required'/'obligatoire'/'bat buoc'. Nhan duoc: '{errMsg}'");

            // Assert 3: Không có Fatal/Parse error PHP
            string pageSource = Driver.PageSource;
            Assert.IsFalse(pageSource.Contains("Fatal error:", StringComparison.OrdinalIgnoreCase),
                "[TC_CRM_009] Khong duoc co 'Fatal error' cua PHP.");

            TestContext.WriteLine($"[TC_CRM_009 PASS] Dolibarr chan dung ten chi gom khoang trang. ErrorMessage='{errMsg}'");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_009");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_010 — Ký tự đặc biệt & Kiểm tra XSS
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_010: Tạo KH với chuỗi O'Brien &amp; Cong ty &lt;Test&gt; "123".
    /// Hành vi thực tế quan sát 2026-10-03:
    ///   - Dolibarr xóa thẻ HTML &lt;Test&gt; và ký tự ngoặc kép "123" → lưu: "O'Brien &amp; Cong ty 123".
    ///   - Giữ nguyên dấu nháy đơn (') và ký tự &amp;.
    /// Expected cố định từ log thực tế: "O'Brien &amp; Cong ty 123".
    /// Không còn phụ thuộc vào SimulateDolibarrSanitize() làm oracle.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Security")]
    [Description("TC_CRM_010 — Tạo KH với ký tự đặc biệt: Dolibarr sanitize server-side, lưu 'O'Brien & Cong ty 123'.")]
    public void TC_CRM_010_CreateCustomer_SpecialChars_ObserveXssHandling()
    {
        _createdCustomerUrl = null;
        try
        {
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_010", TestConfig.ExcelPath);
            string specialName = row["Test data"];

            // Expected cố định từ log thực tế 2026-10-03:
            // Dolibarr xóa <Test> và "123", giữ ' và & → kết quả "O'Brien & Cong ty 123"
            const string ExpectedSavedName = "O'Brien & Cong ty 123";

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            createPage.SelectCustomerType();
            createPage.EnterName(specialName);

            createPage.ClickSave();

            bool redirected = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;

            Assert.IsTrue(redirected,
                $"[TC_CRM_010] Form phải lưu được và redirect về trang chi tiết. URL: {Driver.Url}");

            // Assert 1: Kiểm tra XSS — <Test> không được xuất hiện unescaped trong HTML
            string containerHtml = detailPage.GetCustomerNameContainerHtml();
            Assert.IsFalse(containerHtml.Contains("<Test>", StringComparison.OrdinalIgnoreCase),
                "[TC_CRM_010] CANH BAO XSS: the <Test> xuat hien khong escape trong HTML container!");

            // Assert 2 (Rule độc lập): Tên đã lưu KHÔNG chứa < > "
            string actualName = detailPage.GetDisplayedName();
            TestContext.WriteLine($"[TC_CRM_010] Actual name: '{actualName}' (expected: '{ExpectedSavedName}')");
            Assert.AreEqual(-1, actualName.IndexOfAny(new[] { '<', '>', '"' }),
                $"[TC_CRM_010] Ten da luu KHONG duoc chua ky tu '<', '>', '\"'. Thuc te: '{actualName}'");

            // Assert 3: Tên đã lưu phải giữ dấu nháy đơn và &
            Assert.IsTrue(actualName.Contains('\''),
                $"[TC_CRM_010] Ten phai giu nguyen ky tu nháy đơn ('). Thuc te: '{actualName}'");
            Assert.IsTrue(actualName.Contains('&'),
                $"[TC_CRM_010] Ten phai giu nguyen ky tu '&'. Thuc te: '{actualName}'");

            // Assert 4: So khớp với expected cố định từ log thực tế
            Assert.AreEqual(ExpectedSavedName, actualName,
                $"[TC_CRM_010] Ten sau sanitize phai khop chinh xac gia tri quan sat tu log 2026-10-03.\n" +
                $"Expected: '{ExpectedSavedName}'\n" +
                $"Actual:   '{actualName}'");

            TestContext.WriteLine($"[TC_CRM_010 PASS] Luu va hien thi dung: '{actualName}'. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_010");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }


    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_011 — Tiếng Việt có dấu 128 ký tự (Unicode BVA)
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_011: Tạo KH với chuỗi 128 ký tự tiếng Việt có dấu (Unicode multi-byte).
    /// Đọc trực tiếp từ dòng 8 cột Test data của sheet Test Cases.
    /// Assert: Khớp chính xác từng ký tự (không chỉ độ dài) để phát hiện lỗi mã hóa (mojibake).
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Unicode")]
    [Description("TC_CRM_011 — Tạo KH với chuỗi 128 ký tự tiếng Việt có dấu: kiểm tra xử lý multi-byte Unicode.")]
    public void TC_CRM_011_CreateCustomer_VietnameseDiacritics128Chars_ShouldSaveSuccessfully()
    {
        _createdCustomerUrl = null;
        try
        {
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_011", TestConfig.ExcelPath, preferredSheet: "Test Cases");
            string vietnameseName = row["Test data"];

            // 1. Assert chuỗi đọc từ Excel có độ dài 128 và có ít nhất 1 ký tự ord > 127
            Assert.AreEqual(128, vietnameseName.Length,
                $"[TC_CRM_011 Arrange] Chuỗi từ Excel phải có đúng 128 ký tự, thực tế: {vietnameseName.Length}");
            Assert.IsTrue(vietnameseName.Any(c => c > 127),
                "[TC_CRM_011 Arrange] Chuỗi đọc từ Excel không có ký tự có dấu (>127) — vi phạm mục đích test Unicode!");

            TestContext.WriteLine($"[TC_CRM_011] Đã đọc chuỗi tiếng Việt 128 ký tự từ Excel: '{vietnameseName}'");
            TestContext.WriteLine($"[TC_CRM_011] Số ký tự non-ASCII (>127): {vietnameseName.Count(c => c > 127)}");

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            createPage.SelectCustomerType();
            createPage.EnterName(vietnameseName);

            // 2. Assert giá trị ô nhập SAU KHI nhập: độ dài == 128 VÀ bằng chính xác chuỗi gốc
            string actualInInput = createPage.GetNameInputValue();
            Assert.AreEqual(128, actualInInput.Length,
                $"[TC_CRM_011] Ô nhập phải chứa đúng 128 ký tự, thực tế: {actualInInput.Length}");
            Assert.AreEqual(vietnameseName, actualInInput,
                "[TC_CRM_011] Giá trị ô nhập phải khớp chính xác từng ký tự chuỗi tiếng Việt có dấu gốc.");

            // 3. Submit
            createPage.ClickSave();

            // 4. Assert redirect về trang chi tiết
            bool redirected = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;

            Assert.IsTrue(redirected,
                $"[TC_CRM_011] Form phải lưu thành công và redirect về trang chi tiết. URL: {Driver.Url}");

            string errMsg = createPage.GetErrorMessage();
            Assert.AreEqual(string.Empty, errMsg,
                $"[TC_CRM_011] Không được có thông báo lỗi. Nhận được: '{errMsg}'");

            // 5. Assert tên hiển thị trên trang chi tiết khớp chính xác chuỗi gốc (dùng HtmlDecode nếu cần)
            string displayedName = detailPage.GetDisplayedName();
            string decodedDisplayedName = WebUtility.HtmlDecode(displayedName);

            Assert.AreEqual(vietnameseName, decodedDisplayedName,
                $"[TC_CRM_011] Tên hiển thị trên trang chi tiết phải khớp chính xác 100% chuỗi tiếng Việt gốc (không bị mojibake/lỗi font).\n" +
                $"Gốc:     '{vietnameseName}'\n" +
                $"Hiển thị: '{decodedDisplayedName}'");

            TestContext.WriteLine(
                $"[TC_CRM_011 PASS] Tên tiếng Việt 128 ký tự lưu và hiển thị hoàn hảo. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_011");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ============================================================
    // CLEANUP SCRIPT — Xoa du lieu rac tich luy
    // ============================================================

    // Prefix whitelist: chi xoa KH co ten bat dau bang mot trong cac chuoi nay.
    // Bao ve du lieu nen: khong xoa KH khong ro nguon goc.
    private static readonly string[] TestNamePrefixes =
    [
        "AUTO_", "KH_Edit_", "SearchFull_", "SearchPart_", "Delete_",
        "TC005_", "TC006_", "TC_"
    ];

    // CLEANUP_DRY_RUN: mac dinh TRUE — chi in log, KHONG xoa.
    // Dat false khi muon xoa that: CLEANUP_DRY_RUN=false dotnet test --filter TestCategory=Cleanup
    private static readonly bool CleanupDryRun =
        !string.Equals(Environment.GetEnvironmentVariable("CLEANUP_DRY_RUN"), "false",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsTestCustomerName(string name) =>
        TestNamePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    /// <summary>
    /// Cleanup_DeleteAllTestCustomers:
    /// Chi xoa KH co ten khop prefix whitelist (AUTO_, KH_Edit_, SearchFull_, ...).
    /// Mac dinh chay DRY-RUN (chi in danh sach, khong xoa).
    /// De xoa that: dat bien moi truong CLEANUP_DRY_RUN=false truoc khi chay.
    /// Guard: thoat neu 3 vong lien tiep khong xoa duoc them KH nao (tranh loop vo han).
    /// </summary>
    [TestMethod]
    [TestCategory("Cleanup")]
    [Description("Don sach KH test: chi xoa KH co ten bat dau bang prefix whitelist (xem TestNamePrefixes). Mac dinh DRY-RUN.")]
    public void Cleanup_DeleteAllTestCustomers()
    {
        Login();

        if (CleanupDryRun)
            TestContext.WriteLine("[Cleanup] CHE DO DRY-RUN: chi liet ke, KHONG XOA. De xoa that: dat CLEANUP_DRY_RUN=false.");
        else
            TestContext.WriteLine("[Cleanup] CHE DO THAT: se xoa cac KH khop prefix.");

        var detailPage = new CustomerDetailPage(Driver);
        int totalDeleted = 0;
        int totalFailed = 0;
        int staleRounds = 0;      // guard chong loop vo han
        const int MaxStaleRounds = 3;
        const int PageSize = 100;
        int prevDeletedBeforeRound = -1;

        bool foundMore = true;
        while (foundMore)
        {
            // Guard: neu 3 vong lien tiep khong xoa them duoc KH nao, thoat
            if (totalDeleted == prevDeletedBeforeRound)
            {
                staleRounds++;
                if (staleRounds >= MaxStaleRounds)
                {
                    TestContext.WriteLine(
                        $"[Cleanup WARN] {MaxStaleRounds} vong lien tiep khong xoa them duoc KH nao. Thoat de tranh loop.");
                    break;
                }
            }
            else
            {
                staleRounds = 0;
            }
            prevDeletedBeforeRound = totalDeleted;

            Driver.Navigate().GoToUrl(
                $"{TestConfig.BaseUrl}/societe/list.php?type=c&limit={PageSize}");
            WaitHelper.WaitVisible(Driver, By.Name("search_nom"), timeoutSeconds: 15);

            var customerLinks = Driver.FindElements(
                By.CssSelector("table.tagtable td a[href*='societe/card.php?socid=']"));

            var testCustomerUrls = new List<(string Url, string Name)>();
            foreach (var link in customerLinks)
            {
                string url = link.GetAttribute("href") ?? string.Empty;
                string name = link.Text.Trim();

                // Whitelist bao ve: KHONG xoa du lieu nen
                if (url.Contains("socid=1&") || url.EndsWith("socid=1") ||
                    url.Contains("socid=2&") || url.EndsWith("socid=2") ||
                    name.Equals("Cong ty ABC", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Cong ty BCD", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Chi them KH co ten khop prefix whitelist — an toan hon truoc
                if (!IsTestCustomerName(name))
                {
                    TestContext.WriteLine($"[Cleanup SKIP] Bo qua KH khong ro nguon goc: '{name}' ({url})");
                    continue;
                }

                if (!string.IsNullOrEmpty(url))
                    testCustomerUrls.Add((url, name));
            }

            if (testCustomerUrls.Count == 0)
            {
                foundMore = false;
                break;
            }

            TestContext.WriteLine(
                $"[Cleanup] Tim thay {testCustomerUrls.Count} KH test khop prefix tren trang hien tai.");

            foreach (var (url, name) in testCustomerUrls)
            {
                if (CleanupDryRun)
                {
                    TestContext.WriteLine($"[Cleanup DRY-RUN] Se xoa: '{name}' ({url})");
                    continue; // Khong xoa that
                }

                try
                {
                    Driver.Navigate().GoToUrl(url);
                    bool deleted = detailPage.DeleteCustomer(timeoutSeconds: 15);
                    if (deleted)
                    {
                        totalDeleted++;
                        TestContext.WriteLine($"[Cleanup] Da xoa #{totalDeleted}: '{name}'");
                    }
                    else
                    {
                        totalFailed++;
                        TestContext.WriteLine($"[Cleanup WARN] Khong xoa duoc: '{name}' ({url})");
                    }
                }
                catch (Exception ex)
                {
                    totalFailed++;
                    TestContext.WriteLine($"[Cleanup WARN] Exception khi xoa '{name}': {ex.Message}");
                }
            }

            if (CleanupDryRun)
            {
                // DRY-RUN: chi quet 1 trang roi thoat (khong can lap)
                foundMore = false;
            }
            else if (testCustomerUrls.Count < PageSize)
            {
                foundMore = false;
            }
        }

        if (CleanupDryRun)
        {
            TestContext.WriteLine("[Cleanup DRY-RUN DONE] Danh sach tren la nhung KH SE BI XOA khi chay voi CLEANUP_DRY_RUN=false.");
            TestContext.WriteLine("DUNG LAI — xac nhan voi nguoi dung truoc khi xoa that.");
            // DRY-RUN luon PASS — khong assert gi ca
            return;
        }

        TestContext.WriteLine(
            $"[Cleanup DONE] Tong xoa thanh cong: {totalDeleted}, That bai: {totalFailed}");

        // Xac minh lai: chi con du lieu nen
        Driver.Navigate().GoToUrl(
            $"{TestConfig.BaseUrl}/societe/list.php?type=c&limit={PageSize}");
        var remainingLinks = Driver.FindElements(
            By.CssSelector("table.tagtable td a[href*='societe/card.php?socid=']"));
        var remainingNames = remainingLinks
            .Select(l => l.Text.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        TestContext.WriteLine(
            $"[Cleanup Verify] Con lai ({remainingNames.Count}): {string.Join(", ", remainingNames)}");

        // [FIX] Assert chat hon: khong duoc co bat ky that bai nao
        Assert.AreEqual(0, totalFailed,
            $"[Cleanup] Co {totalFailed} KH KHONG xoa duoc. Kiem tra log phia tren de biet chi tiet.");
    }

    // ============================================================
    // TC_CRM_012 -- Sua ten khach hang (Update flow)
    // ============================================================
    /// <summary>
    /// TC_CRM_012: Tao KH moi -> vao trang chi tiet -> click Edit -> sua ten -> luu -> assert ten moi hien thi dung.
    /// Dung lai CustomerDetailPage.ClickEdit(), EnterName(), ClickSave(), GetDisplayedName().
    /// Cleanup: xoa KH vua tao sau khi ket thuc.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Update")]
    [Description("TC_CRM_012 -- Sua ten KH: tao KH -> edit -> luu -> assert ten moi hien thi dung.")]
    public void TC_CRM_012_UpdateCustomer_ChangeName_ShouldDisplayNewName()
    {
        _createdCustomerUrl = null;
        try
        {
            // Arrange
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpper();
            // Prefix AUTO_ de Cleanup loc duoc chinh xac
            string originalName = $"AUTO_Edit_Orig_{suffix}";
            string newName = $"AUTO_Edit_New_{suffix}";

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            // Act 1: Tao KH ban dau
            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "[TC_CRM_012] Phai dieu huong duoc den trang tao KH moi.");
            createPage.SelectCustomerType();
            createPage.EnterName(originalName);
            createPage.ClickSave();

            bool created = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;
            Assert.IsTrue(created, $"[TC_CRM_012] Phai tao duoc KH '{originalName}'. URL: {Driver.Url}");

            string displayedOriginal = detailPage.GetDisplayedName();
            Assert.AreEqual(originalName, displayedOriginal,
                $"[TC_CRM_012] Ten hien thi sau khi tao phai la '{originalName}', thuc te: '{displayedOriginal}'");

            TestContext.WriteLine($"[TC_CRM_012] Da tao KH: '{originalName}'. URL: {Driver.Url}");

            // Act 2: Click nut Edit (Sua)
            detailPage.ClickEdit();
            TestContext.WriteLine($"[TC_CRM_012] Da vao che do Edit. URL: {Driver.Url}");

            // Assert: o nhap ten hien thi dung ten cu
            string nameInEditBox = detailPage.GetNameInputValue();
            Assert.AreEqual(originalName, nameInEditBox,
                $"[TC_CRM_012] O nhap ten trong Edit phai chua ten cu '{originalName}', thuc te: '{nameInEditBox}'");

            // Act 3: Sua ten moi
            detailPage.EnterName(newName);

            string nameAfterInput = detailPage.GetNameInputValue();
            Assert.AreEqual(newName, nameAfterInput,
                $"[TC_CRM_012] O nhap phai chua ten moi '{newName}' sau khi sua, thuc te: '{nameAfterInput}'");

            detailPage.ClickSave();

            // Act 4: Cho redirect ve trang chi tiet (khong con action=edit)
            var waitEdit = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
            bool savedOk = waitEdit.Until(d => CustomerDetailPage.IsCustomerCreatedSuccessfully(d.Url)
                                            && !d.Url.Contains("action=edit", StringComparison.OrdinalIgnoreCase));
            _createdCustomerUrl = Driver.Url;
            Assert.IsTrue(savedOk, $"[TC_CRM_012] Sau khi sua ten, phai redirect ve trang chi tiet. URL: {Driver.Url}");

            // Assert chinh: ten moi hien thi dung
            string displayedNew = detailPage.GetDisplayedName();
            Assert.AreEqual(newName, displayedNew,
                $"[TC_CRM_012] Ten hien thi phai la ten moi '{newName}', thuc te: '{displayedNew}'");

            TestContext.WriteLine($"[TC_CRM_012 PASS] Sua ten thanh cong: '{originalName}' -> '{displayedNew}'. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_012");
            if (screenshotPath != null) TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ============================================================
    // TC_CRM_013 -- Tim kiem KH theo ten day du
    // ============================================================
    /// <summary>
    /// TC_CRM_013: Tao KH moi -> tim kiem theo ten day du -> assert 1 ket qua tra ve, ten khop.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Search")]
    [Description("TC_CRM_013 -- Tim kiem KH theo ten day du: phai tra ve dung 1 KH khop ten.")]
    public void TC_CRM_013_SearchCustomer_ByFullName_ShouldReturnExactMatch()
    {
        _createdCustomerUrl = null;
        try
        {
            // Arrange
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpper();
            // Prefix AUTO_ de Cleanup loc duoc chinh xac
            string searchName = $"AUTO_SearchFull_{suffix}";

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);
            var listPage = new CustomerListPage(Driver);

            // Tao KH
            createPage.GoTo();
            createPage.SelectCustomerType();
            createPage.EnterName(searchName);
            createPage.ClickSave();
            bool created = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;
            Assert.IsTrue(created, $"[TC_CRM_013] Phai tao duoc KH '{searchName}'.");

            TestContext.WriteLine($"[TC_CRM_013] Da tao KH: '{searchName}'");

            // Act: Tim kiem theo ten day du
            listPage.GoTo();
            listPage.SearchByName(searchName);
            TestContext.WriteLine($"[TC_CRM_013] Da tim kiem: '{searchName}'. URL: {listPage.GetCurrentUrl()}");

            // Assert 1: So luong ket qua phai dung 1 (hoac >= 1)
            int count = listPage.GetResultCount();
            var resultNames = listPage.GetResultNames();
            TestContext.WriteLine($"[TC_CRM_013] Count={count}, Ket qua={string.Join(", ", resultNames)}");

            Assert.AreEqual(1, count,
                $"[TC_CRM_013] Phai tra ve dung 1 ket qua khi tim theo ten day du '{searchName}'. Tim thay: {count}");

            // Assert 2: Danh sach ten phai chua ten da tim kiem
            bool found = resultNames.Any(n => n.Equals(searchName, StringComparison.OrdinalIgnoreCase)
                                           || n.Contains(searchName, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(found,
                $"[TC_CRM_013] Danh sach ket qua phai chua ten '{searchName}'. " +
                $"Ket qua thuc te: [{string.Join(", ", resultNames)}]");

            TestContext.WriteLine($"[TC_CRM_013 PASS] Tim kiem theo ten day du thanh cong. Count={count}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_013");
            if (screenshotPath != null) TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ============================================================
    // TC_CRM_014 -- Tim kiem KH theo mot phan ten
    // ============================================================
    /// <summary>
    /// TC_CRM_014: Tao KH moi -> tim kiem theo mot phan ten -> assert co ket qua chua phan ten do.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Search")]
    [Description("TC_CRM_014 -- Tim kiem KH theo mot phan ten: phai tra ve KH khop.")]
    public void TC_CRM_014_SearchCustomer_ByPartialName_ShouldReturnMatchingResults()
    {
        _createdCustomerUrl = null;
        try
        {
            // Arrange
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpper();
            // Prefix AUTO_ de Cleanup loc duoc chinh xac
            string fullName = $"AUTO_SearchPart_{suffix}";
            // Dung suffix GUID lam search term — dam bao duy nhat trong DB
            string partialName = suffix; // Phan suffix la duy nhat

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);
            var listPage = new CustomerListPage(Driver);

            // Tao KH
            createPage.GoTo();
            createPage.SelectCustomerType();
            createPage.EnterName(fullName);
            createPage.ClickSave();
            bool created = detailPage.WaitForRedirectAfterSave();
            _createdCustomerUrl = Driver.Url;
            Assert.IsTrue(created, $"[TC_CRM_014] Phai tao duoc KH '{fullName}'.");

            TestContext.WriteLine($"[TC_CRM_014] Da tao KH: '{fullName}'. Tim kiem theo: '{partialName}'");

            // Act: Tim kiem theo mot phan ten
            listPage.GoTo();
            listPage.SearchByName(partialName);
            TestContext.WriteLine($"[TC_CRM_014] Da tim kiem: '{partialName}'");

            // Assert 1: Co ket qua
            int count = listPage.GetResultCount();
            TestContext.WriteLine($"[TC_CRM_014] Count={count}, Tim kiem theo='{partialName}'");
            Assert.IsTrue(count >= 1,
                $"[TC_CRM_014] Phai co it nhat 1 ket qua khi tim phan '{partialName}'. Tim thay: {count}");

            // Assert 2: Ket qua chua ten KH vua tao
            var resultNames = listPage.GetResultNames();
            TestContext.WriteLine($"[TC_CRM_014] Ket qua: {string.Join(", ", resultNames)}");
            bool found = resultNames.Any(n => n.Contains(partialName, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(found,
                $"[TC_CRM_014] Ket qua tim kiem phai chua phan '{partialName}'. " +
                $"Ket qua: [{string.Join(", ", resultNames)}]");

            TestContext.WriteLine($"[TC_CRM_014 PASS] Tim kiem mot phan ten thanh cong. Count={count}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_014");
            if (screenshotPath != null) TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ============================================================
    // TC_CRM_015 -- Tim kiem ten khong ton tai
    // ============================================================
    /// <summary>
    /// TC_CRM_015: Tim kiem ten ngau nhien khong ton tai -> assert 0 ket qua / thong bao rong.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Search")]
    [Description("TC_CRM_015 -- Tim kiem ten khong ton tai: phai tra ve 0 ket qua.")]
    public void TC_CRM_015_SearchCustomer_NonExistentName_ShouldReturnNoResults()
    {
        _createdCustomerUrl = null;
        try
        {
            // Arrange: ten ngu nhien chac chan khong ton tai trong DB
            string nonExistentName = $"NOTEXIST_{Guid.NewGuid().ToString("N")[..12].ToUpper()}_ZZZZZ";

            Login();
            var listPage = new CustomerListPage(Driver);

            // Act: Tim kiem
            listPage.GoTo();
            listPage.SearchByName(nonExistentName);
            TestContext.WriteLine($"[TC_CRM_015] Da tim kiem: '{nonExistentName}'");

            // Assert: 0 ket qua
            int count = listPage.GetResultCount();
            bool hasNoResult = listPage.HasNoResultMessage();
            TestContext.WriteLine($"[TC_CRM_015] Count={count}, HasNoResultMessage={hasNoResult}");

            Assert.IsTrue(count == 0 && hasNoResult,
                // AND thay vi OR: log thuc te 2026-10-03 xac nhan ca 2 dieu kien deu dung.
                // Neu chi 1 trong 2 la True, chi co the la loi UI (GetResultCount != HasNoResultMessage).
                $"[TC_CRM_015] Tim kiem ten khong ton tai phai tra ve 0 ket qua VA co thong bao rong. " +
                $"Thuc te Count={count}, HasNoResult={hasNoResult}. URL: {listPage.GetCurrentUrl()}");

            TestContext.WriteLine($"[TC_CRM_015 PASS] Tim kiem ten khong ton tai tra ve rong. Count={count}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_015");
            if (screenshotPath != null) TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    // ============================================================
    // TC_CRM_016 -- Xoa khach hang
    // ============================================================
    /// <summary>
    /// TC_CRM_016: Tao KH moi -> click Xoa -> xac nhan popup -> assert KH da bi xoa (tim lai = 0 ket qua).
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Delete")]
    [Description("TC_CRM_016 -- Xoa khach hang: xoa thanh cong va khong con xuat hien trong danh sach.")]
    public void TC_CRM_016_DeleteCustomer_ShouldRemoveFromList()
    {
        _createdCustomerUrl = null;
        try
        {
            // Arrange: Tao KH moi de xoa
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpper();
            // Prefix AUTO_ de Cleanup loc duoc chinh xac
            string customerName = $"AUTO_Delete_{suffix}";

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);
            var listPage = new CustomerListPage(Driver);

            createPage.GoTo();
            createPage.SelectCustomerType();
            createPage.EnterName(customerName);
            createPage.ClickSave();
            bool created = detailPage.WaitForRedirectAfterSave();
            // [FIX Bug#1] Gán _createdCustomerUrl ngay sau tao thanh cong
            // → nếu DeleteCustomer() fail, finally sẽ CleanupCreatedCustomer() được
            _createdCustomerUrl = Driver.Url;
            Assert.IsTrue(created, $"[TC_CRM_016] Phai tao duoc KH '{customerName}' truoc khi xoa.");
            TestContext.WriteLine($"[TC_CRM_016] Da tao KH: '{customerName}', URL={_createdCustomerUrl}");

            // Act: Thuc hien xoa KH
            bool deleted = detailPage.DeleteCustomer(timeoutSeconds: 15);
            Assert.IsTrue(deleted, $"[TC_CRM_016] Thao tac xoa KH '{customerName}' phai thanh cong.");
            // Xoa thanh cong → không cần cleanup nữa
            _createdCustomerUrl = null;
            TestContext.WriteLine($"[TC_CRM_016] Da thuc hien xoa KH thanh cong.");

            // Assert: Tim lai khach hang vua xoa -> phai tra ve 0 ket qua
            listPage.GoTo();
            listPage.SearchByName(customerName);
            int count = listPage.GetResultCount();
            bool hasNoResult = listPage.HasNoResultMessage();
            TestContext.WriteLine($"[TC_CRM_016] Count={count}, HasNoResult={hasNoResult} sau khi xóa");

            Assert.IsTrue(count == 0 && hasNoResult,
                // AND thay vi OR: log thuc te 2026-10-03 xac nhan ca 2 dieu kien deu dung sau khi xoa.
                // Neu chi 1 trong 2 la True, can kiem tra lai GetResultCount() hoac HasNoResultMessage().
                $"[TC_CRM_016] KH '{customerName}' da xoa nhung van tim thay trong danh sach. Count={count}, HasNoResult={hasNoResult}");

            TestContext.WriteLine($"[TC_CRM_016 PASS] Xoa KH thanh cong va xac minh khong con ton tai trong danh sach.");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_016");
            if (screenshotPath != null) TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }
}

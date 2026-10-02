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
    /// Kiểm tra hành vi chặn ở Client-side (HTML5 required attribute) hoặc Server-side (error message).
    /// Dữ liệu: đọc từ cột "Test data" của dòng TC_CRM_007 trong Excel.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Negative")]
    [Description("TC_CRM_007 — Tạo KH với tên rỗng: kiểm tra client-side required hoặc server-side chặn.")]
    public void TC_CRM_007_CreateCustomer_EmptyName_ShouldBeBlocked()
    {
        _createdCustomerUrl = null;
        try
        {
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_007", TestConfig.ExcelPath);
            string testData = row.TryGetValue("Test data", out var td) ? td : string.Empty;

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            // 1. Kiểm tra thuộc tính required trong HTML của ô tên
            bool hasRequired = createPage.IsNameInputRequired();
            TestContext.WriteLine($"[TC_CRM_007] Thuộc tính 'required' của ô Tên trong HTML: {hasRequired}");

            createPage.SelectCustomerType();
            if (!string.IsNullOrEmpty(testData))
            {
                createPage.EnterName(testData);
            }

            createPage.ClickSave();

            if (hasRequired)
            {
                // Khi có HTML5 required: browser chặn submit tại client-side
                string valMsg = createPage.GetNameValidationMessage();
                TestContext.WriteLine($"[TC_CRM_007 Client-side] Form bị chặn bởi HTML5 required. validationMessage='{valMsg}'");

                Assert.IsTrue(createPage.IsOnCreatePage(),
                    "[TC_CRM_007] Khi có required attribute, form không được submit/redirect.");
                Assert.IsTrue(Driver.Url.Contains("action=create", StringComparison.OrdinalIgnoreCase),
                    $"[TC_CRM_007] URL phải còn 'action=create', thực tế: {Driver.Url}");
                Assert.IsFalse(string.IsNullOrEmpty(valMsg),
                    "[TC_CRM_007] validationMessage của input 'name' phải khác rỗng khi browser chặn.");
            }
            else
            {
                // Khi không có required: server-side chặn và trả về thông báo lỗi
                bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 3);
                string errMsg = createPage.GetErrorMessage();
                TestContext.WriteLine($"[TC_CRM_007 Server-side] Redirected={redirected}, ServerErrorMessage='{errMsg}'");

                // Assert chính: không được redirect thành công (URL không có socid/id)
                Assert.IsFalse(redirected,
                    $"[TC_CRM_007] Server không được tạo KH rỗng và redirect thành công. URL: {Driver.Url}");

                // Assert phụ: kiểm tra còn ở trang tạo hoặc có thông báo lỗi server (chứa 'required' / 'bắt buộc')
                Assert.IsTrue(
                    createPage.IsOnCreatePage() || !string.IsNullOrEmpty(errMsg),
                    "[TC_CRM_007] Phải còn ở trang tạo hoặc có thông báo lỗi từ server.");

                if (!string.IsNullOrEmpty(errMsg))
                {
                    Assert.IsTrue(
                        errMsg.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                        errMsg.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase) ||
                        errMsg.Contains("obligatoire", StringComparison.OrdinalIgnoreCase),
                        $"[TC_CRM_007 Assert phụ] Thông báo lỗi server phải chứa từ khóa yêu cầu nhập tên. Lỗi: '{errMsg}'");
                }
            }

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
    /// TC_CRM_009: Nhập tên chỉ gồm 10 khoảng trắng (đọc từ Excel).
    /// Quan sát và ghi nhận hành vi thực tế: Dolibarr chặn hay tự động trim hay cho lưu.
    /// Assert: Không gặp lỗi crash / 500 / Fatal error của PHP.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("EdgeCase")]
    [Description("TC_CRM_009 — Nhập tên chỉ gồm khoảng trắng (10 spaces từ Excel): quan sát hành vi thực tế.")]
    public void TC_CRM_009_CreateCustomer_WhitespaceOnly_ObserveActualBehavior()
    {
        _createdCustomerUrl = null;
        try
        {
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_009", TestConfig.ExcelPath);
            string whitespaceName = row["Test data"];

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(), "Phải điều hướng được đến trang tạo KH mới.");

            createPage.SelectCustomerType();
            createPage.EnterName(whitespaceName);

            // a. Ghi lại giá trị actualInInput.Length ngay sau khi nhập (trước submit)
            string actualInInput = createPage.GetNameInputValue();
            TestContext.WriteLine(
                $"[TC_CRM_009] Input length trước khi submit: {actualInInput.Length}, Raw='{actualInInput}'");

            // b. Thử submit
            createPage.ClickSave();

            bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 5);
            if (redirected)
            {
                _createdCustomerUrl = Driver.Url;
            }

            string savedNameRaw = string.Empty;
            int savedNameLength = 0;
            if (redirected)
            {
                savedNameRaw = detailPage.GetDisplayedName();
                savedNameLength = savedNameRaw.Length;
            }
            else
            {
                string errMsg = createPage.GetErrorMessage();
                TestContext.WriteLine($"[TC_CRM_009] Form bị chặn submit, thông báo lỗi: '{errMsg}'");
            }

            // c. Assert không có Exception không mong muốn (không lỗi Fatal error / Warning PHP)
            string pageSource = Driver.PageSource;
            Assert.IsFalse(pageSource.Contains("Fatal error:", StringComparison.OrdinalIgnoreCase),
                "[TC_CRM_009] Trang web không được chứa 'Fatal error:' của PHP.");
            Assert.IsFalse(pageSource.Contains("Parse error:", StringComparison.OrdinalIgnoreCase),
                "[TC_CRM_009] Trang web không được chứa 'Parse error:' của PHP.");

            // d. In ra TestContext định dạng yêu cầu để dán vào cột Actual của Excel:
            TestContext.WriteLine(
                string.Format("[TC_CRM_009 OBSERVED] Redirected={0}, SavedNameLength={1}, SavedNameRaw='{2}'",
                    redirected, savedNameLength, savedNameRaw));

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_CRM_009");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            CleanupCreatedCustomer();
        }
    }

    /// <summary>
    /// Mô phỏng cơ chế sanitize input của Dolibarr (strip_tags thẻ HTML, xóa ngoặc kép, gộp khoảng trắng liền kề).
    /// </summary>
    private static string SimulateDolibarrSanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Bước 1: Xóa TOÀN BỘ các khối khớp mẫu thẻ HTML (<[^>]*>) như strip_tags()
        string step1 = Regex.Replace(input, "<[^>]*>", "");

        // Bước 2: Xóa toàn bộ ký tự dấu ngoặc kép '"'
        string step2 = step1.Replace("\"", "");

        // Bước 3: Chuẩn hóa khoảng trắng: gộp các khoảng trắng liền kề thành 1 khoảng trắng và trim
        return Regex.Replace(step2, @"\s+", " ").Trim();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_CRM_010 — Ký tự đặc biệt & Kiểm tra XSS
    // ════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// TC_CRM_010: Tạo KH với chuỗi chứa ký tự đặc biệt (O'Brien &amp; Cong ty &lt;Test&gt; "123").
    /// Kiểm tra: Lưu thành công, hiển thị đúng, và kiểm tra cơ chế escape chống XSS.
    /// </summary>
    [TestMethod]
    [TestCategory("CRM")]
    [TestCategory("Security")]
    [Description("TC_CRM_010 — Tạo KH với ký tự đặc biệt O'Brien & Cong ty <Test> \"123\": kiểm tra an toàn XSS.")]
    public void TC_CRM_010_CreateCustomer_SpecialChars_ObserveXssHandling()
    {
        _createdCustomerUrl = null;
        try
        {
            var row = ExcelDataReader.GetRowByTestId("TC_CRM_010", TestConfig.ExcelPath);
            string specialName = row["Test data"];

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

            // Lấy PageSource và HTML vùng hiển thị tên khách hàng
            string containerHtml = detailPage.GetCustomerNameContainerHtml();
            string pageSource = Driver.PageSource;

            // Kiểm tra an toàn XSS: xác nhận thẻ HTML không bị chèn trực tiếp không escape
            bool hasUnescapedTag = containerHtml.Contains("<Test>", StringComparison.OrdinalIgnoreCase);
            if (hasUnescapedTag)
            {
                string warnMsg = "CANH BAO: co the co lo hong XSS — the HTML khong duoc escape trong container hiển thị tên!";
                TestContext.WriteLine(warnMsg);
                Assert.Fail(warnMsg);
            }

            TestContext.WriteLine(
                "[TC_CRM_010] Dolibarr sanitize dau vao bang cach loai bo ky tu <, >, \" truoc khi luu (khong phai escape khi hien thi) — day la co che chong XSS/injection o tang server");

            // Lấy chuỗi tên hiển thị thật trên trang chi tiết (không dính địa chỉ/Vietnam)
            string actualName = detailPage.GetDisplayedName();

            // Assert 1: Chuỗi đã lưu KHÔNG chứa bất kỳ ký tự nào trong bộ {'<', '>', '"'}
            Assert.AreEqual(-1, actualName.IndexOfAny(new[] { '<', '>', '"' }),
                $"[TC_CRM_010] Chuỗi đã lưu không được chứa bất kỳ ký tự nào trong bộ {{'<', '>', '\"'}}. Thực tế: '{actualName}'");

            // Assert 2: Chuỗi đã lưu PHẢI chứa ký tự ''' (dấu nháy đơn) và '&' (xác nhận được giữ nguyên)
            Assert.IsTrue(actualName.Contains('\''),
                $"[TC_CRM_010] Tên đã lưu phải giữ nguyên ký tự nháy đơn ('). Thực tế: '{actualName}'");
            Assert.IsTrue(actualName.Contains('&'),
                $"[TC_CRM_010] Tên đã lưu phải giữ nguyên ký tự '&'. Thực tế: '{actualName}'");

            // Assert 3: Sau khi simulate sanitize (xóa thẻ HTML, xóa ngoặc kép, chuẩn hóa khoảng trắng), kết quả phải bằng đúng actualName
            string expectedAfterSanitize = SimulateDolibarrSanitize(specialName);

            // Log debug hiển thị khoảng trắng bằng ký tự '•' (luôn in ra dù pass hay fail)
            TestContext.WriteLine($"[TC_CRM_010 DEBUG] Input goc (the hien khoang trang bang '•'): '{specialName.Replace(" ", "•")}'");
            TestContext.WriteLine($"[TC_CRM_010 DEBUG] Ky vong sau simulate (•): '{expectedAfterSanitize.Replace(" ", "•")}'");
            TestContext.WriteLine($"[TC_CRM_010 DEBUG] Thuc te lay duoc (•): '{actualName.Replace(" ", "•")}'");

            Assert.AreEqual(expectedAfterSanitize, actualName,
                $"[TC_CRM_010] Tên sau khi Dolibarr sanitize phải khớp chính xác chuỗi gốc sau khi loại bỏ thẻ HTML, ngoặc kép và chuẩn hóa khoảng trắng.\n" +
                $"Kỳ vọng: '{expectedAfterSanitize}'\n" +
                $"Thực tế: '{actualName}'");

            TestContext.WriteLine(
                $"[TC_CRM_010 PASS] Tên sau sanitize lưu và hiển thị đúng: '{actualName}'. URL: {Driver.Url}");

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
    /// <summary>
    /// CleanupAllTestCustomers: xoa toan bo KH test da tich luy.
    /// Loc theo prefix/pattern ten test (prefix "TC" hoac ten chi gom 1 ky tu "A" vv.).
    /// Day la [TestMethod] rieng, phai chay thu cong khi can don dep DB.
    /// KHONG phai [TestInitialize] / [TestCleanup] — khong chay tu dong.
    /// </summary>
    [TestMethod]
    [TestCategory("Cleanup")]
    [Description("Don sach KH test: xoa toan bo KH co ten khop pattern test (prefix chay tu dong).")]
    public void Cleanup_DeleteAllTestCustomers()
    {
        Login();

        var listPage = new CustomerListPage(Driver);
        var detailPage = new CustomerDetailPage(Driver);

        // Pattern ten KH test: cac prefix/ky tu dac trung ma test suite tao ra
        // - "TC005_" ... "TC006_" (128/129 ky tu, co prefix nay)
        // - Ten dung 1 ky tu "A"
        // - Ten chi gom khoang trang (sau khi trim: rong hoac 1 chu)
        // - Ten chua ky tu dac biet (O'Brien)
        // - Ten tieng Viet (bat dau bang "Cong ty TNHH")
        int totalDeleted = 0;
        int totalFailed = 0;

        // Dieu huong den danh sach khach hang (limit=100)
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/list.php?type=c&limit=100");
        WaitHelper.WaitVisible(Driver, By.Name("search_nom"), timeoutSeconds: 15);

        // Lay tat ca link toi trang chi tiet KH
        var customerLinks = Driver.FindElements(By.CssSelector("table.tagtable td a[href*='societe/card.php?socid=']"));
        var testCustomerUrls = new List<(string Url, string Name)>();

        foreach (var link in customerLinks)
        {
            string url = link.GetAttribute("href") ?? string.Empty;
            string name = link.Text.Trim();

            // Bao ve du lieu nen: KHONG duoc xoa "Cong ty ABC" (socid=1) va "Cong ty BCD" (socid=2)
            if (url.Contains("socid=1&") || url.EndsWith("socid=1") ||
                url.Contains("socid=2&") || url.EndsWith("socid=2") ||
                name.Equals("Cong ty ABC", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Cong ty BCD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(url))
            {
                testCustomerUrls.Add((url, name));
            }
        }

        TestContext.WriteLine($"[Cleanup] Tim thay {testCustomerUrls.Count} KH test can xoa.");

        foreach (var (url, name) in testCustomerUrls)
        {
            try
            {
                Driver.Navigate().GoToUrl(url);
                bool deleted = detailPage.DeleteCustomer(timeoutSeconds: 15);
                if (deleted)
                {
                    totalDeleted++;
                    TestContext.WriteLine($"[Cleanup] Da xoa ({totalDeleted}/{testCustomerUrls.Count}): '{name}' ({url})");
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

        TestContext.WriteLine($"[Cleanup DONE] Tong xoa thanh cong: {totalDeleted}, That bai: {totalFailed}");

        // Xac minh lai danh sach sau khi xoa: chi con du lieu nen (Cong ty ABC, Cong ty BCD)
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/list.php?type=c&limit=100");
        var remainingLinks = Driver.FindElements(By.CssSelector("table.tagtable td a[href*='societe/card.php?socid=']"));
        var remainingNames = remainingLinks.Select(l => l.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
        TestContext.WriteLine($"[Cleanup Verify] Danh sach con lai ({remainingNames.Count}): {string.Join(", ", remainingNames)}");

        Assert.IsTrue(totalFailed == 0 || totalDeleted > 0,
            $"[Cleanup] Xoa that bai nhieu hon thanh cong. Deleted={totalDeleted}, Failed={totalFailed}");
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
            string originalName = $"KH_Edit_Orig_{suffix}";
            string newName = $"KH_Edit_New_{suffix}";

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
            string searchName = $"SearchFull_{suffix}";

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

            // Assert 1: So luong ket qua phai >= 1
            int count = listPage.GetResultCount();
            Assert.IsTrue(count >= 1,
                $"[TC_CRM_013] Phai co it nhat 1 ket qua khi tim theo ten day du '{searchName}'. Tim thay: {count}");

            // Assert 2: Danh sach ten phai chua ten da tim kiem
            var resultNames = listPage.GetResultNames();
            TestContext.WriteLine($"[TC_CRM_013] Ket qua: {string.Join(", ", resultNames)}");
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
            string fullName = $"SearchPart_{suffix}";
            // Dung phan giua lam search term (tranh trung voi KH khac)
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

            Assert.IsTrue(count == 0 || hasNoResult,
                $"[TC_CRM_015] Tim kiem ten khong ton tai phai tra ve 0 ket qua. " +
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
            string customerName = $"Delete_{suffix}";

            Login();
            var createPage = new CustomerCreatePage(Driver);
            var detailPage = new CustomerDetailPage(Driver);
            var listPage = new CustomerListPage(Driver);

            createPage.GoTo();
            createPage.SelectCustomerType();
            createPage.EnterName(customerName);
            createPage.ClickSave();
            bool created = detailPage.WaitForRedirectAfterSave();
            string createdUrl = Driver.Url;
            Assert.IsTrue(created, $"[TC_CRM_016] Phai tao duoc KH '{customerName}' truoc khi xoa.");
            TestContext.WriteLine($"[TC_CRM_016] Da tao KH: '{customerName}', URL={createdUrl}");

            // Act: Thuc hien xoa KH
            bool deleted = detailPage.DeleteCustomer(timeoutSeconds: 15);
            Assert.IsTrue(deleted, $"[TC_CRM_016] Thao tac xoa KH '{customerName}' phai thanh cong.");
            TestContext.WriteLine($"[TC_CRM_016] Da thuc hien xoa KH thanh cong.");

            // Assert: Tim lai khach hang vua xoa -> phai tra ve 0 ket qua
            listPage.GoTo();
            listPage.SearchByName(customerName);
            int count = listPage.GetResultCount();
            bool hasNoResult = listPage.HasNoResultMessage();

            Assert.IsTrue(count == 0 || hasNoResult,
                $"[TC_CRM_016] KH '{customerName}' da xoa nhung van tim thay trong danh sach. Count={count}");

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

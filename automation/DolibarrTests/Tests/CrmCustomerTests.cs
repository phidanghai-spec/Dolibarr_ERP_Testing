using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using OpenQA.Selenium;
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
}

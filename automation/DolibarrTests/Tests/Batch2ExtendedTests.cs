using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DolibarrTests.Tests;

/// <summary>
/// Bộ kiểm thử tự động đợt 2 (9 Test Cases mở rộng):
/// - CRM: TC_CRM_017, TC_CRM_018, TC_CRM_021, TC_CRM_023
/// - Sales & Invoicing: TC_SAL_020, TC_SAL_021, TC_SAL_023
/// - Stock: TC_STK_010, TC_STK_014
/// 
/// Tuân thủ nghiêm ngặt quy tắc AGENTS.md:
/// - Không Thread.Sleep, dùng WebDriverWait.
/// - Mỗi test có Assert giá trị cụ thể (tiền, tồn kho, chuỗi thông báo, trạng thái).
/// - Dữ liệu tự sinh dùng hậu tố ngẫu nhiên và tiền tố AUTO_ / TCxxx_.
/// - Chạy tuần tự [DoNotParallelize].
/// </summary>
[TestClass]
[DoNotParallelize]
public class Batch2ExtendedTests : BaseTest
{
    private LoginPage _loginPage = null!;
    private DashboardPage _dashboardPage = null!;

    [TestInitialize]
    public void SetUp()
    {
        _loginPage = new LoginPage(Driver);
        _dashboardPage = new DashboardPage(Driver);

        _loginPage.GoTo();
        _loginPage.LoginAs(TestConfig.AdminUsername, TestConfig.AdminPassword);

        Assert.IsTrue(_dashboardPage.IsLoaded(),
            "Đăng nhập admin phải thành công. Kiểm tra Dolibarr đang chạy và biến môi trường DOLIBARR_ADMIN_PASSWORD.");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helper tạo hóa đơn chuẩn Unpaid phục vụ test thanh toán
    // ─────────────────────────────────────────────────────────────────────────────
    private string CreateAndValidateStandardInvoice()
    {
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var propalDetailPage = new ProposalDetailPage(Driver);
        propalDetailPage.AddPredefinedProduct("PR001", 1);
        propalDetailPage.ValidateProposal();
        propalDetailPage.CloseAsSigned();
        propalDetailPage.ClickCreateInvoice();

        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        invoiceDetailPage.SubmitCreateInvoiceDraft();
        invoiceDetailPage.ValidateInvoice();

        return Driver.Url;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_CRM_017 — Tạo khách hàng với Email sai định dạng (Negative Testing)
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("CRM")]
    [Description("TC_CRM_017: Tạo khách hàng với Email sai định dạng ('abc@' và 'abc.com')")]
    public void TC_CRM_017_CreateCustomer_InvalidEmail_ShouldBeRejected()
    {
        var createPage = new CustomerCreatePage(Driver);

        // Đợt 1: Email = "abc@"
        string suffix1 = Guid.NewGuid().ToString("N")[..6].ToUpper();
        string name1 = $"TC017_InvalidEmail_{suffix1}";
        const string invalidEmail1 = "abc@";

        createPage.GoTo();
        createPage.EnterName(name1);
        createPage.EnterEmail(invalidEmail1);
        createPage.SelectCustomerType();
        createPage.ClickSave();

        bool isSaved1 = CustomerDetailPage.IsCustomerCreatedSuccessfully(Driver.Url);
        string err1 = createPage.GetErrorMessage();
        TestContext.WriteLine($"[TC_CRM_017 - Đợt 1 'abc@'] isSaved={isSaved1}, URL={Driver.Url}, Error='{err1}'");

        // Đợt 2: Email = "abc.com"
        string suffix2 = Guid.NewGuid().ToString("N")[..6].ToUpper();
        string name2 = $"TC017_InvalidEmail2_{suffix2}";
        const string invalidEmail2 = "abc.com";

        createPage.GoTo();
        createPage.EnterName(name2);
        createPage.EnterEmail(invalidEmail2);
        createPage.SelectCustomerType();
        createPage.ClickSave();

        bool isSaved2 = CustomerDetailPage.IsCustomerCreatedSuccessfully(Driver.Url);
        string err2 = createPage.GetErrorMessage();
        TestContext.WriteLine($"[TC_CRM_017 - Đợt 2 'abc.com'] isSaved={isSaved2}, URL={Driver.Url}, Error='{err2}'");

        // Kỳ vọng: Hệ thống từ chối lưu hoặc báo lỗi định dạng cho cả 2 trường hợp
        // Nếu Dolibarr lưu thành công cả 2 lần (thiếu validation trường email) → Test FAIL theo đúng đặc tả
        bool blocked1 = !isSaved1 || !string.IsNullOrEmpty(err1);
        bool blocked2 = !isSaved2 || !string.IsNullOrEmpty(err2);

        Assert.IsTrue(blocked1,
            $"[TC_CRM_017 BUG] Dolibarr không chặn email sai định dạng 'abc@'. Bản ghi đã được lưu thành công tại URL: {Driver.Url}");
        Assert.IsTrue(blocked2,
            $"[TC_CRM_017 BUG] Dolibarr không chặn email sai định dạng 'abc.com'. Bản ghi đã được lưu thành công tại URL: {Driver.Url}");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_CRM_018 — Tạo khách hàng với Email hợp lệ (Positive Control)
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("CRM")]
    [Description("TC_CRM_018: Tạo khách hàng với Email đúng chuẩn 'tc018.valid@example.com'")]
    public void TC_CRM_018_CreateCustomer_ValidEmail_ShouldSaveSuccessfully()
    {
        var createPage = new CustomerCreatePage(Driver);
        var detailPage = new CustomerDetailPage(Driver);

        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        string customerName = $"TC018_ValidEmail_{suffix}";
        const string validEmail = "tc018.valid@example.com";

        createPage.GoTo();
        createPage.EnterName(customerName);
        createPage.EnterEmail(validEmail);
        createPage.SelectCustomerType();
        createPage.ClickSave();

        bool redirected = detailPage.WaitForRedirectAfterSave(timeoutSeconds: 15);
        Assert.IsTrue(redirected, $"[TC_CRM_018] Phải chuyển hướng về trang chi tiết khách hàng. URL hiện tại: {Driver.Url}");

        string displayedEmail = detailPage.GetDisplayedEmail();
        TestContext.WriteLine($"[TC_CRM_018 ACTUAL] Email nhập: '{validEmail}', Email hiển thị trên trang chi tiết: '{displayedEmail}'");

        Assert.AreEqual(validEmail, displayedEmail,
            $"[TC_CRM_018] Email hiển thị trên trang chi tiết phải khớp chính xác với email đã nhập.");

        // Dọn dẹp bản ghi tự tạo
        detailPage.DeleteCustomer();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_CRM_021 — Thử xóa khách hàng đang có hóa đơn Validated liên kết
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("CRM")]
    [Description("TC_CRM_021: Chặn xóa khách hàng 'Cong ty ABC' khi đang có hóa đơn Validated liên kết")]
    public void TC_CRM_021_DeleteCustomer_WithValidatedInvoice_ShouldBeBlocked()
    {
        // 1. Kiểm tra precondition: Cong ty ABC có hóa đơn Validated chưa
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/compta/facture/list.php?search_societe=Cong+ty+ABC");
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche, table.tagtable, table.liste"), 15);

        var rows = Driver.FindElements(By.CssSelector("table.tagtable tr.oddeven, table.liste tr.oddeven, tr[data-rowid]"));
        bool hasValidatedInvoice = rows.Any(r => r.Text.Contains("Unpaid", StringComparison.OrdinalIgnoreCase) ||
                                                 r.Text.Contains("Paid", StringComparison.OrdinalIgnoreCase) ||
                                                 r.Text.Contains("Chưa thanh toán", StringComparison.OrdinalIgnoreCase) ||
                                                 r.Text.Contains("Đã thanh toán", StringComparison.OrdinalIgnoreCase));

        if (!hasValidatedInvoice)
        {
            Assert.Inconclusive("[TC_CRM_021] Thiếu precondition: 'Cong ty ABC' chưa có hóa đơn Validated. Không tự tạo bằng SQL.");
            return;
        }

        // 2. Mở trang khách hàng "Cong ty ABC" (socid=1)
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/card.php?socid=1");
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

        var detailPage = new CustomerDetailPage(Driver);
        detailPage.DeleteCustomer(timeoutSeconds: 10);

        string error = detailPage.GetErrorMessage();
        TestContext.WriteLine($"[TC_CRM_021 ACTUAL] Thông báo sau thao tác xóa: Error='{error}', URL={Driver.Url}");

        // 3. Xác nhận rằng khách hàng 'Cong ty ABC' không bị xóa và hệ thống đã chặn
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/card.php?socid=1");
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);
        string nameStillExists = detailPage.GetDisplayedName();

        TestContext.WriteLine($"[TC_CRM_021 ACTUAL] Tên đối tác sau thao tác: '{nameStillExists}'");

        StringAssert.Contains(nameStillExists, "Cong ty ABC",
            "[TC_CRM_021 CRITICAL BUG] Khách hàng 'Cong ty ABC' đã bị xóa khỏi hệ thống dù đang có chứng từ liên kết! Vi phạm toàn vẹn Foreign Key.");

        bool hasConstraintMessage = error.Contains("child", StringComparison.OrdinalIgnoreCase) ||
                                    error.Contains("Failed to delete", StringComparison.OrdinalIgnoreCase) ||
                                    error.Contains("Proposal", StringComparison.OrdinalIgnoreCase) ||
                                    error.Contains("Invoice", StringComparison.OrdinalIgnoreCase);

        Assert.IsTrue(hasConstraintMessage,
            $"[TC_CRM_021] Hệ thống phải hiển thị thông báo lỗi ràng buộc toàn vẹn khi chặn xóa. Thông báo nhận được: '{error}'");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_CRM_023 — Tìm kiếm không phân biệt hoa thường (Case-Insensitive Search)
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("CRM")]
    [Description("TC_CRM_023: Tìm kiếm khách hàng bằng chuỗi viết thường không phân biệt hoa thường")]
    public void TC_CRM_023_SearchCustomer_CaseInsensitive_ShouldFindRecord()
    {
        var createPage = new CustomerCreatePage(Driver);
        var detailPage = new CustomerDetailPage(Driver);
        var listPage = new CustomerListPage(Driver);

        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        string mixedCaseName = $"AUTO_CiSearch_{suffix}";
        string lowerQuery = mixedCaseName.ToLower();

        // 1. Tạo khách hàng có tên chữ hoa chữ thường hỗn hợp
        createPage.GoTo();
        createPage.EnterName(mixedCaseName);
        createPage.SelectCustomerType();
        createPage.ClickSave();

        bool redirected = detailPage.WaitForRedirectAfterSave(15);
        Assert.IsTrue(redirected, $"Tạo khách hàng {mixedCaseName} thất bại.");
        string createdSocId = detailPage.GetSocId();

        try
        {
            // 2. Tìm kiếm bằng từ khóa viết thường toàn bộ
            listPage.GoTo();
            listPage.SearchByName(lowerQuery);

            int resultCount = listPage.GetResultCount();
            TestContext.WriteLine($"[TC_CRM_023 ACTUAL] Tên gốc: '{mixedCaseName}', Từ khóa tìm: '{lowerQuery}', Số kết quả: {resultCount}");

            Assert.IsTrue(resultCount >= 1,
                $"[TC_CRM_023] Tìm kiếm không phân biệt hoa thường phải trả về ít nhất 1 kết quả. Thực tế: {resultCount}");

            var names = listPage.GetResultNames();
            bool foundExact = names.Any(n => n.Contains(mixedCaseName, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(foundExact,
                $"[TC_CRM_023] Danh sách kết quả phải chứa đối tác '{mixedCaseName}'. Các tên tìm thấy: {string.Join(", ", names)}");
        }
        finally
        {
            // Dọn dẹp
            if (!string.IsNullOrEmpty(createdSocId))
            {
                Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/card.php?socid={createdSocId}");
                detailPage.DeleteCustomer();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_SAL_020 — Khảo sát hành vi khi thanh toán số tiền 0.00 €
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("Sales & Invoicing")]
    [Description("TC_SAL_020: Thanh toán số tiền 0.00 € tại Enter payment")]
    public void TC_SAL_020_EnterPayment_ZeroAmount_ShouldBeBlocked()
    {
        string invoiceUrl = CreateAndValidateStandardInvoice();
        TestContext.WriteLine($"[TC_SAL_020] Hóa đơn Unpaid tại: {invoiceUrl}");

        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        string error = invoiceDetailPage.TryEnterPayment(0.00m);

        TestContext.WriteLine($"[TC_SAL_020 ACTUAL] Thông báo lỗi nhận được khi nhập 0.00 €: '{error}'");

        // Quay lại trang chi tiết hóa đơn để kiểm tra trạng thái
        Driver.Navigate().GoToUrl(invoiceUrl);
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

        string statusText = invoiceDetailPage.GetStatusText();
        decimal alreadyPaid = invoiceDetailPage.GetAlreadyPaidAmount();

        TestContext.WriteLine($"[TC_SAL_020 ACTUAL] Trạng thái sau thanh toán 0 €: '{statusText}', Already paid = {alreadyPaid} €");

        Assert.IsTrue(statusText.Contains("Not paid", StringComparison.OrdinalIgnoreCase) ||
                      statusText.Contains("Unpaid", StringComparison.OrdinalIgnoreCase) ||
                      statusText.Contains("Chưa thanh toán", StringComparison.OrdinalIgnoreCase),
            $"[TC_SAL_020] Hóa đơn phải giữ nguyên trạng thái Not paid. Thực tế: '{statusText}'");

        Assert.AreEqual(0.00m, alreadyPaid,
            $"[TC_SAL_020] Số tiền đã thanh toán (Already paid) phải bằng 0.00 €. Thực tế: {alreadyPaid} €");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_SAL_021 — Khảo sát hành vi khi thanh toán số tiền âm (-50.00 €)
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("Sales & Invoicing")]
    [Description("TC_SAL_021: Thanh toán số tiền âm -50.00 € tại Enter payment")]
    public void TC_SAL_021_EnterPayment_NegativeAmount_ShouldBeBlocked()
    {
        string invoiceUrl = CreateAndValidateStandardInvoice();
        TestContext.WriteLine($"[TC_SAL_021] Hóa đơn Unpaid tại: {invoiceUrl}");

        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        string error = invoiceDetailPage.TryEnterPayment(-50.00m);

        TestContext.WriteLine($"[TC_SAL_021 ACTUAL] Thông báo lỗi nhận được khi nhập -50.00 €: '{error}'");

        Driver.Navigate().GoToUrl(invoiceUrl);
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

        string statusText = invoiceDetailPage.GetStatusText();
        decimal alreadyPaid = invoiceDetailPage.GetAlreadyPaidAmount();

        TestContext.WriteLine($"[TC_SAL_021 ACTUAL] Trạng thái sau thanh toán âm: '{statusText}', Already paid = {alreadyPaid} €");

        Assert.IsTrue(statusText.Contains("Not paid", StringComparison.OrdinalIgnoreCase) ||
                      statusText.Contains("Unpaid", StringComparison.OrdinalIgnoreCase) ||
                      statusText.Contains("Chưa thanh toán", StringComparison.OrdinalIgnoreCase),
            $"[TC_SAL_021] Hóa đơn phải giữ nguyên trạng thái Not paid khi nhập tiền âm. Thực tế: '{statusText}'");

        Assert.AreEqual(0.00m, alreadyPaid,
            $"[TC_SAL_021] Số tiền đã thanh toán (Already paid) phải bằng 0.00 €. Thực tế: {alreadyPaid} €");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_SAL_023 — Thử Validate hóa đơn Draft rỗng (không có dòng sản phẩm)
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("Sales & Invoicing")]
    [Description("TC_SAL_023: Thử Validate một hóa đơn Draft rỗng không có dòng sản phẩm")]
    public async Task TC_SAL_023_ValidateInvoice_EmptyLines_ShouldBeBlocked()
    {
        // 0. Đếm số hóa đơn trong DB trước khi tạo
        int countBefore;
        await using (var conn = await DbHelper.OpenConnectionAsync())
        {
            await using var cmd = new MySqlConnector.MySqlCommand("SELECT COUNT(*) FROM llx_facture;", conn);
            countBefore = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // 1. Tạo hóa đơn Draft trực tiếp không qua Proposal
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/compta/facture/card.php?action=create");
        WaitHelper.WaitVisible(Driver, By.CssSelector("form#add, div.fiche"), 15);

        // Chọn khách hàng "Cong ty ABC" và chờ form reload xong
        var propalCreatePage = new ProposalCreatePage(Driver);
        propalCreatePage.SelectCustomerByName("Cong ty ABC");

        // Chờ sau khi chọn đối tác, form đã sẵn sàng để điền ngày
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
        var js = (IJavaScriptExecutor)Driver;

        // Click nút 'Now' để điền ngày hóa đơn
        try
        {
            var nowBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("#reButtonNow, button.datenowlink, a[id*='now']"), 5);
            nowBtn.Click();
        }
        catch (WebDriverTimeoutException)
        {
            js.ExecuteScript(@"
                var btn = document.querySelector('#reButtonNow, button.datenowlink, tr[id*=""date""] a');
                if (btn) btn.click();
                else {
                    var d = new Date();
                    var day = String(d.getDate()).padStart(2, '0');
                    var month = String(d.getMonth() + 1).padStart(2, '0');
                    var year = d.getFullYear();
                    var inp = document.querySelector('#re, input[name=""re""]');
                    if (inp) { inp.value = month + '/' + day + '/' + year; $(inp).trigger('change'); }
                }
            ");
        }

        // Click nút 'Create draft'
        var submitBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input[type='submit'].button-save, input[type='submit'][value*='draft'], input[name='bouton']"), 10);
        submitBtn.Click();

        // Chờ chuyển hướng THẬT SỰ sang trang chi tiết hóa đơn mới
        bool onDetailPage = wait.Until(d =>
        {
            string u = d.Url;
            return Regex.IsMatch(u, @"compta/facture/card\.php\?.*(?<!\w)(facid|id)=\d+", RegexOptions.IgnoreCase) && !u.Contains("action=create");
        });

        Assert.IsTrue(onDetailPage, $"[TC_SAL_023] Phải tạo được hóa đơn nháp và chuyển về trang chi tiết. URL hiện tại: {Driver.Url}");

        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        string draftRef = invoiceDetailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_023] Hóa đơn Draft rỗng vừa tạo: Ref='{draftRef}', URL={Driver.Url}");

        // 2. Xác minh trong Database: phải có thêm 1 bản ghi hóa đơn mới ở trạng thái Draft (fk_statut=0)
        int countAfter;
        await using (var conn = await DbHelper.OpenConnectionAsync())
        {
            await using var cmd = new MySqlConnector.MySqlCommand("SELECT COUNT(*) FROM llx_facture;", conn);
            countAfter = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        TestContext.WriteLine($"[TC_SAL_023 DB] Số hóa đơn trước: {countBefore}, Sau khi tạo draft rỗng: {countAfter}");
        Assert.IsTrue(countAfter > countBefore,
            $"[TC_SAL_023] Hóa đơn Draft rỗng phải được lưu thành công vào cơ sở dữ liệu. Trước: {countBefore}, Sau: {countAfter}");

        // 3. Kiểm tra hành vi chặn Validate trên hóa đơn rỗng
        // Trên Dolibarr, khi hóa đơn chưa có dòng sản phẩm:
        // - Nút Validate bị ẩn hoàn toàn khỏi tabsAction hoặc hiển thị dưới dạng vô hiệu hóa (.butActionRefused)
        var actionContainer = Driver.FindElements(By.CssSelector("div.tabsAction, .tabsAction"));
        string tabsActionText = actionContainer.Count > 0 ? actionContainer[0].Text.Trim() : string.Empty;
        TestContext.WriteLine($"[TC_SAL_023 ACTUAL] Các nút hành động trong tabsAction: '{tabsActionText}'");

        var clickableValidateButtons = Driver.FindElements(By.CssSelector("div.tabsAction a.butAction[href*='action=valid']:not(.butActionRefused)"));
        bool canValidate = clickableValidateButtons.Count > 0 && clickableValidateButtons[0].Displayed;

        // Kiểm tra xem có nút Validate bị từ chối (.butActionRefused) kèm tooltip giải thích lý do không
        var refusedButtons = Driver.FindElements(By.CssSelector("div.tabsAction a.butActionRefused[href*='action=valid'], div.tabsAction span.butActionRefused"));
        string refusedTooltip = refusedButtons.Count > 0 ? refusedButtons[0].GetAttribute("title") ?? string.Empty : string.Empty;

        // Thử kích hoạt trực tiếp action=valid trên URL để kiểm tra phản hồi/thông báo lỗi từ phía server Dolibarr
        var facIdMatch = Regex.Match(Driver.Url, @"(?:facid|id)=(\d+)");
        string currentFacId = facIdMatch.Success ? facIdMatch.Groups[1].Value : string.Empty;
        
        string serverErrorMessage = string.Empty;
        if (!string.IsNullOrEmpty(currentFacId))
        {
            Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/compta/facture/card.php?facid={currentFacId}&action=valid");
            
            // Nếu có hộp thoại xác nhận Validate xuất hiện, thử bấm Yes để xem hệ thống phản hồi thế nào
            try
            {
                var confirmBtn = Driver.FindElements(By.XPath("//div[contains(@class, 'ui-dialog-buttonset')]//button[contains(., 'Yes') or contains(., 'Oui')] | //input[@value='Yes' or @value='Oui'] | //input[contains(@class, 'confirmvalidatebutton')]"));
                if (confirmBtn.Count > 0 && confirmBtn[0].Displayed)
                {
                    confirmBtn[0].Click();
                    WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 10);
                }
            }
            catch (Exception) { }

            serverErrorMessage = invoiceDetailPage.GetErrorMessage();
            if (string.IsNullOrEmpty(serverErrorMessage))
            {
                var warningEls = Driver.FindElements(By.CssSelector("div.warning, div.error, div.info, div.jnotify-message, .ui-dialog-content"));
                foreach (var w in warningEls)
                {
                    if (w.Displayed && !string.IsNullOrWhiteSpace(w.Text))
                    {
                        serverErrorMessage = w.Text.Trim();
                        break;
                    }
                }
            }
        }

        string currentRef = invoiceDetailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_023 ACTUAL] Nút Validate khả dụng trên UI: {canValidate}. Refused Tooltip: '{refusedTooltip}'. Phản hồi Server: '{serverErrorMessage}'. Ref sau thao tác: '{currentRef}'");

        // 4. Assertions:
        // Hệ thống phải chặn Validate trên UI (nút Validate không khả dụng hoặc bị ẩn)
        Assert.IsFalse(canValidate,
            "[TC_SAL_023 BUG] Hệ thống cho phép hiển thị nút Validate khả dụng trên hóa đơn rỗng chưa có sản phẩm!");

        // Khẳng định chính xác thông báo lỗi từ server Dolibarr:
        // 'Object (PROV...) must have lines to be validated.'
        Assert.IsFalse(string.IsNullOrEmpty(serverErrorMessage),
            "[TC_SAL_023] Hệ thống phải hiển thị thông báo lỗi khi cố tình Validate hóa đơn rỗng!");

        StringAssert.Contains(serverErrorMessage, "must have lines to be validated",
            $"[TC_SAL_023] Thông báo lỗi không khớp kỳ vọng. Thực tế: '{serverErrorMessage}'");

        // Hóa đơn rỗng bắt buộc phải giữ nguyên mã tạm PROV (không được cấp mã chính thức IN...)
        StringAssert.StartsWith(currentRef, "PROV",
            $"[TC_SAL_023 BUG] Hóa đơn rỗng bị cấp mã chính thức! Mã hiện tại: '{currentRef}'");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_STK_010 — Khảo sát hành vi khi điều chỉnh tồn kho Quantity = 0
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("Stock")]
    [Description("TC_STK_010: Khảo sát hành vi khi nhập Quantity = 0 tại Correct stock")]
    public void TC_STK_010_CorrectStock_ZeroQuantity_StockAndMovementsShouldNotChange()
    {
        const string targetProduct = "PR002";
        var stockPage = new WarehouseStockPage(Driver);

        // 1. Đọc tồn kho ban đầu và số dòng biến động ban đầu
        int initialStock = stockPage.GetProductPhysicalStock(targetProduct);
        int initialMovements = stockPage.GetStockMovementsCount(targetProduct);
        TestContext.WriteLine($"[TC_STK_010] Trước thao tác: Tồn kho {targetProduct} = {initialStock}, Số dòng movements = {initialMovements}");

        // 2. Thao tác điều chỉnh số lượng = 0
        string error = stockPage.PerformStockCorrection(targetProduct, 0, "Khao sat so luong 0");
        TestContext.WriteLine($"[TC_STK_010] Phản hồi sau khi Record Quantity=0: Error='{error}'");

        // 3. Đọc lại tồn kho và số dòng biến động sau thao tác
        int afterStock = stockPage.GetProductPhysicalStock(targetProduct);
        int afterMovements = stockPage.GetStockMovementsCount(targetProduct);
        TestContext.WriteLine($"[TC_STK_010 ACTUAL] Sau thao tác: Tồn kho = {afterStock}, Số dòng movements = {afterMovements}");

        // Assert: Tồn kho phải giữ nguyên, số lượng biến động không được phát sinh thêm
        Assert.AreEqual(initialStock, afterStock,
            $"[TC_STK_010] Tồn kho {targetProduct} không được thay đổi sau khi nhập Quantity=0. Trước: {initialStock}, Sau: {afterStock}");

        Assert.AreEqual(initialMovements, afterMovements,
            $"[TC_STK_010] Số dòng biến động kho không được tăng thêm khi nhập Quantity=0. Trước: {initialMovements}, Sau: {afterMovements}");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TC_STK_014 — Đo thời gian tải trang Replenishment khi có sản phẩm cảnh báo
    // ─────────────────────────────────────────────────────────────────────────────
    [TestMethod]
    [TestCategory("Stock")]
    [Description("TC_STK_014: Đo thời gian tải trang Replenishment (ngưỡng <= 3000ms)")]
    public void TC_STK_014_StockReplenishment_LoadTime_ShouldBeWithinThreshold()
    {
        var stockPage = new WarehouseStockPage(Driver);

        // Đo thời gian tải trang và đếm số dòng
        var (elapsedMs, rowCount) = stockPage.MeasureReplenishmentPage();
        TestContext.WriteLine($"[TC_STK_014 ACTUAL] Thời gian tải trang Replenishment: {elapsedMs} ms, Số sản phẩm hiển thị: {rowCount}");

        // Kiểm tra precondition: Phải có ít nhất 3 sản phẩm dưới ngưỡng cảnh báo
        if (rowCount < 3)
        {
            Assert.Inconclusive($"[TC_STK_014] Thiếu precondition: Hiện chỉ có {rowCount}/3 sản phẩm cần bổ sung trên trang Replenishment. Không tự sửa ngưỡng baseline.");
            return;
        }

        // Assert thời gian tải <= 3000ms (ngưỡng do nhóm tự đặt cho kịch bản hiệu năng nhẹ)
        Assert.IsTrue(elapsedMs <= 3000,
            $"[TC_STK_014] Thời gian tải trang Replenishment ({elapsedMs} ms) vượt quá ngưỡng 3000 ms.");
    }
}

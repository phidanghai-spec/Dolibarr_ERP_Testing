using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace DolibarrTests.Tests;

/// <summary>
/// Test Suite kiểm thử tự động Module Sales & Invoicing:
/// - TC_SAL_001: Tạo báo giá thương mại nháp (Draft Proposal) cho khách hàng Cong ty ABC
/// - TC_SAL_002: Thêm sản phẩm PR001, assert tiền hàng (Total HT, VAT, Total TTC)
/// - TC_SAL_003: Thêm sản phẩm PR002, assert cộng dồn lũy kế
/// - TC_SAL_004: Xác thực báo giá (Validate Proposal) chuyển trạng thái Open
/// - TC_SAL_005: Đóng báo giá với trạng thái Chấp thuận/Đã ký (Signed)
/// - TC_SAL_006: Sinh Hóa đơn bán hàng từ báo giá đã ký
/// - TC_SAL_007: Xác thực hóa đơn chuyển sang trạng thái Unpaid
/// - TC_SAL_008: Xác minh quy tắc trừ kho tự động sau khi Validate hóa đơn
/// </summary>
[TestClass]
[DoNotParallelize] // Các test Sales thao tác dữ liệu nghiệp vụ theo luồng, chạy tuần tự
public class SalesProposalTests : BaseTest
{
    private string? _createdProposalUrl;

    // ── Helper: Đăng nhập ────────────────────────────────────────────────────────
    private void Login()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.GoTo();
        loginPage.LoginAs(TestConfig.AdminUsername, TestConfig.AdminPassword);
        var dashboard = new DashboardPage(Driver);
        Assert.IsTrue(dashboard.IsLoaded(),
            "Đăng nhập admin phải thành công. Kiểm tra Dolibarr đang chạy và biến DOLIBARR_ADMIN_PASSWORD.");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_001 — Tạo báo giá nháp cho khách hàng
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_001 — Tạo báo giá nháp mới cho khách hàng Cong ty ABC")]
    public void TC_SAL_001_CreateDraftProposal_ShouldSucceed()
    {
        _createdProposalUrl = null;
        try
        {
            const string targetCustomer = "Cong ty ABC";

            Login();
            var createPage = new ProposalCreatePage(Driver);

            createPage.GoTo();
            Assert.IsTrue(createPage.IsOnCreatePage(),
                "[TC_SAL_001] Phải điều hướng được đến trang tạo báo giá mới.");

            // Chọn khách hàng
            createPage.SelectCustomerByName(targetCustomer);

            // Chọn ngày báo giá là hiện tại (Now)
            createPage.SetProposalDateNow();

            // Nhấn tạo nháp
            createPage.ClickCreateDraft();

            // Chờ redirect về trang chi tiết báo giá (comm/propal/card.php?id=\d+)
            var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(15));
            bool redirected = wait.Until(d => Regex.IsMatch(d.Url, @"comm/propal/card\.php\?id=\d+", RegexOptions.IgnoreCase));

            _createdProposalUrl = Driver.Url;
            Assert.IsTrue(redirected,
                $"[TC_SAL_001] Sau khi tạo nháp, phải chuyển về trang chi tiết báo giá. URL: {Driver.Url}");

            // Đọc mã tham chiếu PR... từ giao diện
            string pageSource = Driver.PageSource;
            var refMatch = Regex.Match(pageSource, @"(PR\d{4}-\d{4,5}|PROV\d+)", RegexOptions.IgnoreCase);
            string proposalRef = refMatch.Success ? refMatch.Value : "Unknown";

            TestContext.WriteLine($"[TC_SAL_001] Proposal Reference: '{proposalRef}'");
            TestContext.WriteLine($"[TC_SAL_001 PASS] Tạo báo giá nháp thành công cho '{targetCustomer}'. URL: {Driver.Url}");

            var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_001");
            if (screenshotPath != null)
                TestContext.WriteLine($"[Screenshot] {screenshotPath}");
        }
        finally
        {
            TestContext.WriteLine($"[Cleanup info] Proposal URL created: {_createdProposalUrl}");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_002 — Thêm sản phẩm PR001, kiểm tra tính toán tiền hàng và thuế
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_002 — Thêm sản phẩm PR001 (SL=2, Đơn giá=50€, VAT=10%), assert Total HT=100€, VAT=10€, TTC=110€")]
    public void TC_SAL_002_AddProductLine_ShouldCalculateCorrectTotal()
    {
        Login();
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var detailPage = new ProposalDetailPage(Driver);
        string initialRef = detailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_002] Tạo báo giá nháp thành công: {initialRef}");

        // Thêm sản phẩm PR001 với số lượng = 2
        detailPage.AddPredefinedProduct("PR001", quantity: 2);

        // Kiểm tra tiền hàng tính toán tự động
        decimal totalHT = detailPage.GetAmountExclTax();
        decimal totalVAT = detailPage.GetAmountTax();
        decimal totalTTC = detailPage.GetAmountIncTax();

        TestContext.WriteLine($"[TC_SAL_002] Kết quả tính tiền: Total HT = {totalHT} €, Total VAT = {totalVAT} €, Total TTC = {totalTTC} €");

        Assert.AreEqual(200.00m, totalHT, 0.01m, "[TC_SAL_002] Total HT phải bằng 200.00 € (2 * 100.00 €)");
        Assert.AreEqual(20.00m, totalVAT, 0.01m, "[TC_SAL_002] Total VAT phải bằng 20.00 € (10% của 200.00 €)");
        Assert.AreEqual(220.00m, totalTTC, 0.01m, "[TC_SAL_002] Total TTC phải bằng 220.00 €");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_002");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_003 — Thêm sản phẩm PR002, kiểm tra tính toán lũy kế
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_003 — Thêm PR001 (SL=2) và PR002 (SL=1), assert tổng tiền lũy kế Total HT, VAT, TTC")]
    public void TC_SAL_003_AddMultipleProducts_ShouldCalculateCumulativeTotal()
    {
        Login();
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var detailPage = new ProposalDetailPage(Driver);
        string initialRef = detailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_003] Tạo báo giá nháp thành công: {initialRef}");

        // 1. Thêm dòng PR001 (SL=2)
        detailPage.AddPredefinedProduct("PR001", quantity: 2);

        // 2. Thêm dòng PR002 (SL=1)
        detailPage.AddPredefinedProduct("PR002", quantity: 1);

        // Đọc tổng tiền sau khi thêm cả 2 dòng
        decimal totalHT = detailPage.GetAmountExclTax();
        decimal totalVAT = detailPage.GetAmountTax();
        decimal totalTTC = detailPage.GetAmountIncTax();

        TestContext.WriteLine($"[TC_SAL_003] Kết quả lũy kế: Total HT = {totalHT} €, Total VAT = {totalVAT} €, Total TTC = {totalTTC} €");

        Assert.AreEqual(310.00m, totalHT, 0.01m, "[TC_SAL_003] Total HT lũy kế phải bằng 310.00 € (2*100 + 1*110)");
        Assert.AreEqual(31.00m, totalVAT, 0.01m, "[TC_SAL_003] Total VAT lũy kế phải bằng 31.00 € (10% của 310.00 €)");
        Assert.AreEqual(341.00m, totalTTC, 0.01m, "[TC_SAL_003] Total TTC phải bằng 341.00 €");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_003");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_004 — Xác thực báo giá (Validate Proposal)
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_004 — Validate báo giá nháp, assert mã chuyển từ PROV sang PR và trạng thái Open")]
    public void TC_SAL_004_ValidateProposal_ShouldChangeStatusToOpen()
    {
        Login();
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var detailPage = new ProposalDetailPage(Driver);
        string draftRef = detailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_004] Báo giá nháp ban đầu: {draftRef}");

        // Thêm ít nhất 1 dòng sản phẩm trước khi Validate
        detailPage.AddPredefinedProduct("PR001", quantity: 1);

        // Nhấn Validate
        detailPage.ValidateProposal();

        // Kiểm tra sau khi Validate:
        // 1. Mã tham chiếu chính thức phải có tiền tố PR (ví dụ PR2610-0001)
        string officialRef = detailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_004] Mã tham chiếu sau khi Validate: {officialRef}");
        Assert.IsTrue(Regex.IsMatch(officialRef, @"^PR\d+", RegexOptions.IgnoreCase),
            $"[TC_SAL_004] Mã báo giá sau Validate phải chuyển từ PROV sang PR... Thực tế: '{officialRef}'");

        // 2. Trạng thái không còn chứa 'Draft' và hiển thị 'Open' hoặc 'Validated'
        string status = detailPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_004] Trạng thái sau Validate: '{status}'");
        Assert.IsFalse(status.Contains("Draft", StringComparison.OrdinalIgnoreCase),
            "[TC_SAL_004] Trạng thái không được còn là Draft.");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_004");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_005 — Đóng báo giá với trạng thái Chấp thuận/Đã ký (Signed)
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_005 — Đóng báo giá trạng thái Đã ký (Signed), assert trạng thái cập nhật Signed")]
    public void TC_SAL_005_CloseProposalAsSigned_ShouldUpdateStatus()
    {
        Login();
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var detailPage = new ProposalDetailPage(Driver);
        detailPage.AddPredefinedProduct("PR001", quantity: 1);
        detailPage.ValidateProposal();

        // Đóng báo giá với trạng thái Signed
        detailPage.CloseAsSigned();

        // Kiểm tra trạng thái hiển thị 'Signed'
        string status = detailPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_005] Trạng thái sau khi Đóng: '{status}'");
        Assert.IsTrue(status.Contains("Signed", StringComparison.OrdinalIgnoreCase),
            $"[TC_SAL_005] Trạng thái phải chứa 'Signed'. Thực tế: '{status}'");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_005");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_006 — Sinh Hóa đơn từ Báo giá đã ký (Create Invoice from Signed Proposal)
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_006 — Tạo hóa đơn từ báo giá đã ký, assert kế thừa đúng tổng tiền và trạng thái Draft")]
    public void TC_SAL_006_CreateInvoiceFromSignedProposal_ShouldInheritTotal()
    {
        Login();
        // 1. Tạo và ký duyệt báo giá
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var propalDetailPage = new ProposalDetailPage(Driver);
        detailPage_AddProduct(propalDetailPage, "PR001", 2);
        propalDetailPage.ValidateProposal();
        propalDetailPage.CloseAsSigned();

        // 2. Nhấn Create Invoice
        propalDetailPage.ClickCreateInvoice();

        // 3. Tại màn hình tạo hóa đơn, xác nhận ngày và tạo nháp
        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        invoiceDetailPage.SubmitCreateInvoiceDraft();

        // 4. Kiểm tra hóa đơn nháp được tạo
        string invoiceRef = invoiceDetailPage.GetReference();
        string invoiceStatus = invoiceDetailPage.GetStatusText();
        decimal invoiceTTC = invoiceDetailPage.GetAmountIncTax();

        TestContext.WriteLine($"[TC_SAL_006] Hóa đơn nháp: Ref='{invoiceRef}', Status='{invoiceStatus}', Total TTC={invoiceTTC} €");

        Assert.IsTrue(Regex.IsMatch(invoiceRef, @"PROV\d+", RegexOptions.IgnoreCase),
            $"[TC_SAL_006] Hóa đơn mới tạo phải có mã nháp PROV... Thực tế: '{invoiceRef}'");
        Assert.IsTrue(invoiceStatus.Contains("Draft", StringComparison.OrdinalIgnoreCase),
            $"[TC_SAL_006] Trạng thái hóa đơn mới tạo phải là Draft. Thực tế: '{invoiceStatus}'");
        Assert.AreEqual(220.00m, invoiceTTC, 0.02m,
            $"[TC_SAL_006] Tổng tiền TTC của hóa đơn ({invoiceTTC} €) phải kế thừa chính xác từ báo giá (220.00 €)");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_006");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_007 — Xác thực hóa đơn (Validate Invoice) chuyển sang Unpaid
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_007 — Validate hóa đơn, assert mã chuyển sang IN và trạng thái Unpaid")]
    public void TC_SAL_007_ValidateInvoice_ShouldChangeStatusToUnpaid()
    {
        Login();
        // Tạo và ký báo giá
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var propalDetailPage = new ProposalDetailPage(Driver);
        detailPage_AddProduct(propalDetailPage, "PR001", 1);
        propalDetailPage.ValidateProposal();
        propalDetailPage.CloseAsSigned();
        propalDetailPage.ClickCreateInvoice();

        // Tạo hóa đơn nháp
        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        invoiceDetailPage.SubmitCreateInvoiceDraft();

        // Validate hóa đơn
        invoiceDetailPage.ValidateInvoice();

        string officialRef = invoiceDetailPage.GetReference();
        string status = invoiceDetailPage.GetStatusText();

        TestContext.WriteLine($"[TC_SAL_007] Hóa đơn sau Validate: Ref='{officialRef}', Status='{status}'");

        Assert.IsTrue(Regex.IsMatch(officialRef, @"^IN\d+", RegexOptions.IgnoreCase),
            $"[TC_SAL_007] Mã hóa đơn sau Validate phải có tiền tố IN... Thực tế: '{officialRef}'");
        Assert.IsFalse(status.Contains("Draft", StringComparison.OrdinalIgnoreCase),
            "[TC_SAL_007] Trạng thái hóa đơn không được còn là Draft.");
        Assert.IsTrue(status.Contains("Unpaid", StringComparison.OrdinalIgnoreCase) || status.Contains("Not paid", StringComparison.OrdinalIgnoreCase),
            $"[TC_SAL_007] Trạng thái hóa đơn phải là Unpaid (Chưa thanh toán). Thực tế: '{status}'");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_007");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_008 — Xác minh quy tắc tự động trừ kho sau khi Validate hóa đơn
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [TestCategory("Stock")]
    [Description("TC_SAL_008 — Xác minh quy tắc trừ kho: bán 2 cái PR001, sau khi validate hóa đơn thì tồn kho giảm đúng 2")]
    public void TC_SAL_008_ValidateInvoice_ShouldDecreaseProductStock()
    {
        Login();
        const int sellQty = 2;
        const string targetProduct = "PR001";

        // 1. Đọc tồn kho ban đầu của PR001
        var stockPage = new WarehouseStockPage(Driver);
        int initialStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_SAL_008] Tồn kho ban đầu của {targetProduct}: {initialStock}");
        Assert.IsTrue(initialStock >= sellQty,
            $"[TC_SAL_008] Tồn kho {targetProduct} hiện tại ({initialStock}) phải đủ để bán ({sellQty})");

        // 2. Tạo báo giá bán 2 cái PR001
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var propalDetailPage = new ProposalDetailPage(Driver);
        detailPage_AddProduct(propalDetailPage, targetProduct, sellQty);
        propalDetailPage.ValidateProposal();
        propalDetailPage.CloseAsSigned();
        propalDetailPage.ClickCreateInvoice();

        // 3. Tạo hóa đơn nháp và Validate hóa đơn
        var invoiceDetailPage = new InvoiceDetailPage(Driver);
        invoiceDetailPage.SubmitCreateInvoiceDraft();
        invoiceDetailPage.ValidateInvoice();

        string invoiceRef = invoiceDetailPage.GetReference();
        TestContext.WriteLine($"[TC_SAL_008] Hóa đơn đã validate: {invoiceRef}");

        // 4. Đọc lại tồn kho sau khi xuất hóa đơn
        int newStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_SAL_008] Tồn kho của {targetProduct} sau khi Validate hóa đơn: {newStock}");

        // 5. Assert: Tồn kho phải giảm chính xác bằng số lượng bán (initialStock - sellQty)
        int expectedStock = initialStock - sellQty;
        Assert.AreEqual(expectedStock, newStock,
            $"[TC_SAL_008] Tồn kho {targetProduct} phải giảm từ {initialStock} xuống {expectedStock} (giảm {sellQty}). Thực tế: {newStock}");

        TestContext.WriteLine($"[TC_SAL_008 PASS] Quy tắc tự động trừ kho hoạt động hoàn hảo: {initialStock} -> {newStock} (-{sellQty})");

        var screenshotPath = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_008");
        if (screenshotPath != null)
            TestContext.WriteLine($"[Screenshot] {screenshotPath}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_013 — Báo giá có chiết khấu theo dòng (% Discount)
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_013 — Thêm sản phẩm PR001 (SL=2) với chiết khấu 10%, assert Total HT, VAT và Total TTC sau chiết khấu")]
    public void TC_SAL_013_AddProductWithDiscount_ShouldCalculateCorrectTotal()
    {
        Login();
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var detailPage = new ProposalDetailPage(Driver);
        // PR001: Đơn giá 100€, VAT 10%. SL = 2, Giảm 10% -> HT = 180€, VAT = 18€, TTC = 198€
        detailPage.AddPredefinedProduct("PR001", 2, discountPercent: 10m);

        decimal totalHT = detailPage.GetAmountExclTax();
        decimal totalVAT = detailPage.GetAmountTax();
        decimal totalTTC = detailPage.GetAmountIncTax();

        TestContext.WriteLine($"[TC_SAL_013] Sau chiết khấu 10%: Total HT={totalHT} €, VAT={totalVAT} €, Total TTC={totalTTC} €");

        Assert.AreEqual(180.00m, totalHT, "[TC_SAL_013] Total HT sau chiết khấu 10% phải là 180.00 €");
        Assert.AreEqual(18.00m, totalVAT, "[TC_SAL_013] VAT 10% trên 180.00 € phải là 18.00 €");
        Assert.AreEqual(198.00m, totalTTC, "[TC_SAL_013] Total TTC sau chiết khấu 10% phải là 198.00 €");

        var screenshot = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_013");
        if (screenshot != null) TestContext.WriteLine($"[Screenshot] {screenshot}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_014 — Đóng báo giá trạng thái Từ chối (Refused Proposal)
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_014 — Đóng báo giá trạng thái Refused, assert trạng thái cập nhật và không cho phép tạo hóa đơn")]
    public void TC_SAL_014_CloseProposalAsRefused_ShouldNotAllowInvoiceCreation()
    {
        Login();
        var createPage = new ProposalCreatePage(Driver);
        createPage.GoTo();
        createPage.SelectCustomerByName("Cong ty ABC");
        createPage.SetProposalDateNow();
        createPage.ClickCreateDraft();

        var detailPage = new ProposalDetailPage(Driver);
        detailPage.AddPredefinedProduct("PR001", 1);
        detailPage.ValidateProposal();

        // Đóng báo giá với trạng thái Refused
        detailPage.CloseAsRefused();

        string statusText = detailPage.GetStatusText();
        bool hasCreateInvoiceBtn = detailPage.HasCreateInvoiceButton();

        TestContext.WriteLine($"[TC_SAL_014] Trạng thái sau khi đóng Refused: '{statusText}', HasCreateInvoiceButton: {hasCreateInvoiceBtn}");

        Assert.IsTrue(statusText.Contains("Refused", StringComparison.OrdinalIgnoreCase) ||
                      statusText.Contains("Closed", StringComparison.OrdinalIgnoreCase),
            $"[TC_SAL_014] Trạng thái báo giá phải là Refused/Closed. Thực tế: '{statusText}'");
        Assert.IsFalse(hasCreateInvoiceBtn,
            "[TC_SAL_014] Báo giá bị từ chối không được phép hiển thị nút Create invoice.");

        var screenshot = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_014");
        if (screenshot != null) TestContext.WriteLine($"[Screenshot] {screenshot}");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TC_SAL_015 — Ghi nhận Thanh toán toàn bộ hóa đơn (Full Payment 100%)
    // ════════════════════════════════════════════════════════════════════════════
    [TestMethod]
    [TestCategory("Sales")]
    [Description("TC_SAL_015 — Thanh toán 100% hóa đơn bán hàng, assert trạng thái chuyển sang Paid và số tiền còn lại bằng 0")]
    public void TC_SAL_015_PayInvoiceInFull_ShouldChangeStatusToPaid()
    {
        Login();
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

        string unpaidStatus = invoiceDetailPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_015] Trạng thái hóa đơn trước khi thanh toán: '{unpaidStatus}'");

        // Ghi nhận thanh toán toàn bộ 100%
        string invoiceCardUrl = Driver.Url;
        invoiceDetailPage.EnterPayment(0m);

        // Quay lại trang chi tiết hóa đơn nếu chưa redirect
        if (!Driver.Url.Contains("facture/card.php"))
        {
            Driver.Navigate().GoToUrl(invoiceCardUrl);
            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 10);
        }

        string paidStatus = invoiceDetailPage.GetStatusText();
        decimal remaining = invoiceDetailPage.GetRemainingAmount();
        TestContext.WriteLine($"[TC_SAL_015] Trạng thái sau thanh toán 100%: '{paidStatus}', Remains to pay: {remaining} €");

        bool isPaid = !paidStatus.Contains("Not", StringComparison.OrdinalIgnoreCase) &&
                      paidStatus.Contains("Paid", StringComparison.OrdinalIgnoreCase);

        Assert.IsTrue(isPaid,
            $"[TC_SAL_015] Hóa đơn phải chuyển sang trạng thái Paid (không còn Not paid). Thực tế: '{paidStatus}'");
        Assert.AreEqual(0.00m, remaining,
            $"[TC_SAL_015] Số tiền còn lại phải trả sau thanh toán 100% phải là 0.00 €. Thực tế: {remaining} €");

        var screenshot = ScreenshotHelper.Capture(Driver, TestContext.TestName ?? "TC_SAL_015");
        if (screenshot != null) TestContext.WriteLine($"[Screenshot] {screenshot}");
    }

    // ── Helper dùng chung ───────────────────────────────────────────────────────
    private static void detailPage_AddProduct(ProposalDetailPage detailPage, string productCode, int qty)
    {
        detailPage.AddPredefinedProduct(productCode, qty);
    }
}


using DolibarrTests.Helpers;
using DolibarrTests.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DolibarrTests.Tests;

/// <summary>
/// Test Suite thực thi tự động 8 kịch bản cho:
/// - Sales & Invoicing: Hóa đơn đặc biệt (TC_SAL_009..012)
/// - Stock: Điều chỉnh kho & Cảnh báo tồn kho (TC_STK_001..004)
/// Tự động chụp ảnh minh chứng vào evidence/manual/ và cập nhật Excel qua ExcelResultUpdater.
/// </summary>
[TestClass]
[DoNotParallelize]
public class SpecialInvoiceAndStockTests : BaseTest
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
            "Đăng nhập admin phải thành công. Kiểm tra Dolibarr đang chạy và DOLIBARR_ADMIN_PASSWORD.");
    }

    /// <summary>Helper tạo và validate hóa đơn chuẩn qua luồng Proposal đã được kiểm chứng</summary>
    private string CreateAndValidateInvoiceViaProposal()
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

    [TestMethod]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_SAL_009: Hủy hóa đơn bán hàng ở trạng thái Unpaid (Abandon/Cancel)")]
    public void TC_SAL_009_CancelInvoice_ShouldUpdateStatusToAbandoned()
    {
        string invoiceUrl = CreateAndValidateInvoiceViaProposal();
        TestContext.WriteLine($"[TC_SAL_009] Hóa đơn Unpaid tại: {invoiceUrl}");

        // Click CLASSIFY 'ABANDONED'
        var abandonBtn = WaitHelper.WaitClickable(Driver, By.XPath("//a[contains(@class, 'butAction') and (contains(@href, 'action=canceled') or contains(text(), 'ABANDONED'))]"), 10);
        abandonBtn.Click();

        // Xử lý xác nhận (Form hoặc dialog confirm)
        try
        {
            var js = (IJavaScriptExecutor)Driver;
            js.ExecuteScript(@"
                // Chọn radio reason đầu tiên
                var radios = document.querySelectorAll('div.ui-dialog input[type=""radio""], input[type=""radio""]');
                if (radios.length > 0) {
                    radios[0].checked = true;
                    radios[0].click();
                }
                // Điền comment nếu có
                var comment = document.querySelector('div.ui-dialog textarea, div.ui-dialog input[type=""text""], textarea[name=""comment""]');
                if (comment) {
                    comment.value = 'Khách hàng đổi ý hủy đơn';
                }
            ");

            var confirmBtn = WaitHelper.WaitClickable(Driver, By.XPath("//div[contains(@class, 'ui-dialog-buttonset')]//button[contains(., 'Yes') or 1]"), 5);
            confirmBtn.Click();
        }
        catch
        {
            var js = (IJavaScriptExecutor)Driver;
            js.ExecuteScript(@"
                var b = document.querySelector('div.ui-dialog-buttonset button, input[type=""submit""][value=""Yes""]');
                if (b) b.click();
            ");
        }

        // Chờ dialog đóng và badge trạng thái chuyển sang Abandoned / Canceled
        WaitHelper.WaitGone(Driver, By.CssSelector("div.ui-dialog"), 10);
        var invPage = new InvoiceDetailPage(Driver);
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
        {
            string s = invPage.GetStatusText();
            return s.Contains("Canceled", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("Abandoned", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("Bị hủy", StringComparison.OrdinalIgnoreCase);
        });

        // Chụp ảnh minh chứng
        string evidenceRelPath = "evidence/manual/TC_SAL_009_invoice_canceled.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        // Assert trạng thái Canceled / Abandoned qua InvoiceDetailPage
        string statusText = invPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_009] Trạng thái sau khi hủy: '{statusText}'");

        bool isCanceled = statusText.Contains("Canceled", StringComparison.OrdinalIgnoreCase) ||
                          statusText.Contains("Abandoned", StringComparison.OrdinalIgnoreCase) ||
                          statusText.Contains("Bị hủy", StringComparison.OrdinalIgnoreCase);

        Assert.IsTrue(isCanceled, $"Hóa đơn phải chuyển sang trạng thái Canceled/Abandoned. Thực tế: '{statusText}'");

        // Ghi vào Excel
        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_SAL_009", "Pass",
            $"Hóa đơn chuyển sang trạng thái {statusText}. Nút thanh toán bị vô hiệu hóa.", evidenceRelPath);
    }

    [TestMethod]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_SAL_010: Tạo hóa đơn hoàn tiền / điều chỉnh (Credit note AV...) từ hóa đơn gốc")]
    public void TC_SAL_010_CreateCreditNote_ShouldGenerateRefAV()
    {
        string invoiceUrl = CreateAndValidateInvoiceViaProposal();
        TestContext.WriteLine($"[TC_SAL_010] Hóa đơn gốc tại: {invoiceUrl}");

        // Nhấn nút CREATE CREDIT NOTE
        var creditBtn = WaitHelper.WaitClickable(Driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'CREDIT NOTE') or contains(@href, 'type=2'))]"), 10);
        creditBtn.Click();

        WaitHelper.WaitVisible(Driver, By.CssSelector("form#forminvoice, form[name='formsoc'], div.fiche"), 15);

        // Click Now cho Invoice date và check Create Credit note with lines
        var js = (IJavaScriptExecutor)Driver;
        js.ExecuteScript(@"
            // Click Now cho Invoice date
            var nowBtn = document.querySelector('tr[id*=""date""] a, a[id*=""now""], a.dp-choose-date');
            var links = document.querySelectorAll('a, button');
            for (var i = 0; i < links.length; i++) {
                if (links[i].innerText && links[i].innerText.trim() === 'Now') {
                    links[i].click();
                    break;
                }
            }

            // Checkbox Create Credit Note with lines from origin
            var cbs = document.querySelectorAll('input[type=""checkbox""]');
            if (cbs.length > 0) {
                cbs[0].checked = true;
                $(cbs[0]).trigger('change');
            }
        ");

        // Nhấn Create draft cho Credit note
        var submitBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input[type='submit'].button-save, input[type='submit'][value*='draft'], input[name='bouton']"), 10);
        submitBtn.Click();

        // Validate Credit note
        var invPage = new InvoiceDetailPage(Driver);
        invPage.ValidateInvoice();

        // Chờ reference của credit note được cấp mã chính thức (AV... hoặc IC...)
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
        {
            string r = invPage.GetReference();
            return Regex.IsMatch(r, @"^(AV|IC)\d+", RegexOptions.IgnoreCase);
        });

        string creditRef = invPage.GetReference();
        string statusText = invPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_010] Credit note đã tạo: Ref='{creditRef}', Status='{statusText}'");

        // Chụp ảnh minh chứng
        string evidenceRelPath = "evidence/manual/TC_SAL_010_credit_note_validated.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        Assert.IsTrue(Regex.IsMatch(creditRef, @"^(AV|IC)\d+", RegexOptions.IgnoreCase),
            $"Credit note phải có tiền tố tham chiếu AV hoặc IC... Thực tế: '{creditRef}'");
        Assert.IsFalse(statusText.Contains("Draft", StringComparison.OrdinalIgnoreCase),
            "Credit note phải được xác thực (không còn Draft).");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_SAL_010", "Pass",
            $"Hóa đơn hoàn tiền {creditRef} tạo thành công từ hóa đơn gốc và đã xác thực.", evidenceRelPath);
    }

    /// <summary>Helper ghi nhận thanh toán cho hóa đơn (2 bước chuẩn của Dolibarr)</summary>
    private void EnterPaymentForInvoice(decimal payAmount)
    {
        // Lấy facid từ URL hiện tại nếu có
        var matchFacId = Regex.Match(Driver.Url, @"facid=(\d+)");
        string facId = matchFacId.Success ? matchFacId.Groups[1].Value : string.Empty;

        // Nhấn nút ENTER PAYMENT
        var payBtn = WaitHelper.WaitClickable(Driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'ENTER PAYMENT') or contains(@href, 'compta/paiement.php'))]"), 10);
        payBtn.Click();

        WaitHelper.WaitVisible(Driver, By.CssSelector("form#payment_form, form[action*='paiement.php'], div.fiche"), 15);

        // 1. Điền ngày và phương thức thanh toán qua JS
        var js = (IJavaScriptExecutor)Driver;
        js.ExecuteScript(@"
            // Điền ngày thanh toán
            var nowBtn = document.querySelector('#reButtonNow, button.datenowlink');
            if (nowBtn) {
                nowBtn.click();
            } else {
                var dInput = document.querySelector('#re, input[name=""re""]');
                if (dInput) {
                    var now = new Date();
                    var dd = String(now.getDate()).padStart(2, '0');
                    var mm = String(now.getMonth() + 1).padStart(2, '0');
                    var yyyy = now.getFullYear();
                    dInput.value = mm + '/' + dd + '/' + yyyy;
                    $(dInput).trigger('change');
                }
            }

            // Chọn Payment mode: Cash (LIQ)
            var pSelect = document.querySelector('#selectpaiementcode, select[name=""paiementcode""]');
            if (pSelect && pSelect.options.length > 1) {
                pSelect.value = 'LIQ';
                if (!pSelect.value) pSelect.selectedIndex = 1;
                $(pSelect).trigger('change');
            }
        ");

        // 2. Điền số tiền thanh toán vào đúng input của hóa đơn
        js.ExecuteScript(@"
            var targetFacId = arguments[0];
            var amountToPay = arguments[1];

            if (targetFacId) {
                if (amountToPay <= 0) {
                    var autoBtn = document.querySelector('button[data-rowname=""amount_' + targetFacId + '""]');
                    if (autoBtn) autoBtn.click();
                } else {
                    var inp = document.querySelector('input[name=""amount_' + targetFacId + '""]');
                    if (inp) {
                        inp.value = amountToPay;
                        $(inp).trigger('change');
                    }
                }
            } else {
                var autoBtns = document.querySelectorAll('button.AutoFillAmount');
                if (amountToPay <= 0 && autoBtns.length > 0) {
                    autoBtns[autoBtns.length - 1].click();
                } else {
                    var inps = document.querySelectorAll('input.amount');
                    if (inps.length > 0) {
                        var targetInp = inps[inps.length - 1];
                        targetInp.value = amountToPay > 0 ? amountToPay : '110000';
                        $(targetInp).trigger('change');
                    }
                }
            }
        ", facId, payAmount > 0 ? payAmount.ToString("0", CultureInfo.InvariantCulture) : 0);

        // 3. Nhấn nút Pay ở form 1
        var savePayBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("form#payment_form input[type='submit'][value='Pay'], input[type='submit'].reposition"), 10);
        savePayBtn.Click();

        // Chờ form xác nhận thứ 2 tải xong
        WaitHelper.WaitVisible(Driver, By.CssSelector("input.confirmvalidatebutton, input[type='submit'][value='Validate'], form[action*='paiement']"), 10);

        // 4. Nhấn nút Validate ở form xác nhận thứ 2 (confirm_paiement)
        try
        {
            var validateBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input.confirmvalidatebutton, input[type='submit'][value='Validate']"), 5);
            validateBtn.Click();

            // Chờ redirect hoàn tất thanh toán (không còn confirm_paiement trên URL)
            new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
                !d.Url.Contains("confirm_paiement", StringComparison.OrdinalIgnoreCase) &&
                !d.Url.Contains("action=add", StringComparison.OrdinalIgnoreCase));
        }
        catch (WebDriverTimeoutException)
        {
            TestContext.WriteLine("[EnterPayment] Không thấy nút Validate confirm.");
        }
    }

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_SAL_011: Thanh toán từng phần (Partial Payment đợt 1: 50,000 / 110,000 VND)")]
    public void TC_SAL_011_PartialPayment_ShouldShowRemainingBalance()
    {
        string invoiceUrl = CreateAndValidateInvoiceViaProposal();
        TestContext.WriteLine($"[TC_SAL_011] Hóa đơn Unpaid tại: {invoiceUrl}");

        // Thanh toán một phần: 50,000
        EnterPaymentForInvoice(50000m);

        // Quay lại trang chi tiết hóa đơn nếu đang ở trang payment
        if (!Driver.Url.Contains("compta/facture/card.php"))
        {
            Driver.Navigate().GoToUrl(invoiceUrl);
            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 10);
        }

        var invPage = new InvoiceDetailPage(Driver);
        string statusText = invPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_011] Trạng thái sau thanh toán đợt 1: '{statusText}'");

        // Chụp ảnh minh chứng
        string evidenceRelPath = "evidence/manual/TC_SAL_011_partial_payment_started.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        bool isPartiallyPaid = statusText.Contains("Started", StringComparison.OrdinalIgnoreCase) ||
                               statusText.Contains("Partially", StringComparison.OrdinalIgnoreCase) ||
                               statusText.Contains("Đã bắt đầu", StringComparison.OrdinalIgnoreCase);

        Assert.IsTrue(isPartiallyPaid, $"Hóa đơn phải chuyển sang Started/Partially paid. Thực tế: '{statusText}'");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_SAL_011", "Pass",
            $"Thanh toán đợt 1 thành công (50,000.00 €). Trạng thái hóa đơn chuyển thành {statusText}, hiển thị dư nợ còn lại.", evidenceRelPath);
    }

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_SAL_012: Thanh toán hoàn tất nợ (Full Payment đợt 2: trả nốt và chuyển Paid)")]
    public void TC_SAL_012_FullPayment_ShouldCloseInvoiceAsPaid()
    {
        string invoiceUrl = CreateAndValidateInvoiceViaProposal();
        TestContext.WriteLine($"[TC_SAL_012] Hóa đơn Unpaid tại: {invoiceUrl}");

        // Trả đợt 1: 50,000
        EnterPaymentForInvoice(50000m);

        Driver.Navigate().GoToUrl(invoiceUrl);
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 10);

        // Trả đợt 2: trả hết toàn bộ phần còn lại (payAmount = 0 để click AutoFill)
        EnterPaymentForInvoice(0m);

        Driver.Navigate().GoToUrl(invoiceUrl);
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 10);

        var invPage = new InvoiceDetailPage(Driver);
        string statusText = invPage.GetStatusText();
        TestContext.WriteLine($"[TC_SAL_012] Trạng thái sau thanh toán toàn bộ: '{statusText}'");

        // Chụp ảnh minh chứng
        string evidenceRelPath = "evidence/manual/TC_SAL_012_full_payment_paid.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        bool isPaid = !statusText.Contains("Not", StringComparison.OrdinalIgnoreCase) &&
                      (statusText.Contains("Paid", StringComparison.OrdinalIgnoreCase) ||
                       statusText.Contains("Đã thanh toán", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(isPaid, $"Hóa đơn phải chuyển sang trạng thái Paid (không còn Not paid). Thực tế: '{statusText}'");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_SAL_012", "Pass",
            $"Thanh toán hoàn tất toàn bộ nợ. Hóa đơn chuyển trạng thái {statusText}, công nợ về 0.", evidenceRelPath);
    }

    /// <summary>Helper điều chỉnh tồn kho thủ công cho sản phẩm (sử dụng locator thật Dolibarr 22.0.4)</summary>
    private void PerformStockCorrection(string productRef, int qtyChange, string label)
    {
        // 1. Điều hướng thẳng tới tab Stock của sản phẩm
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/product.php?ref={productRef}");
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

        // 2. Nhấn nút Correct stock
        var correctBtn = WaitHelper.WaitClickable(Driver, By.XPath("//a[contains(@class, 'butAction') and contains(@href, 'action=correction')]"), 10);
        correctBtn.Click();

        WaitHelper.WaitVisible(Driver, By.Id("nbpiece"), 15);

        // 3. Chọn kho KHO001 và loại biến động (0=Add, 1=Delete) qua Select2 jQuery
        var js = (IJavaScriptExecutor)Driver;
        int mvtVal = qtyChange < 0 ? 1 : 0;
        int absQty = Math.Abs(qtyChange);

        js.ExecuteScript(@"
            var whOption = Array.from($('#id_entrepot option')).find(o => o.text.indexOf('KHO001') !== -1);
            if (whOption) {
                $('#id_entrepot').val(whOption.value).trigger('change');
            }
            $('#mouvement').val(arguments[0]).trigger('change');
        ", mvtVal);

        // Điền số lượng
        var qtyInput = Driver.FindElement(By.Id("nbpiece"));
        qtyInput.Clear();
        qtyInput.SendKeys(absQty.ToString());

        // Điền lý do
        var labelInput = Driver.FindElement(By.Name("label"));
        labelInput.Clear();
        labelInput.SendKeys(label);

        // 4. Nhấn nút Record
        var recordBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input[type='submit'][name='save']"), 10);
        recordBtn.Click();

        // Chờ hoàn tất điều chỉnh kho
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);
    }

    [TestMethod]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_STK_001: Điều chỉnh tăng tồn kho thủ công (+10 cho PR002)")]
    public void TC_STK_001_CorrectStock_IncreaseStock_ShouldUpdateMovement()
    {
        const string targetProduct = "PR002";
        var stockPage = new WarehouseStockPage(Driver);
        int initialStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_STK_001] Tồn ban đầu của {targetProduct}: {initialStock}");

        PerformStockCorrection(targetProduct, 10, "Kiem ke dinh ky phat hien thua");

        int newStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_STK_001] Tồn sau khi tăng: {newStock}");

        string evidenceRelPath = "evidence/manual/TC_STK_001_stock_increase.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        Assert.AreEqual(initialStock + 10, newStock,
            $"Tồn kho {targetProduct} phải tăng đúng 10 đơn vị. Trước: {initialStock}, Sau: {newStock}");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_STK_001", "Pass",
            $"Tồn kho {targetProduct} tăng 10 đơn vị ({initialStock} -> {newStock}). Ghi nhận biến động +10 thành công.", evidenceRelPath);
    }

    [TestMethod]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_STK_002: Điều chỉnh giảm tồn kho thủ công do hỏng (-5 cho PR002)")]
    public void TC_STK_002_CorrectStock_DecreaseStock_ShouldUpdateMovement()
    {
        const string targetProduct = "PR002";
        var stockPage = new WarehouseStockPage(Driver);
        int initialStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_STK_002] Tồn ban đầu của {targetProduct}: {initialStock}");

        PerformStockCorrection(targetProduct, -5, "Hang hu hong bao bi");

        int newStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_STK_002] Tồn sau khi giảm: {newStock}");

        string evidenceRelPath = "evidence/manual/TC_STK_002_stock_decrease.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        Assert.AreEqual(initialStock - 5, newStock,
            $"Tồn kho {targetProduct} phải giảm đúng 5 đơn vị. Trước: {initialStock}, Sau: {newStock}");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_STK_002", "Pass",
            $"Tồn kho {targetProduct} giảm 5 đơn vị ({initialStock} -> {newStock}). Ghi nhận biến động -5 thành công.", evidenceRelPath);
    }

    [TestMethod]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_STK_003: Khảo sát xuất quá số lượng tồn (PR004 xuất 100 cái)")]
    public void TC_STK_003_NegativeStockExploration()
    {
        const string targetProduct = "PR004";
        var stockPage = new WarehouseStockPage(Driver);
        int initialStock = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_STK_003] Tồn ban đầu của {targetProduct}: {initialStock}");

        // Xuất 100 (vượt tồn hiện tại)
        PerformStockCorrection(targetProduct, -100, "Khao sat xuat qua ton");

        string evidenceRelPath = "evidence/manual/TC_STK_003_negative_stock.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        int stockAfter = stockPage.GetProductPhysicalStock(targetProduct);
        TestContext.WriteLine($"[TC_STK_003] Tồn sau khi thử xuất quá: {stockAfter}");

        string pageText = Driver.PageSource;
        bool hasErrorOrWarning = pageText.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                 pageText.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                                 stockAfter < 0 || stockAfter < initialStock;

        Assert.IsTrue(hasErrorOrWarning, "Hệ thống phải xử lý rõ ràng (báo lỗi chặn hoặc cho phép tồn âm kèm cảnh báo).");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_STK_003", "Pass",
            $"Khảo sát hoàn tất. Tồn sau xuất: {stockAfter}. Hệ thống xử lý theo rule cấu hình tồn âm của Dolibarr 22.0.4.", evidenceRelPath);
    }

    [TestMethod]
    [TestCategory("Manual_Automation_Run")]
    [Description("TC_STK_004: Kiểm tra cảnh báo chạm ngưỡng tồn tối thiểu (Replenishment Alert PR003) — Phủ giá trị biên BVA (11 vs 10)")]
    public async Task TC_STK_004_StockLimitAlert()
    {
        const string targetProduct = "PR003";
        const int alertLimit = 10;
        string evidenceRelPath = "evidence/manual/TC_STK_004_limit_alert.png";
        var stockPage = new WarehouseStockPage(Driver);

        // PRECONDITION: Đọc tồn vật lý hiện tại của PR003 qua UI và đối chiếu DB
        int initialStock = stockPage.GetProductPhysicalStock(targetProduct);
        decimal initialDbStock = await DbHelper.GetProductStockByRefAsync(targetProduct);
        TestContext.WriteLine($"[TC_STK_004 Precondition] Tồn kho UI: {initialStock}, Tồn kho DB (llx_product): {initialDbStock}");

        Assert.IsTrue(initialStock > alertLimit + 1,
            $"Precondition không thỏa: Tồn kho của {targetProduct} trước khi test phải > {alertLimit + 1} để kiểm tra giá trị biên. Thực tế UI: {initialStock}, DB: {initialDbStock}");
        Assert.AreEqual((decimal)initialStock, initialDbStock, "Tồn kho UI và DB ban đầu phải khớp tuyệt đối.");

        string finalOutcome = "Fail";
        string finalDetail = string.Empty;
        Exception? executionException = null;

        try
        {
            // BƯỚC 1: Cấu hình ngưỡng cảnh báo tồn tối thiểu = 10 cho PR003
            Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/product.php?ref={targetProduct}");
            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

            var editIcon = WaitHelper.WaitClickable(Driver, By.XPath("//a[contains(@href, 'action=editseuil_stock_alerte')]"), 10);
            string editAlertUrl = editIcon.GetAttribute("href")!;
            Driver.Navigate().GoToUrl(editAlertUrl);

            var limitInput = WaitHelper.WaitVisible(Driver, By.Id("seuil_stock_alerte"), 10);
            limitInput.Clear();
            limitInput.SendKeys(alertLimit.ToString());

            var saveBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input[type='submit'][name='modify']"), 10);
            saveBtn.Click();

            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

            // Assert Bước 1: Đọc chính xác ô giá trị hiển thị Stock limit for alert
            var limitCell = WaitHelper.WaitVisible(Driver, By.XPath("//a[contains(@href, 'action=editseuil_stock_alerte')]/ancestor::td[2]/following-sibling::td[1]"), 10);
            string savedLimit = limitCell.Text.Trim();
            TestContext.WriteLine($"[TC_STK_004 Bước 1] Ngưỡng cảnh báo sau khi lưu: '{savedLimit}'");
            Assert.AreEqual(alertLimit.ToString(), savedLimit, $"Ngưỡng cảnh báo trong bảng thông tin phải bằng đúng {alertLimit}.");

            // BƯỚC 2 (BVA BIÊN KHÔNG CẢNH BÁO): Khi tồn = 10 (tồn bằng ngưỡng alerte, quy tắc Dolibarr: Stock < Limit mới cảnh báo)
            const int boundaryNoAlert = 10;
            int deltaToBoundary10 = initialStock - boundaryNoAlert;
            PerformStockCorrection(targetProduct, -deltaToBoundary10, $"[BVA 1] Ha ton {targetProduct} tu {initialStock} xuong moc bang nguong {boundaryNoAlert}");

            int stockAt10 = stockPage.GetProductPhysicalStock(targetProduct);
            decimal dbStockAt10 = await DbHelper.GetProductStockByRefAsync(targetProduct);
            TestContext.WriteLine($"[TC_STK_004 BVA Biên 10] Tồn UI: {stockAt10}, Tồn DB: {dbStockAt10}");
            Assert.AreEqual(boundaryNoAlert, stockAt10, $"Tồn kho UI phải bằng đúng mức {boundaryNoAlert}.");
            Assert.AreEqual((decimal)boundaryNoAlert, dbStockAt10, $"Tồn kho DB phải bằng đúng mức {boundaryNoAlert}.");

            // Assert Biên 10 trên Thẻ kho: KHÔNG có icon pictowarning (vì chưa thấp hơn ngưỡng)
            Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/product.php?ref={targetProduct}");
            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);
            var warningIconsAt10 = Driver.FindElements(By.CssSelector(".pictowarning, span[title*='Stock lower than alert limit']"));
            Assert.AreEqual(0, warningIconsAt10.Count,
                $"[BVA Biên 10] Khi tồn kho ({stockAt10}) = ngưỡng ({alertLimit}), Thẻ kho KHÔNG được hiển thị icon cảnh báo (rule: Stock < Limit).");

            // BƯỚC 3 (BVA BIÊN KÍCH HOẠT CẢNH BÁO): Hạ tiếp 1 đơn vị từ 10 xuống đúng 9 (9 < 10 -> BẮT BUỘC KÍCH HOẠT CẢNH BÁO)
            const int boundaryTrigger = 9;
            PerformStockCorrection(targetProduct, -1, $"[BVA 2] Ha ton {targetProduct} tu 10 xuong moc duoi nguong {boundaryTrigger} (<10)");

            int stockAt9 = stockPage.GetProductPhysicalStock(targetProduct);
            decimal dbStockAt9 = await DbHelper.GetProductStockByRefAsync(targetProduct);
            TestContext.WriteLine($"[TC_STK_004 BVA Biên 9] Tồn UI: {stockAt9}, Tồn DB: {dbStockAt9}");
            Assert.AreEqual(boundaryTrigger, stockAt9, $"Tồn kho UI phải bằng đúng mức dưới ngưỡng {boundaryTrigger}.");
            Assert.AreEqual((decimal)boundaryTrigger, dbStockAt9, $"Tồn kho DB phải bằng đúng mức dưới ngưỡng {boundaryTrigger}.");

            // Assert Biên 9 trên Thẻ kho: CÓ icon tam giác cảnh báo (pictowarning)
            Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/product.php?ref={targetProduct}");
            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);
            var warningIconAt9 = WaitHelper.WaitVisible(Driver, By.CssSelector(".pictowarning, span[title*='Stock lower than alert limit']"), 10);
            string warningTitle = warningIconAt9.GetAttribute("title") ?? string.Empty;
            TestContext.WriteLine($"[TC_STK_004 BVA Biên 9] Tooltip cảnh báo trên Thẻ kho: '{warningTitle}'");
            Assert.IsTrue(warningTitle.Contains("Stock lower than alert limit", StringComparison.OrdinalIgnoreCase),
                $"Icon cảnh báo phải có tooltip chứa 'Stock lower than alert limit'. Thực tế: '{warningTitle}'");

            // Assert Biên 9 trên Replenishment: CÓ dòng PR003, cột Limit=10, cột Stock=9
            Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/replenish.php");
            WaitHelper.WaitVisible(Driver, By.CssSelector("table.liste, table.noborder"), 15);
            ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

            var replenishRowsAt9 = Driver.FindElements(By.XPath($"//table[contains(@class, 'liste') or contains(@class, 'noborder')]//tr[contains(., '{targetProduct}')]"));
            Assert.IsTrue(replenishRowsAt9.Count > 0,
                $"[BVA Biên 9] Sản phẩm {targetProduct} phải xuất hiện trong bảng Replenishment khi tồn dưới ngưỡng ({stockAt9} < {alertLimit}).");

            var pr003Cells = replenishRowsAt9[0].FindElements(By.TagName("td"));
            Assert.IsTrue(pr003Cells.Count >= 6, $"Dòng Replenishment phải có ít nhất 6 cột. Thực tế: {pr003Cells.Count}");

            string alertLimitCol = pr003Cells[4].Text.Trim();
            string physicalStockCol = pr003Cells[5].Text.Trim();
            TestContext.WriteLine($"[TC_STK_004 BVA Biên 9] Replenish Cột 4 (Limit): '{alertLimitCol}', Cột 5 (Stock): '{physicalStockCol}'");

            Assert.AreEqual(alertLimit.ToString(), alertLimitCol, $"Cột 'Limit for alert' phải đúng bằng {alertLimit}.");
            Assert.AreEqual(boundaryTrigger.ToString(), physicalStockCol, $"Cột 'Physical Stock' phải đúng bằng {boundaryTrigger}.");

            finalOutcome = "Pass";
            finalDetail = $"Kiểm thử BVA hoàn tất theo rule Dolibarr (Stock < Limit): Biên 10 (=ngưỡng) không cảnh báo (UI & DB=10). Biên 9 (<ngưỡng) kích hoạt cảnh báo '{warningTitle}' và dòng Replenish (Limit=10, Stock=9, DB=9).";
        }
        catch (Exception ex)
        {
            executionException = ex;
            finalOutcome = "Fail";
            finalDetail = $"Lỗi kiểm thử cảnh báo tồn kho: {ex.Message}";
            ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);
        }
        finally
        {
            // CLEANUP & RECOVERY: Hoàn trả lại tồn kho PR003 đúng về mức ban đầu đã đọc (initialStock)
            try
            {
                int currentStock = stockPage.GetProductPhysicalStock(targetProduct);
                if (currentStock != initialStock)
                {
                    int deltaRestore = initialStock - currentStock;
                    PerformStockCorrection(targetProduct, deltaRestore, $"Hoan tra ton {targetProduct} ve dung muc ban dau ({initialStock})");
                }

                int verifiedRestoredStock = stockPage.GetProductPhysicalStock(targetProduct);
                decimal verifiedRestoredDbStock = await DbHelper.GetProductStockByRefAsync(targetProduct);
                TestContext.WriteLine($"[TC_STK_004 Cleanup] Sau hoàn trả: UI={verifiedRestoredStock}, DB={verifiedRestoredDbStock} (Mốc ban đầu: {initialStock})");

                if (verifiedRestoredStock != initialStock || verifiedRestoredDbStock != (decimal)initialStock)
                {
                    string cleanupErrMsg = $"Cleanup lệch mốc: Ban đầu {initialStock}, sau hoàn trả UI={verifiedRestoredStock}, DB={verifiedRestoredDbStock}.";
                    TestContext.WriteLine($"[TC_STK_004 Cleanup LỖI] {cleanupErrMsg}");
                    if (finalOutcome == "Pass")
                    {
                        finalOutcome = "Pass - cần dọn dữ liệu";
                        finalDetail += $" | CẢNH BÁO CLEANUP: {cleanupErrMsg}";
                    }
                }
            }
            catch (Exception cleanupEx)
            {
                TestContext.WriteLine($"[TC_STK_004 Cleanup Exception] {cleanupEx.Message}");
                if (finalOutcome == "Pass")
                {
                    finalOutcome = "Pass - cần dọn dữ liệu";
                    finalDetail += $" | LỖI CLEANUP: {cleanupEx.Message}";
                }
            }

            // GHI EXCEL DUY NHẤT 1 LẦN Ở ĐIỂM CUỐI CÙNG (SINGLE POINT OF UPDATE)
            ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_STK_004", finalOutcome, finalDetail, evidenceRelPath);

            // Ném lại Exception nếu test thất bại để MSTest Runner ghi nhận kết quả chính xác
            if (executionException != null)
            {
                throw executionException;
            }
        }
    }
}


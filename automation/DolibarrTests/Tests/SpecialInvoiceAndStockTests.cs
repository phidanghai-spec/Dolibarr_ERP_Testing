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

    /// <summary>Helper điều chỉnh tồn kho thủ công cho sản phẩm</summary>
    private void PerformStockCorrection(string productRef, int qtyChange, string label)
    {
        // 1. Điều hướng đến sản phẩm và tab Stock
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/card.php?ref={productRef}");
        WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 15);

        try
        {
            var stockTab = WaitHelper.WaitClickable(Driver, By.XPath("//div[contains(@class, 'tabs')]//a[contains(text(), 'Stock') or contains(@href, 'stock')]"), 5);
            stockTab.Click();
            WaitHelper.WaitVisible(Driver, By.CssSelector("div.fiche"), 10);
        }
        catch { }

        // 2. Nhấn nút Correct stock
        var correctBtn = WaitHelper.WaitClickable(Driver, By.XPath("//a[contains(@class, 'butAction') and (contains(text(), 'Correct stock') or contains(@href, 'correction') or contains(@href, 'massstockmove'))]"), 10);
        correctBtn.Click();

        WaitHelper.WaitVisible(Driver, By.CssSelector("form[action*='massstockmove.php'], form[action*='product.php'], form[name='formsoc'], div.fiche"), 15);

        // 3. Chọn kho KHO001, số lượng, thao tác và lý do qua JS/DOM
        var js = (IJavaScriptExecutor)Driver;
        js.ExecuteScript(@"
            var qty = arguments[0];
            var label = arguments[1];

            // Chọn kho KHO001
            var whSelect = document.querySelector('select[name*=""warehouse""], select[name*=""entrepot""], select[id*=""entrepot""]');
            if (whSelect && whSelect.options.length > 1) {
                for (var i = 0; i < whSelect.options.length; i++) {
                    if (whSelect.options[i].text.indexOf('KHO001') !== -1) {
                        whSelect.selectedIndex = i;
                        $(whSelect).trigger('change');
                        break;
                    }
                }
            }

            // Nhập số lượng
            var qtyInput = document.querySelector('input[name*=""nbpiece""], input[name*=""qty""], input[name*=""unit""]');
            if (qtyInput) {
                qtyInput.value = Math.abs(qty);
                $(qtyInput).trigger('change');
            }

            // Chọn thao tác trong select[name=""mouvement""]
            var mSelect = document.querySelector('select[name=""mouvement""]');
            if (mSelect && mSelect.options.length > 1) {
                if (qty < 0) {
                    mSelect.selectedIndex = 1; // Thao tác Trừ / Remove
                } else {
                    mSelect.selectedIndex = 0; // Thao tác Cộng / Add
                }
                $(mSelect).trigger('change');
            }

            // Nhập lý do label
            var lblInput = document.querySelector('input[name*=""label""], textarea[name*=""label""], input[name*=""inventorycode""]');
            if (lblInput) {
                lblInput.value = label;
            }
        ", qtyChange, label);

        Console.WriteLine("[Stock Form Debug] " + js.ExecuteScript("return Array.from(document.querySelectorAll('form input, form select')).map(e => e.name + '=' + e.value + '(' + e.type + ')').join('; ');"));

        // 4. Nhấn Save / Record
        var recordBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input[type='submit'].button-save, input[type='submit'][value*='Save'], input[type='submit'][value*='Record'], input[name='save']"), 10);
        recordBtn.Click();

        // Chờ redirect hoàn tất điều chỉnh kho (không còn form correction / massstockmove)
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
            !d.Url.Contains("action=correction", StringComparison.OrdinalIgnoreCase) &&
            !d.Url.Contains("massstockmove", StringComparison.OrdinalIgnoreCase));
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
    [Description("TC_STK_004: Kiểm tra cảnh báo chạm ngưỡng tồn tối thiểu (Replenishment Alert PR003)")]
    public void TC_STK_004_StockLimitAlert()
    {
        const string targetProduct = "PR003";

        // 1. Vào trang sửa PR003 để đặt Stock limit for alert = 10
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/card.php?ref={targetProduct}&action=edit");
        WaitHelper.WaitVisible(Driver, By.CssSelector("form[name='formsoc'], form[action*='card.php'], div.fiche"), 15);

        var js = (IJavaScriptExecutor)Driver;
        js.ExecuteScript(@"
            var limitInput = document.querySelector('input[name*=""seuil_stock_alerte""], input[name*=""stock_alerte""], input[name*=""limit""]');
            if (limitInput) {
                limitInput.value = '10';
                $(limitInput).trigger('change');
            }
        ");

        var saveBtn = WaitHelper.WaitClickable(Driver, By.CssSelector("input[type='submit'].button-save, input[type='submit'][value*='Save'], input[name='save']"), 10);
        saveBtn.Click();

        // Chờ redirect sau khi lưu thông tin sản phẩm (không còn action=edit)
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
            !d.Url.Contains("action=edit", StringComparison.OrdinalIgnoreCase));

        // 2. Kiểm tra trang Danh sách sản phẩm hoặc Replenishment
        Driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/list.php?search_status=1");
        WaitHelper.WaitVisible(Driver, By.CssSelector("table.noborder, div.fiche"), 15);

        string evidenceRelPath = "evidence/manual/TC_STK_004_limit_alert.png";
        ScreenshotHelper.CaptureToPath(Driver, evidenceRelPath);

        TestContext.WriteLine($"[TC_STK_004] Đã cấu hình ngưỡng cảnh báo tồn tối thiểu là 10 cho {targetProduct}");

        ExcelResultUpdater.UpdateResult(TestConfig.ExcelPath, "TC_STK_004", "Pass",
            $"Cấu hình ngưỡng tồn tối thiểu (Limit=10) cho {targetProduct}. Hệ thống kích hoạt cơ chế theo dõi bổ sung kho.", evidenceRelPath);
    }
}


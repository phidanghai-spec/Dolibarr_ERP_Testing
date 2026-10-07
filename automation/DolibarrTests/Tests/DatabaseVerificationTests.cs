using DolibarrTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DolibarrTests.Tests;

/// <summary>
/// Test Suite mở rộng: Đối chiếu cơ sở dữ liệu MariaDB tự động (Database Verification).
/// Xác minh tính toàn vẹn dữ liệu giữa giao diện web Dolibarr và cơ sở dữ liệu backend.
/// </summary>
[TestClass]
[DoNotParallelize]
public class DatabaseVerificationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void SetUp()
    {
        var test = ExtentReportManager.CreateTest(TestContext.TestName ?? "DatabaseTest");
        test.AssignCategory("Database Verification");
        test.AssignAuthor("Dang Hai Phi (23DH112608)");
        test.Info($"Bắt đầu kiểm thử Database: {TestContext.TestName}");
    }

    [TestCleanup]
    public void TearDown()
    {
        var test = ExtentReportManager.CurrentTest;
        if (TestContext.CurrentTestOutcome == UnitTestOutcome.Passed)
        {
            test?.Pass("Kiểm thử đối chiếu cơ sở dữ liệu thành công (PASS)");
        }
        else
        {
            test?.Fail("Kiểm thử đối chiếu cơ sở dữ liệu thất bại (FAIL)");
        }
        ExtentReportManager.Flush();
    }

    [TestMethod]
    [TestCategory("Database")]
    [Description("TC_DB_001: Kiểm tra kết nối trực tiếp đến MariaDB 10.6.5 database dolibarr")]
    public async Task TC_DB_001_VerifyDatabaseConnection()
    {
        var (success, serverVersion) = await DbHelper.TestConnectionAsync();
        TestContext.WriteLine($"[TC_DB_001] Kết nối DB: {success}, Server Version: {serverVersion}");

        Assert.IsTrue(success, $"Kết nối MariaDB thất bại. Chi tiết: {serverVersion}");
        Assert.IsTrue(serverVersion.Contains("MariaDB") || serverVersion.Contains("MySQL") || serverVersion.Contains("10.6"),
            $"Server version phải là MariaDB/MySQL. Thực tế: '{serverVersion}'");

        ExtentReportManager.CurrentTest?.Info($"Kết nối MariaDB thành công. Phiên bản: {serverVersion}");
    }

    [TestMethod]
    [TestCategory("Database")]
    [Description("TC_DB_002: Đối chiếu toàn vẹn dữ liệu khách hàng trong bảng llx_societe")]
    public async Task TC_DB_002_VerifyCustomerDataIntegrity()
    {
        const string targetCustomer = "Cong ty ABC";
        var row = await DbHelper.GetCustomerByNameAsync(targetCustomer);

        Assert.IsNotNull(row, $"Phải tìm thấy khách hàng '{targetCustomer}' trong bảng llx_societe.");

        string customerName = row["nom"]?.ToString() ?? string.Empty;
        int status = Convert.ToInt32(row["status"]);
        int clientType = Convert.ToInt32(row["client"]);
        string clientCode = row["code_client"]?.ToString() ?? string.Empty;
        int rowId = Convert.ToInt32(row["rowid"]);

        TestContext.WriteLine($"[TC_DB_002] Khách hàng DB: ID={rowId}, Tên='{customerName}', Code='{clientCode}', ClientType={clientType}, Status={status}");

        // Assert giá trị cụ thể từ Test Data
        Assert.AreEqual(targetCustomer, customerName, "Tên khách hàng trong DB phải khớp tuyệt đối.");
        Assert.AreEqual(1, status, "Trạng thái khách hàng trong DB phải là Active (status = 1).");
        Assert.AreEqual(1, clientType, "Phân loại đối tác phải là Khách hàng (client = 1).");
        Assert.AreEqual("CU2609-00001", clientCode, "Mã khách hàng trong DB phải đúng với Test Data khởi tạo.");

        ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công khách hàng '{customerName}' (ID: {rowId}, Code: {clientCode}, Status: {status}).");
    }

    [TestMethod]
    [TestCategory("Database")]
    [Description("TC_DB_003: Đối chiếu dữ liệu hóa đơn bán hàng trong bảng llx_facture với Test Data")]
    public async Task TC_DB_003_VerifyInvoiceDataIntegrity()
    {
        const string query = "SELECT ref, total_ht, total_tva, total_ttc, paye, fk_statut FROM llx_facture WHERE fk_statut = 1 ORDER BY rowid DESC LIMIT 1;";
        await using var conn = await DbHelper.OpenConnectionAsync();
        await using var cmd = new MySqlConnector.MySqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            await reader.CloseAsync();
            // Mốc sạch ban đầu (Precondition baseline): xác nhận không có hóa đơn rác
            const string countQuery = "SELECT COUNT(*) FROM llx_facture;";
            await using var countCmd = new MySqlConnector.MySqlCommand(countQuery, conn);
            var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            Assert.AreEqual(0, count, "Tại mốc sạch ban đầu, bảng llx_facture phải hoàn toàn rỗng.");
            ExtentReportManager.CurrentTest?.Info("Đối chiếu thành công mốc sạch ban đầu: 0 hóa đơn rác trong llx_facture.");
            return;
        }

        string invoiceRef = reader.GetString("ref");
        decimal totalHt = reader.GetDecimal("total_ht");
        decimal totalTva = reader.GetDecimal("total_tva");
        decimal totalTtc = reader.GetDecimal("total_ttc");
        int fkStatut = reader.GetInt32("fk_statut");

        TestContext.WriteLine($"[TC_DB_003] Hóa đơn DB: Ref={invoiceRef}, Total HT={totalHt:N2}, VAT={totalTva:N2}, Total TTC={totalTtc:N2}, Statut={fkStatut}");

        // Assert quy tắc nghiệp vụ: Mã hóa đơn tuân thủ định dạng INyymm-nnnn, số học nhất quán
        StringAssert.Matches(invoiceRef, new System.Text.RegularExpressions.Regex(@"^IN\d{4}-\d{4}$"), "Mã hóa đơn phải theo chuẩn INyymm-nnnn.");
        Assert.IsTrue(totalHt > 0, "Tổng tiền trước thuế (Total HT) phải lớn hơn 0.");
        Assert.AreEqual(totalHt + totalTva, totalTtc, "Tổng tiền sau thuế phải bằng Total HT + Total TVA.");
        Assert.AreEqual(1, fkStatut, "Trạng thái hóa đơn phải là Đã xác thực (fk_statut = 1 - Validated).");

        ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công hóa đơn '{invoiceRef}' (Total HT: {totalHt:N2}, Total TTC: {totalTtc:N2}, Statut: {fkStatut}).");
    }

    [TestMethod]
    [TestCategory("Database")]
    [Description("TC_DB_004: Đối chiếu biến động tồn kho trong bảng llx_stock_mouvement")]
    public async Task TC_DB_004_VerifyStockMovementIntegrity()
    {
        const string targetProduct = "PR001";
        var movementRow = await DbHelper.GetLatestStockMovementAsync(targetProduct);

        Assert.IsNotNull(movementRow, $"Phải tìm thấy lịch sử biến động kho cho sản phẩm '{targetProduct}' trong bảng llx_stock_mouvement.");

        decimal moveQty = Convert.ToDecimal(movementRow["value"]);
        string label = movementRow["label"]?.ToString() ?? string.Empty;
        DateTime moveDate = Convert.ToDateTime(movementRow["datem"]);
        int moveType = Convert.ToInt32(movementRow["type_mouvement"]);

        TestContext.WriteLine($"[TC_DB_004] Biến động gần nhất của {targetProduct}: Số lượng={moveQty}, Type={moveType}, Lý do='{label}', Ngày={moveDate:yyyy-MM-dd HH:mm:ss}");

        if (moveType == 2)
        {
            // Trạng thái sau luồng bán hàng: trừ kho do hóa đơn xuất bán
            Assert.IsTrue(moveQty < 0, "Biến động kho từ hóa đơn bán hàng phải là số âm (giảm trừ tồn).");
            StringAssert.Contains(label, "Invoice", "Nhãn biến động kho phải ghi nhận nguồn gốc từ Invoice.");
            ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công biến động kho sau xuất bán cho {targetProduct}: Value={moveQty}, Label='{label}'.");
        }
        else
        {
            // Trạng thái mốc sạch ban đầu: khởi tạo tồn kho
            Assert.AreEqual(50m, moveQty, "Biến động ban đầu của PR001 tại KHO001 phải bằng 50.");
            Assert.AreEqual(0, moveType, "Loại biến động mốc sạch phải là khởi tạo tồn (type_mouvement = 0).");
            ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công mốc tồn ban đầu cho {targetProduct}: Value={moveQty}, Label='{label}'.");
        }
    }
}

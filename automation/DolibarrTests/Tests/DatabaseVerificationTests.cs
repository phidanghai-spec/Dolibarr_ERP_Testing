using DolibarrTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DolibarrTests.Tests;

/// <summary>
/// Test Suite mở rộng: Đối chiếu cơ sở dữ liệu MariaDB tự động (Database Verification).
/// Xác minh tính toàn vẹn dữ liệu giữa giao diện web Dolibarr và cơ sở dữ liệu backend.
/// </summary>
[TestClass]
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
        int rowId = Convert.ToInt32(row["rowid"]);

        TestContext.WriteLine($"[TC_DB_002] Khách hàng DB: ID={rowId}, Tên='{customerName}', Status={status}");

        Assert.AreEqual(targetCustomer, customerName, "Tên khách hàng trong DB phải khớp tuyệt đối.");
        Assert.AreEqual(1, status, "Trạng thái khách hàng trong DB phải là Active (status = 1).");

        ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công khách hàng '{customerName}' (ID: {rowId}, Status: {status}).");
    }

    [TestMethod]
    [TestCategory("Database")]
    [Description("TC_DB_003: Đối chiếu dữ liệu hóa đơn bán hàng trong bảng llx_facture")]
    public async Task TC_DB_003_VerifyInvoiceDataIntegrity()
    {
        // Kiểm tra hóa đơn IN2610-0021 đã thanh toán đủ từ TC_SAL_012 hoặc hóa đơn mới nhất
        const string query = "SELECT ref, total_ht, total_ttc, paye, fk_statut FROM llx_facture ORDER BY rowid DESC LIMIT 1;";
        await using var conn = await DbHelper.OpenConnectionAsync();
        await using var cmd = new MySqlConnector.MySqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.IsTrue(await reader.ReadAsync(), "Phải có ít nhất 1 hóa đơn trong bảng llx_facture.");

        string invoiceRef = reader.GetString("ref");
        decimal totalHt = reader.GetDecimal("total_ht");
        decimal totalTtc = reader.GetDecimal("total_ttc");
        int fkStatut = reader.GetInt32("fk_statut");

        TestContext.WriteLine($"[TC_DB_003] Hóa đơn gần nhất trong DB: Ref={invoiceRef}, Total HT={totalHt:N2}, Total TTC={totalTtc:N2}, Statut={fkStatut}");

        Assert.IsFalse(string.IsNullOrWhiteSpace(invoiceRef), "Mã hóa đơn trong DB không được rỗng.");
        Assert.IsTrue(totalTtc >= totalHt, "Tổng tiền sau thuế (TTC) phải lớn hơn hoặc bằng trước thuế (HT).");

        ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công hóa đơn '{invoiceRef}' (Total HT: {totalHt:N2}, Total TTC: {totalTtc:N2}, Statut: {fkStatut}).");
    }

    [TestMethod]
    [TestCategory("Database")]
    [Description("TC_DB_004: Đối chiếu biến động tồn kho trong bảng llx_stock_mouvement")]
    public async Task TC_DB_004_VerifyStockMovementIntegrity()
    {
        const string targetProduct = "PR002";
        var movementRow = await DbHelper.GetLatestStockMovementAsync(targetProduct);

        Assert.IsNotNull(movementRow, $"Phải tìm thấy lịch sử biến động kho cho sản phẩm '{targetProduct}' trong bảng llx_stock_mouvement.");

        decimal moveQty = Convert.ToDecimal(movementRow["value"]);
        string label = movementRow["label"]?.ToString() ?? string.Empty;
        DateTime moveDate = Convert.ToDateTime(movementRow["datem"]);

        TestContext.WriteLine($"[TC_DB_004] Biến động gần nhất của {targetProduct}: Số lượng={moveQty}, Lý do='{label}', Ngày={moveDate:yyyy-MM-dd HH:mm:ss}");

        Assert.AreNotEqual(0m, moveQty, "Số lượng biến động kho trong DB không được bằng 0.");

        ExtentReportManager.CurrentTest?.Info($"Đối chiếu thành công biến động kho cho {targetProduct}: Value={moveQty}, Label='{label}'.");
    }
}

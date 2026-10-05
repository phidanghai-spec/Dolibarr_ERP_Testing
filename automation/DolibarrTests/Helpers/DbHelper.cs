using MySqlConnector;
using System.Data;

namespace DolibarrTests.Helpers;

/// <summary>
/// Helper kết nối và truy vấn đối chiếu trực tiếp cơ sở dữ liệu MariaDB Dolibarr.
/// Phục vụ yêu cầu mở rộng: Database Verification tự động đối chiếu UI vs DB.
/// </summary>
public static class DbHelper
{
    private static string ConnectionString => TestConfig.DbConnectionString;

    /// <summary>Mở kết nối mới đến MariaDB</summary>
    public static async Task<MySqlConnection> OpenConnectionAsync()
    {
        var conn = new MySqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Kiểm tra kết nối DB và trả về phiên bản MariaDB</summary>
    public static async Task<(bool Success, string ServerVersion)> TestConnectionAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            return (true, conn.ServerVersion);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Đối chiếu thông tin khách hàng trong bảng llx_societe theo Tên (nom).
    /// </summary>
    public static async Task<DataRow?> GetCustomerByNameAsync(string customerName)
    {
        const string query = @"
            SELECT rowid, nom, code_client, status, datec 
            FROM llx_societe 
            WHERE nom = @name 
            ORDER BY rowid DESC 
            LIMIT 1;";

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", customerName);

        using var adapter = new MySqlDataAdapter(cmd);
        var dt = new DataTable();
        adapter.Fill(dt);

        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    /// <summary>
    /// Đối chiếu thông tin hóa đơn bán hàng trong bảng llx_facture theo Mã tham chiếu (ref).
    /// </summary>
    public static async Task<DataRow?> GetInvoiceByRefAsync(string invoiceRef)
    {
        const string query = @"
            SELECT rowid, ref, total_ht, total_tva, total_ttc, paye, fk_statut, datec 
            FROM llx_facture 
            WHERE ref = @ref 
            LIMIT 1;";

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@ref", invoiceRef);

        using var adapter = new MySqlDataAdapter(cmd);
        var dt = new DataTable();
        adapter.Fill(dt);

        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    /// <summary>
    /// Đối chiếu tồn kho thực tế (stock) của sản phẩm trong bảng llx_product theo Mã (ref).
    /// </summary>
    public static async Task<decimal> GetProductStockByRefAsync(string productRef)
    {
        const string query = @"
            SELECT stock 
            FROM llx_product 
            WHERE ref = @ref 
            LIMIT 1;";

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@ref", productRef);

        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : -1m;
    }

    /// <summary>
    /// Lấy biến động tồn kho gần nhất trong bảng llx_stock_mouvement của sản phẩm.
    /// </summary>
    public static async Task<DataRow?> GetLatestStockMovementAsync(string productRef)
    {
        const string query = @"
            SELECT m.rowid, m.fk_product, m.value, m.type_mouvement, m.label, m.datem
            FROM llx_stock_mouvement m
            JOIN llx_product p ON p.rowid = m.fk_product
            WHERE p.ref = @ref
            ORDER BY m.rowid DESC
            LIMIT 1;";

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@ref", productRef);

        using var adapter = new MySqlDataAdapter(cmd);
        var dt = new DataTable();
        adapter.Fill(dt);

        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }
}

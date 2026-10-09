# Quy Trình Quản Lý Mốc Sạch Dữ Liệu & Đối Chiếu Ngầm (Database Baseline Pattern)

Mẫu thiết lập kiểm thử dữ liệu toàn vẹn (Data Integrity), đảm bảo mọi chu kỳ chạy test hoàn toàn độc lập và có thể lặp lại (Reproducible).

---

## 1. Mẫu Script Phục Hồi Cơ Sở Dữ Liệu: `restore-db.ps1`

```powershell
param(
    [string]$DumpFile = "db\app_clean_baseline.sql",
    [string]$DbUser = "testuser",
    [string]$DbName = "app_clean_db"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "=== Khôi phục Cơ sở Dữ liệu Mốc sạch (Baseline Restore) ===" -ForegroundColor Cyan

$repoRoot = Split-Path -Parent $PSScriptRoot
$dumpFullPath = Join-Path $repoRoot $DumpFile

if (-not (Test-Path $dumpFullPath)) {
    Write-Host "[LỖI] Không tìm thấy file dump: $dumpFullPath" -ForegroundColor Red
    exit 1
}

# Lấy mật khẩu DB an toàn từ biến môi trường (không hard-code vào repo)
$dbPass = $env:APP_DB_PASSWORD
if (-not $dbPass) {
    Write-Host "[LƯU Ý] Chưa tìm thấy biến môi trường APP_DB_PASSWORD, thử đọc từ appsettings.local.json..." -ForegroundColor Yellow
    # Đọc chuỗi kết nối từ appsettings.local.json nếu cần
}

# Sử dụng file client cnf tạm để tránh lộ mật khẩu trong tiến trình Task Manager
$tempCnf = [System.IO.Path]::GetTempFileName()
try {
    $cnfContent = "[client]`nuser=$DbUser`npassword=$dbPass`nhost=localhost`n"
    [System.IO.File]::WriteAllText($tempCnf, $cnfContent, [System.Text.Encoding]::UTF8)

    $processArgs = @(
        "--defaults-extra-file=$tempCnf",
        $DbName
    )

    Get-Content $dumpFullPath | & mysql.exe @processArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[THÀNH CÔNG] Cơ sở dữ liệu đã khôi phục về mốc sạch baseline." -ForegroundColor Green
    } else {
        Write-Host "[LỖI] Khôi phục thất bại với exit code $LASTEXITCODE." -ForegroundColor Red
        exit $LASTEXITCODE
    }
} finally {
    if (Test-Path $tempCnf) { Remove-Item $tempCnf -Force -ErrorAction SilentlyContinue }
}
```

---

## 2. Lớp Helper Kiểm Chứng Dữ Liệu Ngầm: `DbHelper.cs`

Sau khi người dùng thực hiện giao dịch trên UI, kiểm tra trực tiếp các bảng cơ sở dữ liệu ngầm xem dữ liệu có lưu toàn vẹn không:

```csharp
using MySqlConnector; // hoặc Microsoft.Data.SqlClient, Npgsql

namespace EnterpriseTesting.Helpers;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Đếm số lượng bản ghi thỏa mãn điều kiện
    /// </summary>
    public int GetRecordCount(string tableName, string condition = "1=1")
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE {condition};";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Kiểm tra số lượng tồn kho thực tế của sản phẩm
    /// </summary>
    public int GetProductRealStock(int productId, int warehouseId)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT reel FROM llx_product_stock WHERE fk_product = @pId AND fk_entrepot = @wId;";
        cmd.Parameters.AddWithValue("@pId", productId);
        cmd.Parameters.AddWithValue("@wId", warehouseId);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }
}
```

---

## 3. Quy Tắc Đối Chiếu Dữ Liệu Ngầm Trong Test Class

```csharp
[TestMethod]
public void TC_DB_004_VerifyStockMovementIntegrity()
{
    // 1. Kiểm tra tồn trước giao dịch qua DB
    int initialStock = _dbHelper.GetProductRealStock(productId: 1, warehouseId: 1);

    // 2. Thực hiện hành động tạo và duyệt hóa đơn bán 2 sản phẩm trên UI
    // ...

    // 3. Kiểm tra tồn sau giao dịch qua DB
    int finalStock = _dbHelper.GetProductRealStock(productId: 1, warehouseId: 1);

    // 4. Assert số học chính xác
    Assert.AreEqual(initialStock - 2, finalStock, 
        $"Tồn kho trong cơ sở dữ liệu phải giảm đúng 2 đơn vị. Trước: {initialStock}, Sau: {finalStock}");
}
```

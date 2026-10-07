using Microsoft.Extensions.Configuration;

namespace DolibarrTests.Helpers;

/// <summary>
/// Đọc cấu hình từ appsettings.local.json và biến môi trường.
/// Mật khẩu PHẢI ở biến môi trường DOLIBARR_ADMIN_PASSWORD — không hard-code ở bất kỳ đâu.
/// </summary>
public static class TestConfig
{
    private static readonly IConfiguration _config;

    static TestConfig()
    {
        _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.example.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>http://localhost/dolibarr</summary>
    public static string BaseUrl =>
        _config["Dolibarr:BaseUrl"] ?? "http://localhost/dolibarr";

    /// <summary>http://localhost/dolibarr/index.php</summary>
    public static string LoginUrl =>
        _config["Dolibarr:LoginUrl"] ?? $"{BaseUrl}/index.php";

    public static string AdminUsername =>
        _config["Dolibarr:AdminUsername"] ?? "admin";

    /// <summary>
    /// Đọc mật khẩu từ biến môi trường DOLIBARR_ADMIN_PASSWORD.
    /// Ném InvalidOperationException nếu chưa đặt biến (không in giá trị).
    /// </summary>
    public static string AdminPassword
    {
        get
        {
            var pwd = Environment.GetEnvironmentVariable("DOLIBARR_ADMIN_PASSWORD");
            if (string.IsNullOrEmpty(pwd))
            {
                throw new InvalidOperationException(
                    "Biến môi trường DOLIBARR_ADMIN_PASSWORD chưa được đặt. " +
                    "Chạy: setx DOLIBARR_ADMIN_PASSWORD \"<mật khẩu>\" trong PowerShell, " +
                    "rồi mở lại terminal/IDE để nhận giá trị mới.");
            }
            return pwd;
        }
    }

    public static int TimeoutSeconds =>
        int.TryParse(_config["Browser:TimeoutSeconds"], out var t) ? t : 15;

    public static bool Headless =>
        bool.TryParse(_config["Browser:Headless"], out var h) && h;

    /// <summary>
    /// Đường dẫn file Excel chứa Test Data và Test Cases.
    /// Đặt trong appsettings.local.json: { "TestData": { "ExcelPath": "..." } }
    /// </summary>
    public static string ExcelPath =>
        _config["TestData:ExcelPath"]
        ?? @"D:\Projects\DoAnThucTap_Dolibarr\testcases\Dolibarr_TestCases.xlsx";

    /// <summary>
    /// URL gốc của Dolibarr REST API.
    /// Mặc định: {BaseUrl}/api/index.php
    /// </summary>
    public static string ApiBaseUrl =>
        _config["Dolibarr:ApiBaseUrl"] ?? $"{BaseUrl}/api/index.php";

    /// <summary>
    /// API Key xác thực khi gọi REST API.
    /// Ưu tiên biến môi trường DOLIBARR_API_KEY, sau đó tới appsettings.local.json.
    /// </summary>
    public static string ApiKey =>
        Environment.GetEnvironmentVariable("DOLIBARR_API_KEY")
        ?? _config["Dolibarr:ApiKey"]
        ?? "dolibarr_test_api_key_2026";

    /// <summary>
    /// Chuỗi kết nối cơ sở dữ liệu MariaDB Dolibarr.
    /// Ưu tiên: DOLIBARR_DB_CONNECTION > config (appsettings.local.json có inject DOLIBARR_DB_PASSWORD nếu có).
    /// </summary>
    public static string DbConnectionString
    {
        get
        {
            var envConn = Environment.GetEnvironmentVariable("DOLIBARR_DB_CONNECTION");
            if (!string.IsNullOrWhiteSpace(envConn)) return envConn;

            var configConn = _config["Database:ConnectionString"]
                ?? "Server=localhost;Port=3306;Database=dolibarr;Uid=dolibarrmysql;Charset=utf8mb4;";

            var envPass = Environment.GetEnvironmentVariable("DOLIBARR_DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(envPass))
            {
                if (configConn.Contains("Pwd="))
                {
                    configConn = System.Text.RegularExpressions.Regex.Replace(configConn, @"Pwd=[^;]*", $"Pwd={envPass}");
                }
                else
                {
                    configConn = configConn.TrimEnd(';') + $";Pwd={envPass};";
                }
            }
            return configConn;
        }
    }
}

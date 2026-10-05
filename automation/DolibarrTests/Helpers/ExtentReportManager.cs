using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;
using AventStack.ExtentReports.Reporter.Config;
using System.Reflection;

namespace DolibarrTests.Helpers;

/// <summary>
/// Quản lý Báo cáo Dashboard HTML chuyên nghiệp bằng ExtentReports 5.x.
/// Hỗ trợ đa luồng an toàn (ThreadLocal), tự động flush và đính kèm screenshot.
/// </summary>
public static class ExtentReportManager
{
    private static readonly object LockObj = new();
    private static ExtentReports? _extent;
    private static readonly ThreadLocal<ExtentTest?> _currentTest = new();

    public static string ReportDirectory { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "TestResults", "ExtentReports"));

    public static string ReportFilePath { get; } = Path.Combine(ReportDirectory, "Dolibarr_TestReport.html");

    public static ExtentReports Instance
    {
        get
        {
            if (_extent == null)
            {
                lock (LockObj)
                {
                    if (_extent == null)
                    {
                        Directory.CreateDirectory(ReportDirectory);

                        var sparkReporter = new ExtentSparkReporter(ReportFilePath);
                        sparkReporter.Config.DocumentTitle = "Dolibarr ERP Testing Dashboard";
                        sparkReporter.Config.ReportName = "Báo Cáo Kiểm Thử Tự Động Dolibarr ERP & CRM 22.0.4";
                        sparkReporter.Config.Theme = Theme.Standard;
                        sparkReporter.Config.Encoding = "utf-8";
                        sparkReporter.Config.TimeStampFormat = "yyyy-MM-dd HH:mm:ss";

                        _extent = new ExtentReports();
                        _extent.AttachReporter(sparkReporter);

                        // Thông tin môi trường kiểm thử
                        _extent.AddSystemInfo("Dự án", "Kiểm thử Dolibarr ERP & CRM");
                        _extent.AddSystemInfo("Phiên bản Dolibarr", "22.0.4 (DoliWamp / MariaDB)");
                        _extent.AddSystemInfo("Người thực hiện", "Đặng Hải Phi (23DH112608)");
                        _extent.AddSystemInfo("Hệ điều hành", Environment.OSVersion.ToString());
                        _extent.AddSystemInfo(".NET Runtime", Environment.Version.ToString());
                        _extent.AddSystemInfo("Framework", "MSTest v3 + Selenium POM + EPPlus");
                    }
                }
            }
            return _extent;
        }
    }

    /// <summary>Tạo node kiểm thử mới cho test method hiện tại</summary>
    public static ExtentTest CreateTest(string testName, string? description = null)
    {
        var test = Instance.CreateTest(testName, description ?? string.Empty);
        _currentTest.Value = test;
        return test;
    }

    /// <summary>Lấy node kiểm thử của thread hiện tại</summary>
    public static ExtentTest? CurrentTest => _currentTest.Value;

    /// <summary>Ghi toàn bộ báo cáo ra file HTML</summary>
    public static void Flush()
    {
        lock (LockObj)
        {
            _extent?.Flush();
        }
    }
}

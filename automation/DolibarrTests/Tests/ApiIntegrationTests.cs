using DolibarrTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DolibarrTests.Tests;

/// <summary>
/// Test Suite mở rộng: Kiểm thử tự động Dolibarr REST API qua HttpClient.
/// Đồng bộ kết quả trực tiếp vào Dashboard HTML ExtentReports.
/// </summary>
[TestClass]
public class ApiIntegrationTests
{
    private static readonly HttpClient HttpClient = new();
    private static string ApiBaseUrl => TestConfig.ApiBaseUrl;
    private static string ApiKey => TestConfig.ApiKey;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("DOLAPIKEY", ApiKey);
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    [TestInitialize]
    public void SetUp()
    {
        var test = ExtentReportManager.CreateTest(TestContext.TestName ?? "ApiTest");
        test.AssignCategory("REST API Testing");
        test.AssignAuthor("Dang Hai Phi (23DH112608)");
        test.Info($"Bắt đầu kiểm thử API: {TestContext.TestName}");
    }

    [TestCleanup]
    public void TearDown()
    {
        var test = ExtentReportManager.CurrentTest;
        if (TestContext.CurrentTestOutcome == UnitTestOutcome.Passed)
        {
            test?.Pass("Kiểm thử API thành công (PASS)");
        }
        else
        {
            test?.Fail("Kiểm thử API thất bại (FAIL)");
        }
        ExtentReportManager.Flush();
    }

    [TestMethod]
    [TestCategory("API")]
    [Description("TC_API_001: Kiểm tra API Status và phản hồi JSON của Dolibarr API")]
    public async Task TC_API_001_GetApiStatus()
    {
        var response = await HttpClient.GetAsync($"{ApiBaseUrl}/status");
        TestContext.WriteLine($"[TC_API_001] Status code: {(int)response.StatusCode} {response.StatusCode}");

        Assert.IsTrue(response.IsSuccessStatusCode, $"API Status phải trả về 200 OK. Thực tế: {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.IsTrue(doc.RootElement.TryGetProperty("success", out var successProp), "Phải có thuộc tính 'success' trong JSON phản hồi.");

        ExtentReportManager.CurrentTest?.Info($"API Status trả về HTTP 200 OK. Phản hồi hợp lệ.");
    }

    [TestMethod]
    [TestCategory("API")]
    [Description("TC_API_002: Kiểm tra API lấy danh sách Third parties (Khách hàng CRM)")]
    public async Task TC_API_002_GetThirdParties()
    {
        var response = await HttpClient.GetAsync($"{ApiBaseUrl}/thirdparties?limit=5");
        TestContext.WriteLine($"[TC_API_002] Status code: {(int)response.StatusCode}");

        Assert.IsTrue(response.IsSuccessStatusCode, $"GET /thirdparties phải trả về 200 OK. Thực tế: {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind, "Phản hồi phải là một JSON Array.");
        Assert.IsTrue(doc.RootElement.GetArrayLength() > 0, "Danh sách khách hàng qua API không được rỗng.");

        bool foundAbc = false;
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            if (elem.TryGetProperty("name", out var nameProp) && nameProp.GetString() == "Cong ty ABC")
            {
                foundAbc = true;
                break;
            }
        }

        Assert.IsTrue(foundAbc, "Phải tìm thấy khách hàng nền 'Cong ty ABC' trong danh sách trả về từ API.");
        ExtentReportManager.CurrentTest?.Info($"GET /thirdparties trả về {doc.RootElement.GetArrayLength()} khách hàng. Tìm thấy 'Cong ty ABC'.");
    }

    [TestMethod]
    [TestCategory("API")]
    [Description("TC_API_003: Kiểm tra API lấy danh sách Customer Invoices (Hóa đơn bán hàng)")]
    public async Task TC_API_003_GetInvoices()
    {
        var response = await HttpClient.GetAsync($"{ApiBaseUrl}/invoices?limit=5");
        TestContext.WriteLine($"[TC_API_003] Status code: {(int)response.StatusCode}");

        Assert.IsTrue(response.IsSuccessStatusCode, $"GET /invoices phải trả về 200 OK. Thực tế: {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind, "Phản hồi phải là một JSON Array.");

        ExtentReportManager.CurrentTest?.Info($"GET /invoices trả về {doc.RootElement.GetArrayLength()} hóa đơn.");
    }

    [TestMethod]
    [TestCategory("API")]
    [Description("TC_API_004: Kiểm tra API lấy thông tin tồn kho sản phẩm (Stock Module)")]
    public async Task TC_API_004_GetProductStock()
    {
        var response = await HttpClient.GetAsync($"{ApiBaseUrl}/products/1/stock");
        TestContext.WriteLine($"[TC_API_004] Status code: {(int)response.StatusCode}");

        Assert.IsTrue(response.IsSuccessStatusCode, $"GET /products/1/stock phải trả về 200 OK. Thực tế: {response.StatusCode}");

        string content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.IsTrue(doc.RootElement.TryGetProperty("stock_reel", out var stockProp), "Phải có trường stock_reel trong phản hồi API.");

        decimal stockReel = stockProp.ValueKind == JsonValueKind.Number
            ? stockProp.GetDecimal()
            : decimal.Parse(stockProp.GetString()!);
        TestContext.WriteLine($"[TC_API_004] Tồn kho thực tế (stock_reel) của Product ID 1 qua API: {stockReel}");

        ExtentReportManager.CurrentTest?.Info($"GET /products/1/stock thành công. Tồn kho thực tế stock_reel={stockReel}.");
    }
}

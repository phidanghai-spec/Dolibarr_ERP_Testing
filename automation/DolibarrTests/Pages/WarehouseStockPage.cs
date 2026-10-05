using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object để kiểm tra số lượng tồn kho (Stock) của sản phẩm trong Dolibarr.
/// Hỗ trợ đọc Physical stock từ trang chi tiết sản phẩm ({BaseUrl}/product/card.php?ref={ref})
/// hoặc từ trang kho hàng KHO001.
/// </summary>
public class WarehouseStockPage
{
    private readonly IWebDriver _driver;

    public WarehouseStockPage(IWebDriver driver) => _driver = driver;

    /// <summary>
    /// Điều hướng trực tiếp đến trang chi tiết sản phẩm theo mã Ref (PR001, PR002...)
    /// chuyển sang tab Stock và đọc số lượng tồn kho vật lý (Physical stock).
    /// </summary>
    public int GetProductPhysicalStock(string productRef)
    {
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/card.php?ref={productRef}");
        WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 15);

        // Chuyển sang tab Stock của sản phẩm
        try
        {
            var stockTab = WaitHelper.WaitClickable(_driver, By.XPath("//div[contains(@class, 'tabs')]//a[contains(text(), 'Stock') or contains(@href, 'stock')]"), timeoutSeconds: 5);
            stockTab.Click();
            WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 10);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Stock Debug] Cannot click stock tab: {ex.Message}");
        }

        Console.WriteLine($"[Stock Debug] URL after tab click: {_driver.Url}");

        // 1. Thử tìm trong bảng các dòng có chữ 'Physical' hoặc 'Real'
        try
        {
            var rows = _driver.FindElements(By.XPath("//tr[contains(., 'Physical') or contains(., 'Real stock')]"));
            foreach (var r in rows)
            {
                Console.WriteLine($"[Stock Debug] Found stock row text: '{r.Text}'");
                var match = Regex.Match(r.Text, @"(?:Physical stock|Real stock)[^\d]*(\d+)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int s))
                {
                    return s;
                }
            }
        }
        catch { }

        // 2. Fallback quét toàn bộ page source tìm Physical stock: \d+
        var pageText = _driver.PageSource;
        var pMatch = Regex.Match(pageText, @"(?:Physical stock|Real stock)[^\d]{1,50}(\d+)", RegexOptions.IgnoreCase);
        if (pMatch.Success && int.TryParse(pMatch.Groups[1].Value, out int fallbackStock))
        {
            Console.WriteLine($"[Stock Debug] Matched via PageSource: {fallbackStock}");
            return fallbackStock;
        }

        return -1;
    }
}

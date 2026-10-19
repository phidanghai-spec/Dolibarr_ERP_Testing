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

    /// <summary>
    /// Đếm số dòng biến động kho (Stock movements) của sản phẩm trong Dolibarr.
    /// Điều hướng tới tab biến động kho của sản phẩm và đếm số dòng dữ liệu.
    /// </summary>
    public int GetStockMovementsCount(string productRef)
    {
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/product.php?ref={productRef}");
        WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 15);

        // Click sang tab hoặc link Movement nếu có
        try
        {
            var mvtLink = _driver.FindElements(By.XPath("//a[contains(@href, 'mouvement.php') or contains(text(), 'Movement') or contains(text(), 'Mouvements')]")).FirstOrDefault();
            if (mvtLink != null && mvtLink.Displayed)
            {
                mvtLink.Click();
                WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 10);
            }
        }
        catch { }

        // Đếm số dòng dữ liệu trong bảng biến động
        try
        {
            var rows = _driver.FindElements(By.CssSelector("table.tagtable tr.oddeven, table.liste tr.oddeven, table.tagtable tr[data-rowid]"));
            return rows.Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Thực hiện thao tác điều chỉnh tồn kho (Correct stock) cho sản phẩm.
    /// Hỗ trợ kiểm thử nhập số lượng = 0 (TC_STK_010).
    /// </summary>
    public string PerformStockCorrection(string productRef, int qtyChange, string label)
    {
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/product.php?ref={productRef}");
        WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche"), timeoutSeconds: 15);

        // Nhấn nút Correct stock
        var correctBtn = WaitHelper.WaitClickable(_driver, By.XPath("//a[contains(@class, 'butAction') and contains(@href, 'action=correction')]"), 10);
        correctBtn.Click();

        WaitHelper.WaitVisible(_driver, By.Id("nbpiece"), 15);

        var js = (IJavaScriptExecutor)_driver;
        int mvtVal = qtyChange < 0 ? 1 : 0;
        int absQty = Math.Abs(qtyChange);

        js.ExecuteScript(@"
            var whOption = Array.from($('#id_entrepot option')).find(o => o.text.indexOf('KHO001') !== -1);
            if (whOption) {
                $('#id_entrepot').val(whOption.value).trigger('change');
            }
            $('#mouvement').val(arguments[0]).trigger('change');
        ", mvtVal);

        var qtyInput = _driver.FindElement(By.Id("nbpiece"));
        qtyInput.Clear();
        qtyInput.SendKeys(absQty.ToString());

        var labelInput = _driver.FindElement(By.Name("label"));
        labelInput.Clear();
        labelInput.SendKeys(label);

        var recordBtn = WaitHelper.WaitClickable(_driver, By.CssSelector("input[type='submit'][name='save']"), 10);
        recordBtn.Click();

        // Đọc thông báo lỗi (nếu có)
        return GetErrorMessage();
    }

    /// <summary>
    /// Đọc thông báo lỗi hiển thị trên trang kho.
    /// </summary>
    public string GetErrorMessage()
    {
        var selectors = new[]
        {
            By.CssSelector("div.jnotify-message"),
            By.CssSelector("div.error"),
            By.CssSelector(".fiche .error")
        };

        foreach (var sel in selectors)
        {
            try
            {
                var el = WaitHelper.WaitVisible(_driver, sel, timeoutSeconds: 3);
                var text = el.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            catch (WebDriverTimeoutException) { }
            catch (NoSuchElementException) { }
        }

        return string.Empty;
    }

    /// <summary>
    /// Đo thời gian tải trang Replenishment (ms) và đếm số dòng sản phẩm cần bổ sung (TC_STK_014).
    /// </summary>
    public (long ElapsedMs, int RowCount) MeasureReplenishmentPage()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/product/stock/replenish.php");

        WaitHelper.WaitVisible(_driver, By.CssSelector("div.fiche, table.tagtable, table.liste"), timeoutSeconds: 15);
        sw.Stop();

        // Đếm số dòng sản phẩm hiển thị trong bảng
        int count = 0;
        try
        {
            var rows = _driver.FindElements(By.CssSelector("table.tagtable tr.oddeven, table.liste tr.oddeven, tr[data-rowid]"));
            count = rows.Count;
        }
        catch { }

        return (sw.ElapsedMilliseconds, count);
    }
}

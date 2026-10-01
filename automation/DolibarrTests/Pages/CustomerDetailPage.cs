using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang chi tiết / xem khách hàng (Third party) sau khi tạo.
/// URL thành công: {BaseUrl}/societe/card.php?socid={id} hoặc ?id={id}
/// </summary>
public class CustomerDetailPage
{
    private readonly IWebDriver _driver;

    // ── Locators ────────────────────────────────────────────────────────────────

    /// <summary>Thông báo lưu thành công (div.ok). Xuất hiện ngay sau redirect.</summary>
    private static readonly By SuccessMessage = By.CssSelector("div.ok");

    /// <summary>
    /// Vùng chứa tên khách hàng hiển thị trên banner trang chi tiết.
    /// HTML Dolibarr 22: div.refid chứa trực tiếp text node tên, theo sau bởi các thẻ con .refidno.refaddress (địa chỉ, quốc gia).
    /// </summary>
    private static readonly By CustomerNameDisplay = By.CssSelector("div.refid");

    // ── Constructor ─────────────────────────────────────────────────────────────
    public CustomerDetailPage(IWebDriver driver) => _driver = driver;

    // ── Kiểm tra trạng thái ─────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra URL có phải là trang chi tiết khách hàng đã tạo thành công hay không.
    /// Điều kiện thành công thực sự: URL phải chứa 'societe/card.php' VÀ có tham số 'socid=\d+' hoặc 'id=\d+'.
    /// Tránh nhầm lẫn với trường hợp submit lỗi quay về 'societe/card.php' (không có id/socid).
    /// </summary>
    public static bool IsCustomerCreatedSuccessfully(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Regex.IsMatch(url, @"societe/card\.php\?.*(?<!\w)(socid|id)=\d+", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Chờ redirect về trang chi tiết sau khi lưu thành công.
    /// Sử dụng WebDriverWait để chờ URL khớp định dạng thành công thực sự (chứa id=\d+ hoặc socid=\d+).
    /// Trả về true nếu thành công trong timeout, false nếu bị chặn hoặc submit lỗi.
    /// </summary>
    public bool WaitForRedirectAfterSave(int? timeoutSeconds = null)
    {
        int timeout = timeoutSeconds ?? TestConfig.TimeoutSeconds;
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeout));
        try
        {
            return wait.Until(d => IsCustomerCreatedSuccessfully(d.Url));
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Chờ thông báo thành công (div.ok) xuất hiện.
    /// Trả về nội dung text của div.ok, hoặc rỗng nếu hết timeout.
    /// </summary>
    public string GetSuccessMessage(int? timeoutSeconds = null)
    {
        try
        {
            var el = WaitHelper.WaitVisible(_driver, SuccessMessage, timeoutSeconds);
            return el.Text.Trim();
        }
        catch (WebDriverTimeoutException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Đọc chính xác tên khách hàng từ trang chi tiết (loại bỏ phần tử con chứa địa chỉ/quốc gia).
    /// </summary>
    public string GetDisplayedName()
    {
        IWebElement? targetElement = null;

        // Ưu tiên 1: div.refid — phần tử chứa tên entity trong banner của Dolibarr 22
        try
        {
            targetElement = WaitHelper.WaitVisible(_driver, CustomerNameDisplay, timeoutSeconds: 5);
        }
        catch (WebDriverTimeoutException) { }

        // Ưu tiên 2: h1 trong div#id-right (nếu theme khác)
        if (targetElement == null)
        {
            try
            {
                targetElement = _driver.FindElement(By.CssSelector("#id-right h1"));
            }
            catch (NoSuchElementException) { }
        }

        if (targetElement == null)
        {
            return string.Empty;
        }

        // Dùng JavaScript để lấy text thuần của phần tử cha, loại bỏ hoàn toàn các thẻ con
        // chứa địa chỉ, quốc gia, technical id, vcard: .refidno, .refaddress, a, .opacitymedium, .statusref
        var js = (IJavaScriptExecutor)_driver;
        string script = @"
            var el = arguments[0];
            var clone = el.cloneNode(true);
            var toRemove = clone.querySelectorAll('.refidno, .refaddress, a, .opacitymedium, .statusref, .badge');
            for (var i = 0; i < toRemove.length; i++) {
                toRemove[i].remove();
            }
            return clone.textContent.trim();
        ";

        string extractedName = (js.ExecuteScript(script, targetElement) as string)?.Trim() ?? string.Empty;

        // Fallback: nếu clone rỗng, duyệt trực tiếp các text node (nodeType == 3) của phần tử cha
        if (string.IsNullOrEmpty(extractedName))
        {
            string fallbackScript = @"
                var el = arguments[0];
                var txt = '';
                for (var i = 0; i < el.childNodes.length; i++) {
                    if (el.childNodes[i].nodeType === 3) {
                        txt += el.childNodes[i].nodeValue;
                    }
                }
                return txt.trim();
            ";
            extractedName = (js.ExecuteScript(fallbackScript, targetElement) as string)?.Trim() ?? string.Empty;
        }

        // Unit-safe check theo yêu cầu: phát hiện nếu chuỗi vẫn còn dính newline hoặc dữ liệu địa chỉ/quốc gia
        if (extractedName.Contains('\n') || extractedName.Contains("Vietnam", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Locator lay ten van con dinh du lieu khac, can kiem tra lai HTML. Gia tri lay duoc: '{extractedName}'");
        }

        return extractedName;
    }

    /// <summary>
    /// Đọc HTML thô (innerHTML) của vùng chứa tên khách hàng.
    /// Dùng để kiểm tra XSS (xem thẻ HTML có bị unescaped hay không).
    /// </summary>
    public string GetCustomerNameContainerHtml()
    {
        try
        {
            var el = WaitHelper.WaitVisible(_driver, CustomerNameDisplay, timeoutSeconds: 5);
            return el.GetAttribute("innerHTML") ?? string.Empty;
        }
        catch (WebDriverTimeoutException) { }

        try
        {
            var h1 = _driver.FindElement(By.CssSelector("#id-right h1"));
            return h1.GetAttribute("innerHTML") ?? string.Empty;
        }
        catch (NoSuchElementException) { }

        return string.Empty;
    }

    /// <summary>
    /// Trả về URL hiện tại của trang chi tiết.
    /// Dùng để trích xuất ID khách hàng vừa tạo (không hard-code).
    /// </summary>
    public string GetCurrentUrl() => _driver.Url;
}

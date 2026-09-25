using DolibarrTests.Helpers;
using OpenQA.Selenium;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang chi tiết / xem khách hàng (Third party) sau khi tạo.
/// URL: {BaseUrl}/societe/card.php?id={id}  (Dolibarr redirect về đây sau khi lưu thành công)
///
/// Locator xác nhận từ HTML Dolibarr 22.0.4:
/// - Tiêu đề tên KH:    div.fiche > table tr > td.titlefield  hoặc span.badgeline  hoặc
///                       div#mainbody h1 / div.refid — tùy theme.
///                       An toàn nhất: lấy từ title trang hoặc div.refid / span.refid.
/// - Thông báo thành công: div.ok (hiện ngay sau khi redirect về card)
/// - URL pattern: societe/card.php?id=\d+  (không có action=create)
///
/// GHI CHÚ: Nếu locator sai sau khi tạo thành công thì probe HTML thật để cập nhật.
/// </summary>
public class CustomerDetailPage
{
    private readonly IWebDriver _driver;

    // ── Locators ────────────────────────────────────────────────────────────────

    /// <summary>Thông báo lưu thành công (div.ok). Xuất hiện ngay sau redirect.</summary>
    private static readonly By SuccessMessage = By.CssSelector("div.ok");

    /// <summary>
    /// Tên khách hàng hiển thị trên trang chi tiết.
    /// Dolibarr 22: tên nằm trong div.title-bar hay span chứa tên sau khi tạo.
    /// Thường hiển thị trong thẻ có class "title" hoặc trong breadcrumb/h1.
    /// Selector an toàn: lấy từ input readonly name="name" ở chế độ view,
    /// hoặc từ div.refid, hoặc đọc lại từ form ở chế độ edit.
    /// Dùng span.refid trước; nếu không có thì dùng td[data-key="name"].
    /// </summary>
    private static readonly By CustomerNameDisplay = By.CssSelector("div.refid");

    /// <summary>URL pattern sau khi tạo thành công: chứa "societe/card.php" và "id=" nhưng không có "action=".</summary>
    private const string SuccessUrlFragment = "societe/card.php";

    // ── Constructor ─────────────────────────────────────────────────────────────
    public CustomerDetailPage(IWebDriver driver) => _driver = driver;

    // ── Kiểm tra trạng thái ─────────────────────────────────────────────────────

    /// <summary>
    /// Chờ redirect về trang chi tiết sau khi lưu.
    /// Điều kiện: URL chứa "societe/card.php" VÀ không còn "action=create".
    /// Trả về true nếu redirect thành công trong TimeoutSeconds giây.
    /// </summary>
    public bool WaitForRedirectAfterSave(int? timeoutSeconds = null)
    {
        try
        {
            WaitHelper.WaitUrlContains(_driver, SuccessUrlFragment, timeoutSeconds);
            // Xác nhận thêm: URL không còn action=create
            return !_driver.Url.Contains("action=create", StringComparison.OrdinalIgnoreCase);
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
    /// Đọc tên khách hàng từ trang chi tiết.
    /// Thử nhiều locator theo thứ tự ưu tiên:
    /// 1. div.refid (Dolibarr 22 dùng cho tên entity)
    /// 2. h1 trong vùng nội dung chính
    /// Trả về string.Empty nếu không tìm thấy.
    /// </summary>
    public string GetDisplayedName()
    {
        // Ưu tiên 1: div.refid — phần tử Dolibarr 22 hiển thị tên entity
        try
        {
            var el = WaitHelper.WaitVisible(_driver, CustomerNameDisplay, timeoutSeconds: 5);
            return el.Text.Trim();
        }
        catch (WebDriverTimeoutException) { }

        // Ưu tiên 2: h1 trong div#id-right
        try
        {
            var h1 = _driver.FindElement(By.CssSelector("#id-right h1"));
            return h1.Text.Trim();
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

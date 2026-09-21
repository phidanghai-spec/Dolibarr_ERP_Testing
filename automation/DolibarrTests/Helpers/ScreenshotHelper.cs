using OpenQA.Selenium;

namespace DolibarrTests.Helpers;

/// <summary>
/// Chụp ảnh màn hình khi test Fail, lưu vào TestResults/Screenshots/.
/// Tên file = [TestName]_[yyyy-MM-dd_HH-mm-ss].png
/// </summary>
public static class ScreenshotHelper
{
    private static readonly string ScreenshotDir =
        Path.Combine(AppContext.BaseDirectory, "TestResults", "Screenshots");

    /// <summary>
    /// Chụp ảnh và lưu file. Trả về đường dẫn file hoặc null nếu lỗi.
    /// </summary>
    public static string? Capture(IWebDriver driver, string testName)
    {
        try
        {
            Directory.CreateDirectory(ScreenshotDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            // Làm sạch tên file: loại bỏ ký tự không hợp lệ
            var safeName = string.Concat(testName.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeName}_{timestamp}.png";
            var filePath = Path.Combine(ScreenshotDir, fileName);

            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            screenshot.SaveAsFile(filePath);

            return filePath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ScreenshotHelper] Không chụp được ảnh: {ex.Message}");
            return null;
        }
    }
}

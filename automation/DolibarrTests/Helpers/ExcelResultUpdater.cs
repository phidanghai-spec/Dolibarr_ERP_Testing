using OfficeOpenXml;
using System.IO;

namespace DolibarrTests.Helpers;

/// <summary>
/// Tự động cập nhật kết quả kiểm thử (Actual Result, Status, Evidence, Date)
/// vào file Excel Dolibarr_TestCases.xlsx (học hỏi từ kiến trúc DatVeXe_7).
/// </summary>
public static class ExcelResultUpdater
{
    private static readonly object _lock = new();

    static ExcelResultUpdater()
    {
        ExcelPackage.License.SetNonCommercialPersonal("DolibarrTests");
    }

    /// <summary>
    /// Cập nhật kết quả của một Test ID vào sheet 'Test Cases'.
    /// Cột 13 (M): Actual Result
    /// Cột 14 (N): Trạng thái (Pass / Fail)
    /// Cột 15 (O): Ngày chạy (yyyy-MM-dd)
    /// Cột 16 (P): Minh chứng (đường dẫn screenshot)
    /// Cột 17 (Q): Bug ID (nếu có)
    /// </summary>
    public static bool UpdateResult(string excelPath, string testId, string status, string actualResult, string? evidencePath = null, string? bugId = null)
    {
        lock (_lock)
        {
            if (!File.Exists(excelPath))
            {
                Console.WriteLine($"[ExcelResultUpdater WARN] Không tìm thấy file Excel: {excelPath}");
                return false;
            }

            try
            {
                using var package = new ExcelPackage(new FileInfo(excelPath));
                var ws = package.Workbook.Worksheets["Test Cases"];
                if (ws == null || ws.Dimension == null)
                {
                    Console.WriteLine("[ExcelResultUpdater WARN] Không tìm thấy sheet 'Test Cases'.");
                    return false;
                }

                int targetRow = -1;
                for (int r = 2; r <= ws.Dimension.Rows; r++)
                {
                    string cellId = ws.Cells[r, 1].Text.Trim();
                    if (string.Equals(cellId, testId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        targetRow = r;
                        break;
                    }
                }

                if (targetRow != -1)
                {
                    ws.Cells[targetRow, 13].Value = actualResult;
                    ws.Cells[targetRow, 14].Value = status;
                    ws.Cells[targetRow, 15].Value = DateTime.Now.ToString("yyyy-MM-dd");

                    if (!string.IsNullOrEmpty(evidencePath))
                    {
                        ws.Cells[targetRow, 16].Value = evidencePath;
                    }

                    if (!string.IsNullOrEmpty(bugId))
                    {
                        ws.Cells[targetRow, 17].Value = bugId;
                    }

                    package.Save();
                    Console.WriteLine($"[ExcelResultUpdater OK] Đã cập nhật dòng {targetRow} ({testId}): {status}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[ExcelResultUpdater WARN] Không tìm thấy Test ID '{testId}' trong sheet 'Test Cases'.");
                    return false;
                }
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"[ExcelResultUpdater WARN] File Excel đang được mở/khóa bởi tiến trình khác (Excel lock): {ioEx.Message}. Bỏ qua cập nhật trực tiếp để không làm gián đoạn test.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExcelResultUpdater ERROR] Lỗi khi ghi Excel: {ex.Message}");
                return false;
            }
        }
    }
}

using OfficeOpenXml;

namespace DolibarrTests.Helpers;

/// <summary>
/// Đọc dữ liệu test từ sheet "Test Data" trong Dolibarr_TestCases.xlsx.
/// Theo quy tắc skill: dữ liệu tự sinh nằm ở sheet Test Data, mỗi dòng một bộ dữ liệu.
/// EPPlus LicenseContext phải đặt NonCommercialPersonal trước khi dùng.
/// </summary>
public static class ExcelDataReader
{
    static ExcelDataReader()
    {
        // EPPlus 8+: dùng License.SetNonCommercialPersonal() thay LicenseContext (đã deprecated)
        ExcelPackage.License.SetNonCommercialPersonal("DolibarrTests");
    }

    /// <summary>
    /// Đọc bộ dữ liệu của một Test Case ID từ sheet "Test Data".
    /// Sheet phải có hàng tiêu đề ở dòng 1.
    /// Trả về Dictionary[tên_cột → giá_trị] cho dòng đầu tiên khớp TestCaseID.
    /// Ném KeyNotFoundException nếu không tìm thấy.
    /// </summary>
    public static Dictionary<string, string> GetRowByTestId(string testId, string excelPath)
    {
        using var pkg = new ExcelPackage(new FileInfo(excelPath));
        var ws = pkg.Workbook.Worksheets["Test Data"]
            ?? throw new InvalidOperationException(
                $"Không tìm thấy sheet 'Test Data' trong {excelPath}");

        // Đọc header dòng 1
        int colCount = ws.Dimension?.Columns ?? 0;
        if (colCount == 0)
            throw new InvalidOperationException("Sheet 'Test Data' không có dữ liệu.");

        var headers = new List<string>();
        for (int c = 1; c <= colCount; c++)
            headers.Add(ws.Cells[1, c].Text.Trim());

        // Tìm cột TestCaseID (linh hoạt tên)
        int idCol = headers.FindIndex(h =>
            h.Equals("TestCaseID", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("Test ID", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("TestID", StringComparison.OrdinalIgnoreCase)) + 1;
        if (idCol == 0)
            throw new InvalidOperationException(
                "Sheet 'Test Data' không có cột 'TestCaseID' hoặc 'Test ID'.");

        int rowCount = ws.Dimension?.Rows ?? 0;
        for (int r = 2; r <= rowCount; r++)
        {
            string cellVal = ws.Cells[r, idCol].Text.Trim();
            if (cellVal.Equals(testId, StringComparison.OrdinalIgnoreCase))
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= colCount; c++)
                    result[headers[c - 1]] = ws.Cells[r, c].Text.Trim();
                return result;
            }
        }

        throw new KeyNotFoundException(
            $"Không tìm thấy TestCaseID='{testId}' trong sheet 'Test Data' của {excelPath}.");
    }
}

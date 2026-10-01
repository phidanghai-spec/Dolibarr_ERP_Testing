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
    /// Đọc bộ dữ liệu của một Test Case ID từ sheet "Test Data" hoặc "Test Cases".
    /// Trả về Dictionary[tên_cột → giá_trị] cho dòng đầu tiên khớp TestCaseID / Test ID.
    /// Tự động ánh xạ alias giữa "CustomerName" và "Test data".
    /// Ném KeyNotFoundException nếu không tìm thấy.
    /// </summary>
    public static Dictionary<string, string> GetRowByTestId(string testId, string excelPath, string? preferredSheet = null)
    {
        using var pkg = new ExcelPackage(new FileInfo(excelPath));

        var sheetsToSearch = new List<string>();
        if (!string.IsNullOrEmpty(preferredSheet))
        {
            sheetsToSearch.Add(preferredSheet);
        }
        else
        {
            sheetsToSearch.Add("Test Data");
            sheetsToSearch.Add("Test Cases");
        }

        foreach (var sheetName in sheetsToSearch)
        {
            var ws = pkg.Workbook.Worksheets[sheetName];
            if (ws == null || ws.Dimension == null) continue;

            int colCount = ws.Dimension.Columns;
            int rowCount = ws.Dimension.Rows;

            var headers = new List<string>();
            for (int c = 1; c <= colCount; c++)
                headers.Add(ws.Cells[1, c].Text.Trim());

            int idCol = headers.FindIndex(h =>
                h.Equals("TestCaseID", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Test ID", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("TestID", StringComparison.OrdinalIgnoreCase)) + 1;

            if (idCol == 0) continue;

            for (int r = 2; r <= rowCount; r++)
            {
                string cellVal = ws.Cells[r, idCol].Text.Trim();
                if (cellVal.Equals(testId, StringComparison.OrdinalIgnoreCase))
                {
                    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 1; c <= colCount; c++)
                    {
                        string h = headers[c - 1];
                        if (!string.IsNullOrEmpty(h))
                            result[h] = ws.Cells[r, c].Text;
                    }

                    // Map alias tiện lợi để code dùng CustomerName hoặc Test data đều chạy được
                    if (result.TryGetValue("Test data", out var testData) && !result.ContainsKey("CustomerName"))
                    {
                        result["CustomerName"] = testData;
                    }
                    if (result.TryGetValue("CustomerName", out var custName) && !result.ContainsKey("Test data"))
                    {
                        result["Test data"] = custName;
                    }

                    return result;
                }
            }
        }

        throw new KeyNotFoundException(
            $"Không tìm thấy TestCaseID='{testId}' trong các sheet của {excelPath}.");
    }
}

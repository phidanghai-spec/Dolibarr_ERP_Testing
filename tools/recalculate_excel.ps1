# tools/recalculate_excel.ps1
# Mở file Excel bằng Microsoft Excel COM để tính toán lại toàn bộ công thức và lưu cached values

param(
    [string]$FilePath = "D:\Projects\DoAnThucTap_Dolibarr\testcases\Dolibarr_TestCases.xlsx"
)

$ErrorActionPreference = 'Stop'

try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    
    $wb = $excel.Workbooks.Open($FilePath)
    $excel.CalculateFull()
    
    $ws = $wb.Sheets.Item("Summary")
    Write-Host "Summary Sheet Values:"
    Write-Host "  Total Test Cases (D6):" $ws.Range("D6").Value2
    Write-Host "  Total Automated  (E6):" $ws.Range("E6").Value2
    Write-Host "  Total Manual     (F6):" $ws.Range("F6").Value2
    Write-Host "  Total Pass       (G6):" $ws.Range("G6").Value2
    Write-Host "  Total Fail       (H6):" $ws.Range("H6").Value2
    Write-Host "  Overall Pass Rate(J6):" $ws.Range("J6").Text
    
    $wb.Save()
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
    Write-Host "[THÀNH CÔNG] Đã tính toán và lưu lại toàn bộ công thức Excel."
}
catch {
    Write-Host "[LOI Excel COM]: $($_.Exception.Message)" -ForegroundColor Red
}

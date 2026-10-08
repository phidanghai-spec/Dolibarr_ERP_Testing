# tools\run-demo-ui.ps1
# Script chay demo truc quan (hien trinh duyet Chrome tu dong thao tac)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DO AN THUC TAP: DEMO KIEM THU TU DONG DOLIBARR ERP   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Khoi phuc database ve moc sach truoc khi demo
Write-Host "[1/3] Khoi phuc database ve moc sach baseline..." -ForegroundColor Yellow
& pwsh "$PSScriptRoot\restore-db.ps1"

# 2. Chay test suite tren trinh duyet Chrome
Write-Host ""
Write-Host "[2/3] Bat dau mo trinh duyet Chrome va chay kiem thu tu dong..." -ForegroundColor Green
Write-Host "      (Cua so Chrome se tu dong thao tac truc quan tren man hinh)" -ForegroundColor Gray
Write-Host ""

Set-Location "$PSScriptRoot\.."
dotnet test automation\DolibarrTests\DolibarrTests.csproj --filter "FullyQualifiedName~SalesProposalTests" --logger "console;verbosity=normal"

# 3. Don dep database
Write-Host ""
Write-Host "[3/3] Demo hoan tat! Dang khoi phuc lai database ve moc sach..." -ForegroundColor Yellow
& pwsh "$PSScriptRoot\restore-db.ps1"
Write-Host "[OK] Database da ve moc sach hoan toan. San sang cho lan demo tiep theo!" -ForegroundColor Green

# tools/restore-db.ps1
# Script khôi phục cơ sở dữ liệu Dolibarr về mốc sạch (Baseline Dump).
# Nguồn: db/dolibarr_baseline.sql

param(
    [string]$DumpFile = "db\dolibarr_clean_baseline.sql",
    [string]$DbUser = "dolibarrmysql",
    [string]$DbName = "dolibarr_clean"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "=== Khôi phục Cơ sở dữ liệu Dolibarr (Baseline Restore) ===" -ForegroundColor Cyan

# Kiểm tra file dump
$repoRoot = Split-Path -Parent $PSScriptRoot
$dumpFullPath = Join-Path $repoRoot $DumpFile

if (-not (Test-Path $dumpFullPath)) {
    Write-Host "[LỖI] Không tìm thấy file dump: $dumpFullPath" -ForegroundColor Red
    exit 1
}

# Tìm mysql.exe trong DoliWamp
$mysqlExe = "D:\Projects\dolibarr\bin\mariadb\mariadb10.6.5\bin\mysql.exe"
if (-not (Test-Path $mysqlExe)) {
    $found = Get-ChildItem -Path "D:\Projects\dolibarr" -Filter "mysql.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $mysqlExe = $found.FullName }
}

if (-not (Test-Path $mysqlExe)) {
    Write-Host "[LỖI] Không tìm thấy mysql.exe trong D:\Projects\dolibarr" -ForegroundColor Red
    exit 1
}

# Lấy mật khẩu DB từ biến môi trường hoặc appsettings.local.json (không hardcode)
$dbPass = $env:DOLIBARR_DB_PASSWORD
if (-not $dbPass) {
    $appsettingsLocal = Join-Path $repoRoot "automation\DolibarrTests\appsettings.local.json"
    if (Test-Path $appsettingsLocal) {
        try {
            $localJson = Get-Content $appsettingsLocal -Raw | ConvertFrom-Json
            if ($localJson.Database.ConnectionString -match 'Pwd=([^;]+)') {
                $dbPass = $matches[1]
            }
        } catch {}
    }
}
if (-not $dbPass) {
    Write-Host "[CẢNH BÁO] Chưa tìm thấy mật khẩu DB qua biến môi trường DOLIBARR_DB_PASSWORD hoặc appsettings.local.json." -ForegroundColor Yellow
}

Write-Host "Đang nạp file dump: $dumpFullPath vào database '$DbName'..." -ForegroundColor Yellow

# Dùng file tạm client config để tránh lộ mật khẩu trong danh sách process / command line
$tempCnf = [System.IO.Path]::GetTempFileName()
try {
    $cnfContent = "[client]`nuser=$DbUser`npassword=$dbPass`nhost=localhost`n"
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($tempCnf, $cnfContent, $utf8NoBom)

    $processArgs = @(
        "--defaults-extra-file=$tempCnf",
        $DbName
    )

    Get-Content $dumpFullPath | & $mysqlExe @processArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[THÀNH CÔNG] Cơ sở dữ liệu đã được khôi phục về mốc sạch baseline." -ForegroundColor Green
    } else {
        Write-Host "[LỖI] Khôi phục thất bại với exit code $LASTEXITCODE." -ForegroundColor Red
        exit $LASTEXITCODE
    }
} finally {
    if (Test-Path $tempCnf) {
        Remove-Item $tempCnf -Force -ErrorAction SilentlyContinue
    }
}

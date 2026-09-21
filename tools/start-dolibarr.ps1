# tools/start-dolibarr.ps1
# Bật DoliWamp (MySQL + Apache) và kiểm tra HTTP.
# Tự nâng quyền Admin nếu chưa có.

param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Tự nâng quyền Administrator ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Đang yêu cầu quyền Administrator..." -ForegroundColor Yellow
    Start-Process pwsh -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Write-Host "=== Khởi động Dolibarr (DoliWamp) ===" -ForegroundColor Cyan

# --- Bật Database trước ---
$dbService = 'doliwampmysqld'
$webService = 'doliwampapache'

function Start-ServiceIfNeeded {
    param([string]$Name)
    $svc = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Host "  [LỖI] Không tìm thấy dịch vụ: $Name" -ForegroundColor Red
        return
    }
    if ($svc.Status -ne 'Running') {
        Write-Host "  Đang bật: $Name ..." -ForegroundColor Yellow
        Start-Service -Name $Name
        $svc.WaitForStatus('Running', [TimeSpan]::FromSeconds(15))
    }
    else {
        Write-Host "  $Name đã Running" -ForegroundColor Green
    }
}

Start-ServiceIfNeeded $dbService
Start-ServiceIfNeeded $webService

# --- In trạng thái 2 dịch vụ ---
Write-Host ""
Write-Host "--- Trạng thái dịch vụ ---" -ForegroundColor Cyan
foreach ($svcName in @($dbService, $webService)) {
    $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
    if ($svc) {
        $color = if ($svc.Status -eq 'Running') { 'Green' } else { 'Red' }
        Write-Host ("  {0,-25} {1}" -f $svcName, $svc.Status) -ForegroundColor $color
    }
    else {
        Write-Host "  $svcName : KHÔNG TÌM THẤY" -ForegroundColor Red
    }
}

# --- Kiểm tra HTTP (không follow redirect để bắt cả 302) ---
Write-Host ""
Write-Host "--- Kiểm tra HTTP Dolibarr ---" -ForegroundColor Cyan
$url = 'http://localhost/dolibarr/index.php'
try {
    # MaximumRedirection 0 để không follow redirect, bắt cả 302
    $response = Invoke-WebRequest -Uri $url -Method GET -MaximumRedirection 0 `
        -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "  HTTP $($response.StatusCode)" -ForegroundColor Green
}
catch [System.Net.WebException] {
    # 302 redirect ném exception trong .NET — bắt và đọc status code
    $webEx = $_.Exception
    if ($webEx.Response) {
        $code = [int]$webEx.Response.StatusCode
        Write-Host "  HTTP $code" -ForegroundColor Green
    }
    else {
        Write-Host "  Không kết nối được: $($webEx.Message)" -ForegroundColor Red
    }
}
catch {
    Write-Host "  Không kết nối được: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Read-Host "Nhấn Enter để đóng"

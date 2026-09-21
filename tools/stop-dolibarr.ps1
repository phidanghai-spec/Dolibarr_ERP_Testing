# tools/stop-dolibarr.ps1
# Tắt DoliWamp (Apache trước, rồi MySQL).
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

Write-Host "=== Dừng Dolibarr (DoliWamp) ===" -ForegroundColor Cyan

function Stop-ServiceIfRunning {
    param([string]$Name)
    $svc = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Host "  [SKIP] Không tìm thấy dịch vụ: $Name" -ForegroundColor Yellow
        return
    }
    if ($svc.Status -eq 'Running') {
        Write-Host "  Đang dừng: $Name ..." -ForegroundColor Yellow
        Stop-Service -Name $Name -Force
        Write-Host "  $Name : Stopped" -ForegroundColor Green
    }
    else {
        Write-Host "  $Name đã dừng ($($svc.Status))" -ForegroundColor Gray
    }
}

# Dừng Apache trước, rồi mới dừng Database
Stop-ServiceIfRunning 'doliwampapache'
Stop-ServiceIfRunning 'doliwampmysqld'

Write-Host ""
Write-Host "Dolibarr đã dừng." -ForegroundColor Green

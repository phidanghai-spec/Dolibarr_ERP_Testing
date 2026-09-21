# Đồ án thực tập — Kiểm thử Dolibarr ERP & CRM

Repo automation kiểm thử Dolibarr 22.0.4 cho đồ án thực tập (Đặng Hải Phi - 23DH112608).
Phạm vi: CRM (Third parties), Sales & Invoicing (Proposals, Invoices), Stock.

🔗 **Repo**: https://github.com/phidanghai-spec/Dolibarr_ERP_Testing

---

## Cách chạy

### 1. Bật Dolibarr
```powershell
pwsh tools\start-dolibarr.ps1
```
Chờ đến khi thấy **2 dịch vụ Running** và **HTTP 200** hoặc **HTTP 302**.

### 2. Đặt biến môi trường mật khẩu (lần đầu, rồi mở lại IDE/terminal)
```powershell
setx DOLIBARR_ADMIN_PASSWORD "<mật khẩu admin của bạn>"
# Đóng terminal này và mở lại để nhận giá trị mới
```
> ⚠️ Không commit mật khẩu vào repo. Biến chỉ đặt 1 lần, lưu trong Windows Registry.

### 3. Chạy test
```powershell
cd automation\DolibarrTests
dotnet test
```
Kết quả sẽ hiển thị PASSED/FAILED. Ảnh chụp khi Fail được lưu tại `TestResults/Screenshots/`.

### 4. Tắt Dolibarr sau khi xong
```powershell
pwsh tools\stop-dolibarr.ps1
```

---

## Cấu trúc thư mục

| Thư mục | Nội dung |
|---|---|
| `docs/` | Test Plan, Use Case, báo cáo quá trình (Word) |
| `testcases/` | Dolibarr_TestCases.xlsx — test case, data, bug, AI Log |
| `automation/DolibarrTests/` | Solution C# Selenium + MSTest |
| `evidence/` | Ảnh/clip minh chứng |
| `db/` | Dump DB mốc sạch |
| `tools/` | Script start/stop Dolibarr |

Xem `AGENTS.md` để biết đầy đủ quy ước làm việc.

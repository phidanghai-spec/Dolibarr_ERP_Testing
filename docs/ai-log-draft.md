# AI Log Draft — Phiên làm việc 2026-09-21

## Việc AI đã thực hiện trong phiên này

| # | Nội dung | File tạo/sửa | Ghi chú |
|---|---|---|---|
| 1 | Đọc AGENTS.md, skill selenium-csharp-pom, dolibarr-domain để nắm quy tắc | — | Bắt buộc trước khi viết code |
| 2 | BƯỚC 0: Kiểm tra Dolibarr (HTTP 200 ✅) và biến DOLIBARR_ADMIN_PASSWORD (KHONG ⚠️) | — | Biến chưa đặt — test cần đặt trước khi chạy |
| 3 | BƯỚC 1: Tạo solution MSTest net9.0, thêm NuGet: Selenium.WebDriver 4.49, EPPlus 8.7, Microsoft.Extensions.Configuration.* 10.0.12 | `automation/DolibarrTests/*.csproj` | Không thêm WebDriverManager, ChromeDriver package |
| 4 | BƯỚC 2: Tạo appsettings.example.json (commit), appsettings.local.json (gitignore), TestConfig.cs đọc env var | `Helpers/TestConfig.cs`, `appsettings.*.json` | Mật khẩu KHÔNG lưu ở đâu, ném lỗi rõ nếu thiếu |
| 5 | BƯỚC 3: DriverFactory (incognito, maximize, không ImplicitWait), WaitHelper (WebDriverWait lambda), ScreenshotHelper | `Helpers/*.cs` | WaitHelper viết lại dùng lambda thuần vì Selenium 4.49 không include SeleniumExtras.WaitHelpers |
| 6 | BƯỚC 4: BaseTest (TestInitialize tạo driver, TestCleanup chụp ảnh khi Fail rồi Quit) | `Tests/BaseTest.cs` | |
| 7 | BƯỚC 5: Lấy locator từ HTML thật bằng `curl http://localhost/dolibarr/index.php` | — | Xác nhận: id="username", id="password", form#login input[type=submit] |
| 8 | BƯỚC 6: LoginPage (GoTo, LoginAs, GetErrorMessage, IsOnLoginPage), DashboardPage (IsLoaded dùng id="id-right") | `Pages/*.cs` | Locator id="id-right" chưa xác nhận bằng đăng nhập thật — xem mục CẦN KIỂM TRA |
| 9 | BƯỚC 7: SmokeTests — SMOKE_01 (đăng nhập đúng), SMOKE_02 (đăng nhập sai, mật khẩu giả "sai-mat-khau-xyz") | `Tests/SmokeTests.cs` | Mỗi test có Assert cụ thể theo yêu cầu |
| 10 | BƯỚC 8: tools/start-dolibarr.ps1 (bật MySQL trước, Apache sau; tự nâng quyền Admin), tools/stop-dolibarr.ps1 | `tools/*.ps1` | |
| 11 | BƯỚC 9: Cập nhật AGENTS.md (thêm tools/, mục Môi trường chạy), README.md (viết lại), .gitignore (thêm db/*.sql), SKILL.md (.NET 8→.NET 9) | `AGENTS.md`, `README.md`, `.gitignore`, `SKILL.md` | |
| 12 | BƯỚC 10: dotnet build → **Build succeeded, 0 Error** | — | Log thật dán bên dưới |
| 13 | BƯỚC 10: dotnet test (đang chờ kết quả) | — | Xem bên dưới |

---

## Kết quả dotnet build (thật)
```
  Determining projects to restore...
  All projects are up-to-date for restore.
  DolibarrTests -> D:\Projects\DoAnThucTap_Dolibarr\automation\DolibarrTests\bin\Debug\net9.0\DolibarrTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:04.34
```

## Kết quả dotnet test
*(Cập nhật sau khi chạy xong — xem Terminal)*

---

## Điều AI không chắc / cần người dùng kiểm tra

1. **Locator Dashboard** `id="id-right"`: Được suy luận từ kiến trúc Dolibarr 22.x điển hình, chưa xác nhận bằng đăng nhập thật do biến `DOLIBARR_ADMIN_PASSWORD` chưa đặt lúc probe. **Sau khi đặt biến và chạy SMOKE_01, nếu test Fail ở bước `DashboardPage.IsLoaded()`, cần mở browser DevTools để tìm id/class thật của vùng nội dung chính sau login, rồi cập nhật `DashboardPage.cs`.**

2. **Thông báo lỗi login** (`div.login_main_message`): Selector này cũng suy luận từ source Dolibarr điển hình. Nếu SMOKE_02 Fail ở bước `GetErrorMessage()`, cần kiểm tra HTML trang login sau khi nhập sai mật khẩu và cập nhật `LoginPage.cs`.

3. **Biến môi trường**: Trong phiên làm việc này, `DOLIBARR_ADMIN_PASSWORD = KHONG`. SMOKE_01 sẽ Fail do `TestConfig.AdminPassword` ném lỗi. Người dùng cần chạy `setx DOLIBARR_ADMIN_PASSWORD "..."` rồi mở lại terminal.

4. **EPPlus license**: EPPlus 8.7 yêu cầu khai báo `ExcelPackage.LicenseContext` trước khi sử dụng. Đã chưa dùng EPPlus trong phiên này (chỉ thêm package). Khi viết ResultWriter, cần thêm: `ExcelPackage.LicenseContext = LicenseContext.NonCommercial;`

5. **MSTestSettings.cs**: `dotnet new mstest` tạo file này với `Parallelize(Scope = ExecutionScope.MethodLevel)`. Hai smoke test chạy song song có thể tốt, nhưng nên xem xét khi test sau có thứ tự phụ thuộc.

---

*Ghi vào sheet AI Log của Dolibarr_TestCases.xlsx theo skill ai-log.*

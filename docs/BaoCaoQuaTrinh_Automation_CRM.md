# BÁO CÁO QUÁ TRÌNH THỰC HIỆN
## Kiểm Thử Tự Động Module CRM — Dolibarr ERP & CRM 22.0.4

**Đồ án thực tập** | Sinh viên: Đặng Hải Phi (23DH112608)
**Hạn nộp:** Đầu tháng 12/2026
**Công cụ chính:** C# .NET 9 · Selenium WebDriver 4.x · MSTest · EPPlus · Page Object Model
**Phạm vi tài liệu này:** Module CRM (Third Parties — Khách hàng) — giai đoạn Automation

---

## 1. MỤC TIÊU VÀ PHẠM VI

| Mục | Nội dung |
|-----|----------|
| **Hệ thống test** | Dolibarr 22.0.4 (DoliWamp) tại `http://localhost/dolibarr` |
| **Module** | CRM — Third Parties (Khách hàng / Société) |
| **Loại kiểm thử** | Automation (Selenium WebDriver + MSTest) |
| **Kỹ thuật thiết kế** | BVA, EP, Negative Testing, Edge Case, Security (XSS) |
| **Số test case Automation** | 15 (SMOKE: 2 · CRM: 12 · Cleanup: 1) |
| **Kết quả cuối** | 15/15 PASS |

---

## 2. KIẾN TRÚC GIẢI PHÁP

### 2.1 Cấu trúc thư mục

```
automation/DolibarrTests/
├── Helpers/
│   ├── BaseTest.cs          # Lifecycle driver (Init/Cleanup)
│   ├── DriverFactory.cs     # Tạo ChromeDriver (incognito, maximize)
│   ├── ExcelDataReader.cs   # Đọc dữ liệu từ Dolibarr_TestCases.xlsx
│   ├── ScreenshotHelper.cs  # Chụp ảnh khi test Fail
│   ├── TestConfig.cs        # Đọc cấu hình + biến môi trường
│   └── WaitHelper.cs        # WebDriverWait thuần (không Thread.Sleep)
├── Pages/                   # Page Object Model
│   ├── LoginPage.cs
│   ├── DashboardPage.cs
│   ├── CustomerCreatePage.cs
│   ├── CustomerDetailPage.cs
│   └── CustomerListPage.cs
└── Tests/
    ├── BaseTest.cs
    ├── SmokeTests.cs        # SMOKE_01, SMOKE_02
    └── CrmCustomerTests.cs  # TC_CRM_005 đến TC_CRM_016
```

### 2.2 Nguyên tắc triển khai

- **Không Thread.Sleep**: toàn bộ chờ đợi dùng WebDriverWait với điều kiện cụ thể.
- **Assert có giá trị cụ thể**: mỗi test có ít nhất 1 Assert ghi rõ giá trị kỳ vọng và thực tế.
- **Không hard-code mã tham chiếu Dolibarr**: mã KH (socid) đọc từ URL sau khi tạo.
- **Dữ liệu có hậu tố ngẫu nhiên**: dùng Guid.NewGuid() để tránh trùng lặp trong DB.
- **[DoNotParallelize]**: CRM tests dùng chung session login — tránh race condition.
- **Cleanup luôn trong finally**: đảm bảo không tích lũy rác trong DB dù test Fail.

---

## 3. CÁC GIAI ĐOẠN THỰC HIỆN

### Giai đoạn 1 — Dựng Skeleton Automation (2026-09-21)

**Việc đã làm:**

1. Tạo solution C# .NET 9, MSTest, thêm NuGet packages:
   - Selenium.WebDriver 4.49, Selenium.Support
   - EPPlus 8.7 (đọc/ghi Excel)
   - Microsoft.Extensions.Configuration.*

2. Tạo TestConfig.cs — đọc BaseUrl, LoginUrl từ appsettings.json, đọc password từ biến môi trường DOLIBARR_ADMIN_PASSWORD (không lưu vào repo).

3. Tạo DriverFactory.cs — ChromeDriver incognito, maximize, không ImplicitWait.

4. Tạo WaitHelper.cs — viết lại bằng lambda thuần (Selenium 4.49 không include SeleniumExtras.WaitHelpers).

5. Xác nhận locator bằng curl http://localhost/dolibarr/index.php:
   - Username: id="username", Password: id="password", Form: id="login".

6. Tạo LoginPage.cs, DashboardPage.cs, SmokeTests.cs (SMOKE_01, SMOKE_02).

7. Tạo tools/start-dolibarr.ps1 (tự nâng quyền Admin, bật MySQL trước, Apache sau).

**Kết quả build:** Build succeeded. 0 Warning(s) 0 Error(s)

---

### Giai đoạn 2 — TC_CRM_005 & TC_CRM_006: BVA tên khách hàng (2026-09-24)

**Kỹ thuật:** Boundary Value Analysis — kiểm tra giới hạn varchar(128) / maxlength=128.

| TC | Mô tả | Kỹ thuật | Kết quả |
|----|-------|----------|---------|
| TC_CRM_005 | Tên đúng 128 ký tự (biên N) | BVA | PASS |
| TC_CRM_006 | Tên 129 ký tự → browser cắt còn 128 (biên N+1) | BVA | PASS |

**Triển khai đáng chú ý:**
- CustomerCreatePage.cs với locator xác nhận từ source Dolibarr 22: input#name, input#customerinput, input.button-save.
- ExcelDataReader.cs: đọc template tên từ sheet "Test Data", ghép suffix GUID 6 ký tự để tên unique mỗi lần chạy.
- CleanupCreatedCustomer(): dùng CustomerDetailPage.DeleteCustomer() trong finally để dọn DB sau mỗi test.

---

### Giai đoạn 3 — TC_CRM_007 đến TC_CRM_011: EP, Edge Case, Security (2026-09-25)

| TC | Mô tả | Kỹ thuật | Kết quả |
|----|-------|----------|---------|
| TC_CRM_007 | Tên rỗng — kiểm tra client-side required hoặc server-side block | Negative/EP | PASS |
| TC_CRM_008 | Tên 1 ký tự "A" (biên tối thiểu) | BVA | PASS |
| TC_CRM_009 | Tên chỉ gồm khoảng trắng — quan sát hành vi Dolibarr | Edge Case | PASS |
| TC_CRM_010 | Tên chứa O'Brien & <Test> "123" — kiểm tra XSS | Security | PASS |
| TC_CRM_011 | Tên 128 ký tự tiếng Việt có dấu (Unicode multi-byte) | Unicode/BVA | PASS |

**Vấn đề gặp phải và cách giải:**

1. GetDisplayedName() trả về cả địa chỉ/quốc gia: dùng JavaScript cloneNode(true), xóa phần tử con .refidno/.refaddress trước khi lấy textContent.

2. URL redirect sau khi lưu: Dolibarr chuyển sang card.php?socid=X&action=read. Cần regex /societe/card\.php\?.*(socid|id)=\d+/ thay vì kiểm tra URL đơn giản.

3. Simulate sanitize XSS của Dolibarr (TC_CRM_010): viết SimulateDolibarrSanitize() mô phỏng strip_tags() + xóa dấu ngoặc kép + chuẩn hóa khoảng trắng — dùng để Assert kết quả lưu DB khớp kỳ vọng.

---

### Giai đoạn 4 — TC_CRM_012 đến TC_CRM_016: CRUD hoàn chỉnh (2026-10-02)

| TC | Mô tả | Loại | Kết quả |
|----|-------|------|---------|
| TC_CRM_012 | Sửa tên KH → assert tên mới hiển thị đúng | Update (CRUD-U) | PASS |
| TC_CRM_013 | Tìm kiếm theo tên đầy đủ → đúng 1 kết quả | Search (full) | PASS |
| TC_CRM_014 | Tìm kiếm theo một phần tên → có kết quả khớp | Search (partial) | PASS |
| TC_CRM_015 | Tìm kiếm tên không tồn tại → 0 kết quả | Search (negative) | PASS |
| TC_CRM_016 | Xóa KH → xác minh không còn trong danh sách | Delete (CRUD-D) | PASS |

**Triển khai đáng chú ý:**

CustomerDetailPage.DeleteCustomer(): Dolibarr 22 dùng jQuery UI Dialog (không phải window.confirm()).
Luồng: (1) Click id="action-delete" → dialog xuất hiện. (2) Click nút "Yes" trong .ui-dialog-buttonset bằng XPath động. (3) Fallback: gọi jQuery trực tiếp nếu XPath fail.

CustomerListPage.cs mới với SearchByName(), GetResultCount(), GetResultNames(), HasNoResultMessage().

---

### Giai đoạn 5 — Code Review & Sửa Bug (2026-10-02 đến 2026-10-03)

Sau khi suite đạt 12/12 PASS, thực hiện code review. Phát hiện và sửa 4 lỗi:

**Bug 1 — TC_CRM_016 thiếu gán _createdCustomerUrl (P1)**
- Vấn đề: dùng biến cục bộ createdUrl thay vì field _createdCustomerUrl. Nếu DeleteCustomer() fail → finally không cleanup được → KH rác tích lũy trong DB.
- Fix: gán _createdCustomerUrl = Driver.Url ngay sau khi tạo KH thành công. Set null sau khi xóa thành công.

**Bug 2 — Cleanup_DeleteAllTestCustomers không xử lý pagination (P1)**
- Vấn đề: chỉ lấy limit=100 một lần → bỏ sót nếu DB tích lũy >100 KH test.
- Fix: thêm while loop, mỗi vòng load 1 trang, thoát khi không còn KH test nào.

**Bug 3 — SearchByName race condition (P2)**
- Vấn đề: wait.Until(URL contains "societe/list") luôn true ngay lập tức vì URL đã có sẵn trước khi search.
- Fix: thay bằng wait.Until(ResultRows visible OR NoResultCell visible).

**Bug 4 — IsOnLoginPage() gây StaleElementReferenceException (P1)**
- Triệu chứng: SMOKE_02 fail với StaleElementReferenceException tại LoginPage.cs:76.
- Vấn đề: sau khi submit form login, Dolibarr reload trang → IWebElement form#login cũ bị stale.
- Fix: đổi FindElement().Displayed sang WaitHelper.WaitVisible() — tự tìm lại element mới sau reload.

---

## 4. KẾT QUẢ THỰC THI CUỐI CÙNG

```
Test run: DolibarrTests.dll (.NETCoreApp,Version=v9.0)

  Passed  SMOKE_01_LoginAdmin_ValidCredentials_ShouldNavigateToDashboard      [10 s]
  Passed  SMOKE_02_LoginAdmin_WrongPassword_ShouldShowError                    [ 4 s]
  Passed  TC_CRM_005_CreateCustomer_NameExactly128Chars_ShouldSaveSuccessfully [21 s]
  Passed  TC_CRM_006_CreateCustomer_Name129Chars_BrowserShouldTruncateTo128   [20 s]
  Passed  TC_CRM_007_CreateCustomer_EmptyName_ShouldBeBlocked                  [ 8 s]
  Passed  TC_CRM_008_CreateCustomer_SingleChar_ShouldSaveSuccessfully          [19 s]
  Passed  TC_CRM_009_CreateCustomer_WhitespaceOnly_ObserveActualBehavior       [10 s]
  Passed  TC_CRM_010_CreateCustomer_SpecialChars_ObserveXssHandling            [ 7 s]
  Passed  TC_CRM_011_CreateCustomer_VietnameseDiacritics128Chars_ShouldSave    [20 s]
  Passed  Cleanup_DeleteAllTestCustomers                                        [ 4 s]
  Passed  TC_CRM_012_UpdateCustomer_ChangeName_ShouldDisplayNewName            [10 s]
  Passed  TC_CRM_013_SearchCustomer_ByFullName_ShouldReturnExactMatch          [ 8 s]
  Passed  TC_CRM_014_SearchCustomer_ByPartialName_ShouldReturnMatchingResults  [ 8 s]
  Passed  TC_CRM_015_SearchCustomer_NonExistentName_ShouldReturnNoResults      [ 5 s]
  Passed  TC_CRM_016_DeleteCustomer_ShouldRemoveFromList                       [ 8 s]

Total tests: 15  |  Passed: 15  |  Failed: 0
Total time: 2 phút 47 giây
```

---

## 5. LỊCH SỬ COMMIT (Git Log)

| Commit | Nội dung |
|--------|----------|
| 0145b03 | chore: scaffold đồ án thực tập Dolibarr |
| 0fc64f5 | feat: automation skeleton + smoke tests |
| 8163933 | feat(CRM): automation TC_CRM_005/006 BVA tên KH + ExcelDataReader |
| a655b0a | test(CRM): thêm TC_CRM_007-011 vào sheet Test Cases |
| 3f1ff2e | feat(CRM): automation TC_CRM_007-011, fix bóc tách tên KH, regex redirect & sanitize XSS |
| 8b5eee1 | feat(CRM): fix delete locator, add cleanup test, implement TC_CRM_012-016 |
| c186e67 | fix(crm): sửa 3 bug P1/P2 sau code review (TC_016, pagination, SearchByName race) |
| d7a7e83 | fix(smoke): IsOnLoginPage dùng WaitHelper tránh StaleElementReferenceException |

---

## 6. KHÓ KHĂN VÀ BÀI HỌC

| # | Khó khăn | Bài học |
|---|----------|---------|
| 1 | Dolibarr dùng jQuery UI Dialog thay vì window.confirm() | Phải inspect DOM thực tế, dùng XPath động cho .ui-dialog-buttonset |
| 2 | GetDisplayedName() dính địa chỉ/quốc gia | Dùng JavaScript cloneNode + xóa phần tử con trước khi lấy textContent |
| 3 | StaleElementReferenceException sau page reload | Không giữ IWebElement reference qua page load — luôn dùng WaitHelper.WaitVisible() |
| 4 | wait.Until(URL condition) bị bypass khi form POST | Wait theo trạng thái DOM, không wait theo URL |
| 5 | Cleanup bỏ sót khi DB >100 KH test | Loop qua tất cả trang, không giả định 1 trang là đủ |

---

## 7. HƯỚNG PHÁT TRIỂN TIẾP THEO

- Module Sales: Automation luồng tạo Báo giá (Proposal) → Hóa đơn (Invoice).
- API Testing: Postman collection test REST API Dolibarr (mở rộng +3đ).
- Đối chiếu DB: Script Python truy vấn MariaDB xác minh dữ liệu sau mỗi test.
- ExtentReports: Thay thế TestContext.WriteLine bằng báo cáo HTML trực quan.
- Refactor Login(): Dùng [ClassInitialize] để login 1 lần cho cả class, tiết kiệm ~2-3s/test.

---

Tài liệu tổng hợp từ: git log, kết quả dotnet test thực tế, nhật ký AI Log trong Dolibarr_TestCases.xlsx.
Cập nhật lần cuối: 2026-10-03

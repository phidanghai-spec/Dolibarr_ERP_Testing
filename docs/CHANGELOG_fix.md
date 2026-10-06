# CHANGELOG_fix.md — Tóm tắt khắc phục sau Code Review (Vòng 2)
**Ngày thực hiện:** 2026-10-03 (hoàn thiện 2026-10-04)  
**Người thực hiện:** Đặng Hải Phi  
**Nhánh làm việc:** `fix/review-20261003` (không push trực tiếp lên main)  

---

## 1. ĐÃ HOÀN THÀNH

### PHẦN 1 — Sửa kịch bản Cleanup và cơ chế bảo vệ DB
- **Prefix Whitelist:** Chỉ lọc và xóa các KH có tên bắt đầu bằng tiền tố test đã định nghĩa: `AUTO_`, `KH_Edit_`, `SearchFull_`, `SearchPart_`, `Delete_`, `TC005_`, `TC006_`, `TC_`.
- **Bảo vệ dữ liệu nền:** Whitelist bảo vệ tuyệt đối dữ liệu nền `socid=1`, `socid=2`, `Cong ty ABC`, `Cong ty BCD`. Bỏ qua và ghi log `[Cleanup SKIP]` đối với mọi KH không rõ nguồn gốc.
- **Tiền tố đồng nhất:** Mọi test tạo KH động đều dùng tiền tố `AUTO_`:
  - `TC_CRM_012`: `AUTO_Edit_Orig_{suffix}` -> `AUTO_Edit_New_{suffix}`
  - `TC_CRM_013`: `AUTO_SearchFull_{suffix}`
  - `TC_CRM_014`: `AUTO_SearchPart_{suffix}`
  - `TC_CRM_016`: `AUTO_Delete_{suffix}`
- **Ghi nhận ngoại lệ prefix (Lỗ hổng prefix được kiểm soát):**
  - `TC_CRM_008`: Tên đúng 1 ký tự (`"A"` - kiểm thử giá trị biên min length N=1). Không thể thêm tiền tố `AUTO_` vì sẽ làm sai lệch yêu cầu kiểm thử 1 ký tự.
  - `TC_CRM_010`: Ký tự đặc biệt và thẻ HTML `O'Brien & Cong ty <Test> "123"` (kiểm thử XSS/sanitize theo đúng mẫu từ Excel).
  - `TC_CRM_011`: Tên 128 ký tự tiếng Việt có dấu Unicode đọc từ Excel.
  - *Cơ chế dọn dẹp:* Trong điều kiện bình thường, khối `finally { CleanupCreatedCustomer(); }` của mỗi test luôn xóa ngay theo URL trực tiếp (`_createdCustomerUrl`). Nếu trường hợp đặc biệt bị crash đột ngột giữa chừng (mất điện, ngắt tiến trình), các KH ngoại lệ này sẽ bị bỏ qua bởi whitelist của script Cleanup để tránh xóa nhầm dữ liệu, và cần dọn dẹp thủ công.
- **Cấu hình DRY-RUN mặc định:** `CleanupDryRun = !string.Equals(Environment.GetEnvironmentVariable("CLEANUP_DRY_RUN"), "false", StringComparison.OrdinalIgnoreCase);`. Mặc định luôn là DRY-RUN (chỉ liệt kê KH khớp tiền tố, không xóa). Chỉ xóa thật khi đặt `$env:CLEANUP_DRY_RUN = "false"`.
- **Guard chống loop vô hạn:** Tự động ngắt sau 3 vòng lặp liên tiếp nếu không có KH nào mới được xóa (`MaxStaleRounds = 3`).
- **Lưu bằng chứng DRY-RUN:** Đã chạy lại và lưu nguyên văn ra file [`docs/dryrun_20261003.log`](file:///d:/Projects/DoAnThucTap_Dolibarr/docs/dryrun_20261003.log).
  - *Kết luận chính xác từ log:* **Không có KH nào khớp prefix trong trang đầu (limit=100)**. (Không suy diễn là toàn bộ DB sạch).

---

### PHẦN 2 — Chuẩn hóa Assert theo hành vi quan sát thực tế
- **TC_CRM_007 (Tên để trống):**
  - Thuộc tính `required` trong HTML là `False`.
  - Dolibarr chặn ở phía server: không redirect, trả thông báo lỗi `Field 'Third-party name' is required`.
  - Đã loại bỏ hoàn toàn cấu trúc rẽ nhánh `if/else`, chốt một assert duy nhất kiểm chứng chặn server-side.
- **TC_CRM_009 (Tên chỉ gồm khoảng trắng):**
  - Dolibarr trim khoảng trắng ở server và xử lý như tên rỗng: không redirect, trả lỗi `Field 'Third-party name' is required`.
  - Chốt assert `Assert.IsFalse(redirected)` và thông báo lỗi chứa `'required'`.
- **TC_CRM_010 (Ký tự đặc biệt & XSS):**
  - Đã xóa hoàn toàn hàm tự chế `SimulateDolibarrSanitize`.
  - Đối chiếu trực tiếp với kết quả sanitize thực tế của Dolibarr 22.0.4: loại bỏ thẻ `<Test>` và dấu ngoặc kép `"`, giữ dấu nháy đơn `'` và ký tự `&`. Tên lưu thực tế: `"O'Brien & Cong ty 123"`.
  - Đã thêm log kiểm chứng `[TC_CRM_010] Actual name: 'O\'Brien & Cong ty 123' (expected: 'O\'Brien & Cong ty 123')` và xuất hiện chính xác trong file log.
- **TC_CRM_015 & TC_CRM_016 (Tìm kiếm không tồn tại & Xóa KH):**
  - Chuyển assert từ điều kiện lỏng `count == 0 || hasNoResult` (OR) sang điều kiện chặt chẽ `count == 0 && hasNoResult` (AND) dựa trên kết quả chạy thật cả 2 điều kiện đều thỏa mãn.

---

### PHẦN 3 — Cập nhật tài liệu hướng dẫn README.md
- Cập nhật lệnh chạy test mặc định:
  ```powershell
  dotnet test --filter "TestCategory!=Cleanup"
  ```
- Tách riêng hướng dẫn chạy kịch bản Cleanup với chế độ DRY-RUN và chế độ xóa thật khi có xác nhận.

---

### PHẦN 4 — Chạy test kiểm chứng và lưu log bằng chứng
- **Lệnh thực thi:**
  ```powershell
  dotnet build && dotnet test --filter "TestCategory=CRM" --logger "console;verbosity=detailed" > docs/test_run_after_fix_20261003.log 2>&1
  ```
- **Kết quả tổng kết nguyên văn từ log:**
  ```
  Test Run Successful.
  Total tests: 12
       Passed: 12
   Total time: 2.7653 Minutes
  ```
- **File bằng chứng:** [`docs/test_run_after_fix_20261003.log`](file:///d:/Projects/DoAnThucTap_Dolibarr/docs/test_run_after_fix_20261003.log).
- **Trích xuất dòng TC_CRM_010:**
  ```
  [TC_CRM_010] Actual name: 'O'Brien & Cong ty 123' (expected: 'O'Brien & Cong ty 123')
  [TC_CRM_010 PASS] Luu va hien thi dung: 'O'Brien & Cong ty 123'. URL: http://localhost/dolibarr/societe/card.php?socid=92
  ```

---

### PHẦN 5 — Cập nhật file Excel `Dolibarr_TestCases.xlsx`
Đã sao lưu trước khi chỉnh sửa: `testcases/Dolibarr_TestCases.before_fix_20261003.xlsx`.
1. **Sheet Test Cases:**
   - `TC_CRM_006`: Cập nhật `Loại = Auto`.
   - Phân rã `Function ID`:
     - `F-CRM-01`: Tạo khách hàng mới (`TC_CRM_005` đến `TC_CRM_011`).
     - `F-CRM-02`: Chỉnh sửa thông tin khách hàng (`TC_CRM_012`).
     - `F-CRM-03`: Tìm kiếm khách hàng (`TC_CRM_013` đến `TC_CRM_015`).
     - `F-CRM-04`: Xóa khách hàng (`TC_CRM_016`).
   - Cột `Actual`: Điền giá trị thực tế quan sát được từ `test_run_after_fix_20261003.log` (không sao chép từ Expected).
   - Cột `Trạng thái`: `Pass` cho toàn bộ 12 test case.
   - Cột `Ngày chạy`: `2026-10-03`.
   - Cột `Minh chứng`: Đường dẫn ảnh screenshot tương ứng cho từng test.
2. **Sheet Summary:**
   - Tổng hợp số lượng test case, Auto, Manual, Pass, Fail theo từng Function ID (`F-CRM-01` đến `F-CRM-04`) và dòng Tổng cộng (12/12 Pass - 100%).
3. **Sheet Traceability:**
   - Ma trận truy vết từ Use Case (`UC-01` đến `UC-04`) -> Function ID -> Scenario -> Test ID -> Trạng thái.
4. **Sheet Bug Report:**
   - Cập nhật header chuẩn theo skill `bug-report`. Đợt chạy test ngày 2026-10-03 đạt 100% Pass, không phát hiện lỗi tồn đọng.
5. **Sheet AI Log:**
   - Thay thế toàn bộ cụm từ chung chung `(Người dùng xác nhận)` ở các dòng trước bằng nội dung người dùng đã chỉnh sửa/phê duyệt cụ thể.
   - Thêm dòng ghi nhận công việc STT 5 cho đợt Code Review và chuẩn hóa ngày 2026-10-03.

---

## 2. VIỆC CHƯA LÀM VÀ LÝ DO

| Hạng mục | Lý do |
|---|---|
| Chạy xóa dữ liệu thật (`CLEANUP_DRY_RUN=false`) | Đã chạy cleanup thật ngày 2026-10-03 (xóa 0 KH test do không có KH nào khớp prefix trong trang đầu, log docs/cleanup_real_20261003.log); sau đó chạy lại DRY-RUN xác nhận 0 KH tồn đọng. |
| Đặt tiền tố `AUTO_` cho TC_CRM_008 và TC_CRM_011 | Ràng buộc kỹ thuật kiểm thử giá trị biên (BVA 1 ký tự và Unicode 128 ký tự tiếng Việt) không cho phép gắn prefix vào chuỗi test data. Đã có khối finally xóa trực tiếp theo URL; nếu crash sẽ dọn tay. |
| Merge nhánh `fix/review-20261003` vào `main` | Tuân thủ quy trình kiểm thử và review: chỉ commit trên nhánh tính năng/sửa lỗi, gửi log và Excel để người dùng kiểm chứng trước khi merge. |

---

## 3. RỦI RO CÒN LẠI

- **Khách hàng rác phát sinh khi test bị crash giữa chừng:**
  - Khách hàng do `TC_CRM_008` (tên `"A"`), `TC_CRM_010` (tên `O'Brien & Cong ty <Test> "123"`), `TC_CRM_011` (tên tiếng Việt có dấu 128 ký tự Unicode) không có tiền tố trong whitelist của kịch bản Cleanup (`TestNamePrefixes`).
  - Trong điều kiện chạy bình thường, các khách hàng này luôn được dọn dẹp tức thì qua URL trực tiếp trong khối `finally { CleanupCreatedCustomer(); }`.
  - Tuy nhiên, nếu lần chạy test bị crash đột ngột giữa chừng (mất điện, kill process WebDriver), các khách hàng này sẽ không được dọn tự động bởi script Cleanup (nhằm tránh nguy cơ xóa nhầm dữ liệu nghiệp vụ khác); khi đó cần can thiệp kiểm tra và dọn dẹp bằng tay.
- **Lý do chưa sửa:** Tên khách hàng được đọc từ Excel và có ràng buộc độ dài nghiêm ngặt theo kỹ thuật phân tích giá trị biên BVA (biên min length $N=1$ ở `TC_008`, biên Unicode 128 ký tự ở `TC_011`, hoặc chuỗi kiểm thử XSS đặc thù ở `TC_010`) nên không thể gắn thêm tiền tố `AUTO_`.

---

## 4. CẬP NHẬT ĐỢT REVIEW VÒNG 3 (2026-10-05)

### 4.1. Điều chỉnh tính độc lập của Expected TC_CRM_009
- **Hiện trạng:** Expected ban đầu tại commit `a655b0a` (2026-09-25) được ghi chú là "CHƯA XÁC ĐỊNH TRƯỚC" và cập nhật theo log quan sát tại commit `18b97f9`.
- **Điều chỉnh:** Sửa lại ghi chú trong `docs/UseCases.md` (commit `061a596`) và `Dolibarr_TestCases.xlsx`: Yêu cầu "tên chỉ gồm khoảng trắng bị chặn submit" được bổ sung ngày 2026-10-05 dựa trên hành vi quan sát được trong kiểm thử; **chưa được giảng viên xác nhận**.
- **Đánh giá kiểm thử:** TC_CRM_009 Pass theo quan sát thực tế (chứng minh ứng dụng vẫn hoạt động như hành vi ghi nhận), không ghi nhận là spec chính thức của hệ thống.

### 4.2. Phương án kiểm thử TC_CRM_010 (XSS & Ký tự đặc biệt)
- **Mục tiêu XSS:** Test kiểm tra dữ liệu lưu và hiển thị dưới dạng văn bản thuần, thẻ HTML `<Test>` không được thực thi trên trang chi tiết (an toàn XSS), không bị lỗi PHP Fatal, và giữ lại phần dữ liệu hợp lệ `O'Brien`.
- **Ký tự ngoặc kép `"`:** Server Dolibarr tự động loại bỏ `"`. Tuy nhiên mã kiểm thử **chưa assert ký tự `"`** do chưa có đặc tả chính thức xác nhận đây là hành vi chủ đích hay phụ. Nội dung này đang chờ câu hỏi gửi giảng viên xác nhận trước khi chốt assert trong code.

### 4.3. Phát hiện lỗi và mở BUG_001 (Placeholder `__ID__` trong URL Redirect)
- **Hiện tượng:** Khi tạo khách hàng mới, Dolibarr 22.0.4 chuyển hướng về URL:
  `http://localhost/dolibarr/societe/card.php?id=__ID__&socid={id}`
- **Nguyên nhân gốc từ Dolibarr:** Mã nguồn Dolibarr 22.0.4 (`htdocs/societe/card.php` dòng 204 & 644–648) gán trường ẩn `$backtopage` mặc định `.../societe/card.php?id=__ID__`. Khi redirect, code thực hiện `preg_replace('/--IDFORBACKTOPAGE--/', ...)` thay vì `__ID__`, sau đó nối thêm `&socid={id}`, khiến chuỗi `id=__ID__` bị giữ nguyên trên URL trình duyệt.
- **Vi phạm đặc tả:** `UC-02 §4` yêu cầu URL sau khi tạo phải có dạng hợp lệ `societe/card.php?socid={id}` (hoặc `id={id}` số).
- **Hành động khắc phục trên bộ test:**
  - Thêm `Assert 1b` trong `TC_CRM_010`: `Assert.IsFalse(Driver.Url.Contains("__ID__"))`.
  - Giữ hàm `CustomerDetailPage.NormalizeCustomerUrl()` **chỉ cho mục đích dọn dẹp dữ liệu (cleanup)** sau test, tuyệt đối không dùng để che giấu lỗi hay làm đẹp kết quả kiểm thử.
  - Test `TC_CRM_010` **Fail** ở Assert 1b để phơi bày lỗi ứng dụng. Minh chứng log thực tế: `docs/test_run_crm_tc010_fail_bug001_20261005.log` và log kiểm chứng độc lập `docs/test_run_crm_009_010_20261005_1619.log`.
- **Mở BUG_001:** Đã ghi nhận bug mới vào sheet `Bug Report` trong `Dolibarr_TestCases.xlsx`, cập nhật trạng thái `TC_CRM_010` thành `Fail`.

### 4.4. Cải tiến sheet `Summary` sang công thức tự động
- **Hiện trạng kiểm chứng:** Kiểm tra file backup `Dolibarr_TestCases.backup_20261005.xlsx` bằng `data_only=False` xác nhận toàn bộ ô Passed/Failed và Tỷ lệ Pass trước đây đều là số nguyên và chuỗi tĩnh (`'100%'`), không chứa công thức.
- **Cải tiến thiết kế:** Chuyển đổi toàn bộ sheet `Summary` sang công thức Excel động:
  - Cột Tổng số TC: `=COUNTIF('Test Cases'!$C:$C, B{r})`
  - Cột Auto / Manual: `=COUNTIFS('Test Cases'!$C:$C, B{r}, 'Test Cases'!$H:$H, "Auto")` / `"Manual"`
  - Cột Passed / Failed / Not Run: `=COUNTIFS('Test Cases'!$C:$C, B{r}, 'Test Cases'!$N:$N, "Pass")` / `"Fail"` / `"Not Run"`
  - Dòng Tổng cộng: `=SUM(...)`
  - Cột Tỷ lệ Pass: `=IF(D{r}>0, G{r}/D{r}, 0)` định dạng `0.0%` (kết quả hiển thị tự động: F-CRM-01 đạt 85.7%, Tổng CRM đạt 91.7%).

### 4.5. Xử lý file log không hợp lệ (`docs/archive_invalid/`)
- Di chuyển file log chạy thử `docs/test_run_crm_tc010_20261005.log` (do sử dụng hàm chuẩn hóa che lỗi URL) vào thư mục `docs/archive_invalid/`.
- Bổ sung file `docs/archive_invalid/README.md` giải thích chi tiết lý do không sử dụng file log này làm bằng chứng nghiệm thu, tránh nhầm lẫn cho hội đồng chấm.

### 4.6. Câu hỏi gửi giảng viên hướng dẫn
- Soạn thảo câu hỏi gửi giảng viên về việc xử lý ký tự ngoặc kép `"` và thẻ `<tag>` phía server của Dolibarr để chốt căn cứ cho Expected của `TC_CRM_010` trước khi viết báo cáo quá trình.

---

## 5. CẬP NHẬT ĐỢT REVIEW VÒNG 4 & TỐI ƯU TOÀN DIỆN (2026-10-06)

### 5.1. Khử triệt để 100% `Thread.Sleep` và `SpinWait` trong toàn bộ Solution
- **Hiện trạng phát hiện:** Trong `SpecialInvoiceAndStockTests.cs` tồn tại 10 vị trí sử dụng `System.Threading.Thread.Sleep(...)`, và trong `ProposalDetailPage.cs`, `InvoiceDetailPage.cs` tồn tại 3 vị trí `SpinWait.SpinUntil(() => false, ...)`. Vi phạm quy tắc bắt buộc số 3 trong `AGENTS.md` và nguyên tắc thiết kế POM.
- **Hành động khắc phục:**
  - `SpecialInvoiceAndStockTests.cs`:
    - `TC_SAL_009`: Thay `Sleep(500)` và `Sleep(2000)` bằng `WaitHelper.WaitClickable` cho nút xác nhận, `WaitHelper.WaitGone` cho `div.ui-dialog`, và `WebDriverWait` chờ badge trạng thái chuyển sang `Canceled / Abandoned`.
    - `TC_SAL_010`: Thay `Sleep(800)` và `Sleep(1500)` bằng `WaitHelper.WaitClickable` và `WebDriverWait` chờ mã tham chiếu Credit note cập nhật tiền tố `AV...` hoặc `IC...`.
    - `EnterPayment`: Thay `Sleep(800)`, `Sleep(1500)` và `Sleep(2000)` bằng `WaitHelper.WaitVisible` chờ form thanh toán và `WebDriverWait` chờ URL điều hướng hoàn tất giao dịch.
    - `PerformStockCorrection`: Thay `Sleep(800)` và `Sleep(2000)` bằng `WebDriverWait` chờ URL thoát khỏi màn hình điều chỉnh kho.
    - `TC_STK_004`: Thay `Sleep(1500)` bằng `WebDriverWait` chờ lưu xong thẻ sản phẩm (`!Url.Contains("action=edit")`).
  - `ProposalDetailPage.cs` & `InvoiceDetailPage.cs`:
    - Thay thế `SpinWait` trong `GetReference()` bằng `WebDriverWait` tìm kiếm element `RefTitle` có timeout.
    - Thay thế `SpinWait` trong `AddPredefinedProduct` bằng điều kiện chờ hoàn tất AJAX của jQuery (`jQuery.active == 0`).
- **Kết quả quét kiểm tra:** 0 `Thread.Sleep`, 0 `SpinWait` trong toàn bộ mã nguồn kiểm thử.

### 5.2. Chuẩn hóa cấu hình REST API & Database (Loại bỏ Hard-code)
- **Hiện trạng:** `ApiIntegrationTests.cs` hard-code URL `http://localhost/dolibarr/api/index.php` và API Key `dolibarr_test_api_key_2026`. Vi phạm Rule 8 của `selenium-csharp-pom`.
- **Hành động khắc phục:**
  - Bổ sung property `ApiBaseUrl` và `ApiKey` vào `TestConfig.cs`, ưu tiên đọc từ biến môi trường `DOLIBARR_API_KEY`, sau đó đến `appsettings.local.json`.
  - Cập nhật `ApiIntegrationTests.cs` sử dụng `TestConfig.ApiBaseUrl` và `TestConfig.ApiKey`.
  - Cập nhật file mẫu `appsettings.example.json` và file cấu hình thực tế `appsettings.local.json` với đầy đủ các section `Dolibarr (API)` và `Database (MariaDB)`.

### 5.3. Xử lý an toàn xung đột file Excel (Excel File Lock)
- **Cải tiến:** Bổ sung khối bắt ngoại lệ `catch (IOException ioEx)` trong `ExcelResultUpdater.cs`. Khi file Excel đang được mở bởi ứng dụng khác (Excel lock), hệ thống ghi log cảnh báo rõ ràng thay vì ném unhandled exception làm gián đoạn tiến trình kiểm thử.

### 5.4. Đồng bộ AI Log STT 12
- Ghi nhận đầy đủ thông tin vào sheet `AI Log` của `testcases/Dolibarr_TestCases.xlsx` cho đợt Rà soát & Tối ưu toàn diện ngày 2026-10-06.

### 5.5. Nghiệm thu kiểm thử thực tế trên hệ thống (Verification Run)
- `dotnet build`: **0 Warning(s), 0 Error(s)**.
- `SmokeTests`: **2/2 Pass (100%)** — 21.5s.
- `DatabaseVerificationTests`: **4/4 Pass (100%)** — 18.7s.
- `ApiIntegrationTests`: **4/4 Pass (100%)** — 6.8s.
- `ExtentReportManager`: Báo cáo HTML Dashboard tự động sinh tại `TestResults/ExtentReports/Dolibarr_TestReport.html`.



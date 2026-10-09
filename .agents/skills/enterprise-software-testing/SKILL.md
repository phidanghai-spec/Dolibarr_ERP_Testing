---
name: enterprise-software-testing
description: Khung quy trình kiểm thử phần mềm toàn diện (End-to-End QA & Automation Framework) chuẩn ISTQB cho ứng dụng web, ERP, CRM và hệ thống doanh nghiệp. Áp dụng khi bắt đầu dự án test mới hoặc chuẩn hóa quy trình test: quản lý baseline DB mốc sạch, thiết kế test case Excel 6 sheet, tự động hóa POM (Selenium C# / Playwright), kiểm thử đa tầng (UI, API, DB), và nghiệm thu độc lập minh bạch.
---

# Khung Quy Trình Kiểm Thử Phần Mềm Doanh Nghiệp (Enterprise QA Playbook)

Khung quy trình kiểm thử chuẩn mực được đúc kết từ dự án kiểm thử ERP/CRM phức tạp, áp dụng tiêu chuẩn quốc tế **ISTQB Foundation & Advanced**, kỹ thuật **Page Object Model (POM)** và phương pháp **Kiểm thử đa tầng (Multi-tier Testing)**.

---

## 1. Triết Lý & Nguyên Tắc Cốt Lõi (Core Principles)

1. **Tuyệt đối không bịa số liệu kiểm thử (Zero Hallucination):**
   - Kết quả Manual (Actual, Pass/Fail, Evidence) chỉ lấy từ người thực hiện kiểm thử thật.
   - Kết quả Automation chỉ lấy từ console log của lần chạy thật có Exit Code và Timestamp.
   - Khi test Fail, **giữ nguyên Fail** và phân loại: do bug phần mềm (`BUG_xxx`) hay do môi trường/DOM flaky.
2. **Quản lý Mốc sạch Dữ liệu (Clean Baseline First):**
   - Mọi chu kỳ test bắt buộc bắt đầu và kết thúc tại mốc sạch (Baseline Database).
   - Tách biệt hoàn toàn dữ liệu kiểm thử (Test Data) khỏi logic kiểm thử (Code/Scripts).
3. **Traceability 100% (Ma trận truy vết 2 chiều):**
   - Mọi Test Case phải gắn liền với một Yêu cầu nghiệp vụ / Use Case cụ thể (`Use Case -> Function ID -> Test Case ID`).
4. **Không Hard-code dữ liệu động:**
   - Dữ liệu sinh tự động phải dùng tiền tố/hậu tố ngẫu nhiên (`AUTO_<Timestamp>`, `Guid`).
   - Đọc lại mã tham chiếu động từ hệ thống sau khi tạo (ví dụ: mã hóa đơn, mã khách hàng), không giả định mã tăng dần.

---

## 2. Cấu Trúc Thư Mục Chuẩn Cho Một Dự Án Kiểm Thử

```text
<project-root>/
├── docs/                     # Tài liệu đặc tả: TestPlan.md, UseCases.md, Báo cáo quá trình (.docx)
├── testcases/                # File quản lý test: <Project>_TestCases.xlsx (6 sheet chuẩn mực)
├── automation/               # Mã nguồn kiểm thử tự động (C# / TS / Python)
│   └── <Project>Tests/
│       ├── Pages/            # Page Object Model (chỉ chứa Locators và Thao tác UI)
│       ├── Tests/            # Test Classes (chỉ chứa Luồng nghiệp vụ và Assertions)
│       ├── Helpers/          # DriverFactory, WaitHelper, DbHelper, ScreenshotHelper, ExtentReports
│       ├── TestData/         # Dữ liệu kiểm thử cấu hình (JSON / Excel Reader)
│       └── appsettings.local.json  # Cấu hình môi trường (URL, credentials - GIT-IGNORED)
├── evidence/                 # Minh chứng chạy test
│   ├── manual/               # Ảnh chụp/video kiểm thử thủ công
│   └── automation/           # Ảnh chụp màn hình tự động khi Pass/Fail
├── db/                       # Bản dump cơ sở dữ liệu mốc sạch baseline (<db>_clean_baseline.sql)
├── tools/                    # Script hỗ trợ vận hành (start-service, restore-db, run-demo)
├── logs/                     # File lưu toàn bộ console log thực tế khi chạy test suite
└── .agents/skills/           # Các kỹ năng chuyên biệt của AI Pair Programmer
```

---

## 3. Quy Trình 5 Giai Đoạn Triển Khai Kiểm Thử (5-Phase QA Lifecycle)

### Giai đoạn 1: Khảo sát Nghiệp vụ & Dữ liệu Nền (SUT & Baseline Setup)
1. **Phân tích Use Case:** Xác định các Actor, luồng chính (Main Flow), luồng phụ (Alternative Flow) và các điểm biên (Edge Cases).
2. **Khởi tạo Database Baseline:**
   - Cài đặt hệ thống sạch, cấu hình các tham số bắt buộc.
   - Tạo dữ liệu nền ban đầu chuẩn mực (tài khoản mẫu, danh mục sản phẩm, kho hàng mẫu).
   - Xuất bản dump SQL mốc sạch: `db/<db_name>_clean_baseline.sql`.
3. **Xây dựng script tự động hoàn nguyên DB:**
   - Tạo `tools/restore-db.ps1` (hoặc `.sh`): tự động nạp lại file dump sạch trước và sau khi chạy test suite.
   - Đảm bảo an toàn bảo mật: Lấy mật khẩu DB từ biến môi trường hoặc file local không commit lên Git.

---

### Giai đoạn 2: Thiết kế Ma trận Test Cases Chuẩn ISTQB (Excel 6 Sheets)
Quản lý tập trung tại `testcases/<Project>_TestCases.xlsx` gồm 6 sheet:

1. **`Summary`:** Dashboard tổng hợp tự động bằng công thức Excel (`COUNTIFS`, `SUM`). Thống kê số lượng, tỷ lệ Pass/Fail theo từng Phân hệ và Chức năng.
2. **`Test Cases`:** Chứa 100% test case với đầy đủ 19 cột tiêu chuẩn:
   - `Test ID`: Quy ước `TC_<MODULE>_<NNN>` (ví dụ: `TC_CRM_001`, `TC_SAL_001`, `TC_STK_001`).
   - `Module` & `Function ID`: Phân hệ và mã chức năng (`F-<MOD>-<NN>`).
   - `Scenario` & `Tiêu đề`: Mô tả mục tiêu kiểm thử ngắn gọn, rõ ràng.
   - `Kỹ thuật thiết kế`: Ghi rõ 1 trong 5 kỹ thuật ISTQB:
     * **BVA (Boundary Value Analysis):** Kiểm tra các giá trị biên (0, 1, Max, Max+1, độ dài chuỗi 128/129 ký tự, thuế 0%, 100%).
     * **EP (Equivalence Partitioning):** Phân vùng giá trị hợp lệ / không hợp lệ, định dạng số, ký tự đặc biệt, Unicode có dấu.
     * **ST (State Transition):** Kiểm tra vòng đời chứng từ (Draft -> Validated -> Paid / Canceled).
     * **DT (Decision Table):** Bảng quyết định kết hợp điều kiện (thuế suất, chiết khấu, quy tắc trừ kho).
     * **EG (Error Guessing):** Đoán lỗi bất thường, SQL Injection (`' OR '1'='1`), XSS (`<script>`), tương tranh đồng thời.
   - `Ưu tiên`: `P1` (Core - Bắt buộc), `P2` (Mở rộng/Bổ sung), `P3` (Ít ảnh hưởng).
   - `Loại thực thi`: `Auto` hoặc `Manual`.
   - `Tiền điều kiện`, `Các bước`, `Test Data`, `Expected`: **Expected phải cụ thể, có số liệu định lượng kiểm chứng được (tổng tiền, trạng thái, số lượng tồn kho)**. Tuyệt đối cấm viết chung chung như *"hệ thống hoạt động bình thường"*.
   - `Actual`, `Trạng thái` (Pass / Fail / Not Run / Blocked), `Ngày chạy`, `Minh chứng` (đường dẫn ảnh/log), `Bug ID`, `Nguồn`.
3. **`Traceability`:** Ma trận truy vết 2 chiều: `Use Case ID <-> Function ID <-> Test Case ID`.
4. **`Test Data`:** Bảng dữ liệu kiểm thử độc lập dùng cho Data-Driven Testing.
5. **`Bug Report`:** Báo cáo lỗi chi tiết: `Bug ID (BUG_NNN)`, `Severity` (Blocker/Major/Normal/Minor), `Steps to Reproduce`, `Expected`, `Actual`, `Screenshot`, `Trạng thái sửa`.
6. **`AI Log`:** Nhật ký minh bạch học thuật ghi chép từng đóng góp của AI (Mục đích, Lời nhắc, Kết quả AI, Kiểm chứng của người dùng, Đánh giá chất lượng).

---

### Giai đoạn 3: Xây dựng Framework Automation POM Chuẩn Mực
1. **Nguyên tắc phân tầng Page Object Model (POM):**
   - **`Pages/`:** Chỉ chứa Locators (XPath, CSS, Id) và các method thao tác giao diện (`EnterUsername`, `ClickSubmit`, `GetTotalAmount`). **Tuyệt đối không chứa `Assert` trong Page Object**.
   - **`Tests/`:** Kế thừa từ `BaseTest`. Chỉ gọi các method từ Page Object, lấy giá trị trả về và thực hiện `Assert` định lượng.
2. **Bộ quy tắc vàng chống Flaky Test:**
   - **CẤM TUYỆT ĐỐI `Thread.Sleep(...)`**: Toàn bộ thao tác chờ đợi phải dùng `WebDriverWait` (Explicit Wait) bọc qua `WaitHelper` (chờ Clickable, Visible, Invisible).
   - **Không trộn lẫn ImplicitWait và ExplicitWait**: ImplicitWait đặt về 0 hoặc thời gian rất ngắn.
   - **Đơn luồng xác định (`DoNotParallelize`):** Đối với các ứng dụng ERP/CRM có nhiều Ajax trễ hoặc cơ chế khóa bảng Database, chạy tuần tự an toàn để tránh nghẽn I/O và race condition.
   - **Auto-Screenshot:** Tự động chụp ảnh toàn màn hình khi có bất kỳ Test nào bị `Fail` và chụp ảnh xác nhận ở bước cuối cùng của test thành công.

---

### Giai đoạn 4: Kiểm Thử Đa Tầng Mở Rộng (UI + API + Database)
Để đạt điểm chất lượng cao nhất và kiểm thử toàn diện:
1. **Tầng Giao diện (UI E2E):** Selenium WebDriver / Playwright kiểm thử trải nghiệm người dùng thực tế.
2. **Tầng Giao tiếp Dịch vụ (REST API Integration):**
   - Bỏ qua giao diện UI, gọi thẳng REST API (bằng RestSharp, HttpClient, hoặc Postman/Newman).
   - Kiểm tra mã phản hồi HTTP (200, 201, 400, 401, 404), schema JSON và thời gian phản hồi.
3. **Tầng Dữ liệu Ngầm (Database Integrity Verification):**
   - Kết nối trực tiếp cơ sở dữ liệu (MySQL / PostgreSQL / SQL Server).
   - Truy vấn ngầm đối chiếu các bảng quan trọng sau khi thao tác trên UI:
     * Dữ liệu khách hàng đã lưu đúng chuỗi Unicode chưa?
     * Tổng tiền và trạng thái chứng từ có đúng mã trạng thái nội bộ không (`fk_statut = 1`)?
     * Bảng lịch sử biến động kho (`stock_movement`) có sinh đúng bút toán trừ kho không?
4. **Báo cáo Trực quan (HTML Living Reports):**
   - Tích hợp **ExtentReports** hoặc **Allure Report**: Hiển thị biểu đồ tròn Pass/Fail, phân loại Category (Smoke, CRM, Sales, DB, API), thời gian chạy và đính kèm ảnh chụp lỗi trực tiếp trong báo cáo.

---

### Giai đoạn 5: Nghiệm Thu Độc Lập & Báo Cáo Không "Làm Đẹp" Số Liệu
1. **Lưu trữ Console Log Thực tế:**
   - Chạy lệnh test và chuyển hướng toàn bộ output ra file text:
     ```powershell
     dotnet test <Project>.csproj --logger "console;verbosity=detailed" *>&1 | Tee-Object -FilePath "logs/test-run-log.txt" -Encoding utf8
     ```
2. **Quy trình Kiểm chứng Chéo 4 Góc (4-Way Cross Verification):**
   - Góc 1: Bảng `Summary` & `Test Cases` trong file Excel.
   - Góc 2: File log thực tế [logs/test-run-log.txt](logs/test-run-log.txt).
   - Góc 3: Kế hoạch kiểm thử [docs/TestPlan.md](docs/TestPlan.md).
   - Góc 4: Commit Hash trên Git repository.
   - **Tiêu chuẩn:** Số lượng Total, Passed, Failed và Not Run giữa 4 góc này phải khớp số học 100%.

---

## 4. Cheat Sheet Các Lệnh CLI Chuẩn Dành Cho Tester

| Mục đích | Lệnh PowerShell / Bash | Ghi chú |
|:---|:---|:---|
| **Hoàn nguyên DB sạch** | `pwsh tools\restore-db.ps1` | Đưa database về snapshot baseline |
| **Build kiểm tra cú pháp** | `dotnet build automation\<Project>Tests.csproj` | Kiểm tra 0 Error, 0 Warning |
| **Liệt kê danh sách test** | `dotnet test automation\<Project>Tests.csproj -t` | Kiểm tra danh sách test methods khả dụng |
| **Chạy toàn bộ & lưu log** | `dotnet test automation\<Project>Tests.csproj --logger "console;verbosity=detailed" *>&1 \| Tee-Object -FilePath "logs\test-run-log.txt"` | Chạy nghiệm thu và tạo file log minh chứng |
| **Chạy lọc theo Category** | `dotnet test --filter "TestCategory=Smoke"` | Chạy Smoke tests nhanh |
| **Chạy 1 test bắt lỗi** | `dotnet test --filter "Name~TC_CRM_010"` | Chạy đơn lẻ 1 ca kiểm thử |
| **Demo luồng cho hội đồng**| `pwsh tools\run-demo-ui.ps1` | Tự động mở trình duyệt chạy demo trực quan |

---

## 5. Danh Mục Kiểm Tra Nhanh Trước Khi Nghiệm Thu (Exit Checklist)

- [ ] Database đã được khôi phục về bản dump sạch ban đầu chưa?
- [ ] Mọi test case P1 (Core) đã được thực thi và có kết quả rõ ràng chưa?
- [ ] Có tồn tại `Thread.Sleep` nào trong mã nguồn automation không? (Bắt buộc = 0).
- [ ] Mọi ca test Fail có bắt trúng bug thật và đã được tạo Bug Report kèm ảnh minh chứng chưa?
- [ ] File log thực tế đã được lưu vào `logs/` và commit lên Git chưa?
- [ ] Số liệu Pass/Fail trong Excel có khớp 100% với log chạy thực tế không?
- [ ] Mã nguồn và tài liệu đã được push sạch sẽ lên nhánh chính (`main`) chưa?

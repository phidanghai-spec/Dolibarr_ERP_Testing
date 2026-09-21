# AGENTS.md — Đồ án thực tập: Kiểm thử Dolibarr ERP & CRM

Người thực hiện: Đặng Hải Phi (23DH112608). Hạn nộp: đầu tháng 12/2026.
Ngôn ngữ làm việc: **tiếng Việt** (tên biến, tên class, tên hàm bằng tiếng Anh).

## Bối cảnh
- Hệ thống được test: Dolibarr 22.0.4, cài bằng DoliWamp trên máy local (KHÔNG nằm trong repo này).
- Phạm vi 3 module: CRM (Third parties), Sales & Invoicing (Proposals, Invoices), Stock.
- Phân chia: **Automation** = CRUD khách hàng, luồng chuẩn báo giá → hóa đơn. **Manual** = hóa đơn đặc biệt (hủy, credit note, thanh toán một phần), Stock.
- Rubric chấm: Core 5đ + Mở rộng 3đ + AI hỗ trợ 2đ, xét trên thiết kế, lập trình, kiểm tra chất lượng.
- Mở rộng dự kiến (làm sau khi Core xong): API testing bằng Postman, đối chiếu DB tự động, báo cáo ExtentReports.

## Cấu trúc thư mục
- `docs/`       : file Word (Test Plan, Use Case, báo cáo quá trình)
- `testcases/`  : file Excel `Dolibarr_TestCases.xlsx` (test case, test data, bug, AI Log)
- `automation/` : solution C# (Selenium + MSTest + EPPlus, POM)
- `evidence/`   : ảnh/clip minh chứng (manual, automation)
- `db/`         : bản dump DB mốc sạch
- `tools/`      : script hỗ trợ (bật/tắt Dolibarr)
- `.agents/skills/` : kỹ năng chuyên biệt, đọc SKILL.md tương ứng trước khi làm việc

## Quy tắc BẮT BUỘC
1. **Không bịa kết quả test.** Kết quả Manual (Actual, Pass/Fail, ảnh) chỉ do người dùng cung cấp. Kết quả Automation chỉ lấy từ lần chạy thật; chạy được thì chạy và dán log.
2. **Không sửa/xóa** thư mục cài Dolibarr (`D:\Projects\dolibarr`) và file cài đặt `.exe`.
3. Code Automation: **cấm `Thread.Sleep`** (dùng WebDriverWait), **mỗi test phải có Assert giá trị cụ thể** (tổng tiền, trạng thái, tồn kho...), không chỉ "không lỗi".
4. Không hard-code mã tham chiếu Dolibarr (PR..., IN...): đọc lại giá trị sau khi tạo. Dữ liệu tự sinh dùng hậu tố ngẫu nhiên.
5. Mọi thay đổi lớn phải nói rõ đã sửa file nào. Sau khi sửa code: chạy `dotnet build` (và test nếu được) rồi báo kết quả thật.
6. Ghi vào sheet **AI Log** mỗi lần AI hỗ trợ một việc đáng kể (xem skill `ai-log`).
7. Không tự ý xóa file; hỏi trước nếu cần xóa hoặc đổi cấu trúc thư mục.
8. Khi thiếu thông tin (URL Dolibarr, tài khoản test...), hỏi thay vì đoán. Không ghi mật khẩu thật vào repo.

## Quy ước đặt tên
- Test case: `TC_<MODULE>_<NNN>` (CRM, SAL, STK). Bug: `BUG_<NNN>`. Use Case: `UC-<NN>`. Chức năng: `F-<MODULE>-<NN>`.
- Mức ưu tiên: P1 (bắt buộc), P2 (bổ sung), P3 (ít ảnh hưởng).

## Cấu hình Dolibarr quan trọng
- Bật rule "Decrease real stocks on validation of customer invoice/credit note" (Setup > Modules > Stocks). Không bật thì tồn kho không giảm khi validate hóa đơn.
- Dữ liệu nền: khách hàng Cong ty ABC, Cong ty BCD; sản phẩm PR001–PR004; kho KHO001; tồn ban đầu 50/30/40/89.

## Môi trường chạy (cập nhật 2026-09-21)
- **Repo GitHub**: https://github.com/phidanghai-spec/Dolibarr_ERP_Testing (nhánh `main`)
- **Thư mục làm việc**: `D:\Projects\DoAnThucTap_Dolibarr`. Thư mục cài Dolibarr: `D:\Projects\dolibarr` (không thuộc repo, không được sửa).
- **Dolibarr**: 22.0.4 (DoliWamp), MariaDB 10.6.5, database tên `dolibarr`.
- **BaseUrl**: `http://localhost/dolibarr` ; trang đăng nhập: `http://localhost/dolibarr/index.php` ; tài khoản test: `admin` ; mật khẩu lấy từ biến môi trường `DOLIBARR_ADMIN_PASSWORD` (không lưu ở đâu khác).
- **Dịch vụ Windows**: `doliwampmysqld` (database) và `doliwampapache` (web). Khởi động thủ công, bật database trước.
- **Cách bật Dolibarr**: `pwsh tools\start-dolibarr.ps1` (tự xin quyền Admin). Kết quả đúng: 2 dịch vụ Running và HTTP 200/302. Cách thủ công: `Start-Service doliwampmysqld ; Start-Service doliwampapache` (PowerShell Admin).
- **Cách tắt**: `pwsh tools\stop-dolibarr.ps1`
- **Máy**: .NET SDK 9.0.317, Git 2.52, Python 3.13.7, Chrome đã cài, PowerShell 7.
- **Quy tắc cho agent**: KHÔNG tự bật/tắt dịch vụ Windows; nếu Dolibarr không chạy thì dừng và nhắc người dùng chạy `start-dolibarr.ps1`.

---
name: bug-report
description: Định dạng bug report trong sheet Bug Report của Excel. Đọc khi ghi hoặc rà soát bug.
---
# Bug report

Cột: `Bug ID | Tiêu đề | Module | Test ID liên quan | Môi trường (Dolibarr 22.0.4, Chrome ...) | Bước tái hiện | Kết quả thực tế | Kết quả mong đợi | Mức độ | Ưu tiên | Trạng thái | Ảnh/clip | Ngày | Ghi chú`

- Mức độ: Critical (mất dữ liệu, chặn luồng chính) · Major (sai tiền, sai tồn kho) · Minor (sai thông báo/hiển thị) · Trivial.
- Trạng thái: New → Confirmed → Reported/Won't fix → Verified → Closed (Dolibarr là mã nguồn mở, thường chỉ ghi nhận và phân tích).
- Bước tái hiện phải đủ để người khác làm lại từ DB mốc sạch. Có ảnh hoặc clip.
- Phân biệt: lỗi thật của Dolibarr, hành vi mặc định gây hiểu nhầm (ghi là Observation), và lỗi của test script. Chỉ người dùng xác nhận bug từ Manual.
- Mỗi bug phải liên kết ngược tới Test ID bị Fail.

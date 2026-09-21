---
name: test-case-design
description: Cách viết test case, test scenario, test data trong file Excel Dolibarr_TestCases.xlsx với kỹ thuật thiết kế test. Đọc khi tạo hoặc sửa test case.
---
# Thiết kế test case

## Cột bắt buộc của sheet Test Case
`Test ID | Module | Function ID | Scenario | Tiêu đề | Kỹ thuật | Ưu tiên | Loại (Manual/Auto) | Tiền điều kiện | Các bước | Test data | Expected | Actual | Trạng thái | Ngày chạy | Minh chứng | Bug ID | Nguồn (Tự thiết kế / AI gợi ý-đã chỉnh)`

## Kỹ thuật thiết kế (ghi vào cột Kỹ thuật)
Phân vùng tương đương (EP), giá trị biên (BVA), bảng quyết định (DT), chuyển trạng thái (ST), đoán lỗi (EG).

## Quy tắc
- Mỗi test case kiểm tra MỘT điều; Expected phải cụ thể, kiểm chứng được (có giá trị, trạng thái, số lượng). Cấm "hiển thị đúng", "hoạt động bình thường".
- Có cả case tích cực và tiêu cực. Với số lượng, giá, chiết khấu: dùng BVA.
- Test Data cho Automation nằm ở sheet Test Data, mỗi dòng một bộ dữ liệu, cột tên rõ ràng (không dùng chuỗi `Key: Value`).
- Trạng thái: Not Run / Pass / Fail / Blocked. Chỉ người dùng hoặc lần chạy thật mới được đổi.
- Sau khi thêm test case, cập nhật sheet Traceability (UC → Scenario → TC) và Summary.
- Không đụng vào Actual/Trạng thái của case Manual nếu người dùng chưa cung cấp.

---
name: dolibarr-domain
description: Kiến thức nghiệp vụ Dolibarr 22.0.4 cho đồ án (CRM, báo giá, hóa đơn, kho, dữ liệu mẫu, rule trừ kho). Đọc khi thiết kế test case hoặc viết Page Object.
---
# Dolibarr domain

## Luồng chuẩn (UC-01)
Tạo báo giá cho khách hàng → thêm dòng sản phẩm → Validate → Signed → Tạo hóa đơn từ báo giá → Validate hóa đơn → (tồn kho giảm nếu đã bật rule) → ghi nhận thanh toán.
- Không tạo được hóa đơn từ báo giá chưa Signed.
- Báo giá: Draft → Validated → Signed → Billed. Hóa đơn: Draft → Validated → Paid / Cancelled.

## Cấu hình phải nhớ
- Stocks: bật "Decrease real stocks on validation of customer invoice/credit note". Mặc định KHÔNG bật (phát hiện khi smoke test: PR003 40 → 35 sau khi bật rule và bán 5).
- Module đã bật: Third Parties, Proposals, Invoices, Products, Stocks.

## Dữ liệu nền
- Khách hàng: Cong ty ABC, Cong ty BCD.
- Sản phẩm: PR001, PR002, PR003, PR004 (đều có giá bán). Kho: KHO001.
- Tồn ban đầu: PR001=50, PR002=30, PR003=40, PR004=89.
- Bộ dữ liệu riêng cho Automation phải TÁCH khỏi bộ của Manual (để tồn kho không lệch nhau).

## Đặc thù giao diện (rủi ro locator)
- Mã tham chiếu tự sinh; URL có token bảo mật nên đi qua menu, không hard-code URL trang chi tiết.
- Combo box dạng select2 và hộp thoại xác nhận jQuery cần helper riêng.
- Đọc locator từ HTML thật (DevTools), không đoán.

## Cần người dùng cung cấp
URL Dolibarr local, tên đăng nhập test, cách khôi phục DB mốc sạch.

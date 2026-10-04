# Ghi chú về các file log kiểm thử (Test Run Logs)

1. **Tính toàn vẹn của bằng chứng kiểm thử:**
   - Hai file log `docs/test_run_before_fix_20261003.log` và `docs/test_run_after_fix_20261003.log` là đầu ra gốc (raw output) trực tiếp từ các lần chạy kiểm thử trước đó, được lưu trữ nguyên bản và tuyệt đối không được chỉnh sửa nội dung.

2. **Về chuỗi `id=__ID__` trong log cũ:**
   - Các dòng hiển thị URL chứa `id=__ID__` trong hai file log cũ trên là do lỗi hiển thị chuỗi log trong mã nguồn kiểm thử tại thời điểm chạy (giá trị placeholder ID chưa được thay thế vào URL log), hoàn toàn không ảnh hưởng đến logic kiểm thử hay kết quả Pass/Fail của các test case.

3. **Khắc phục trong lần chạy final:**
   - Vấn đề hiển thị này đã được xử lý triệt để trong mã nguồn từ lần chạy kiểm thử chính thức cuối cùng (final run).
   - Minh chứng tại `docs/test_run_final_20261003.log` và các lần chạy sau đều ghi nhận đầy đủ, chính xác URL thực tế từ trình duyệt (chứa `socid` và `id` thật từ Dolibarr), không còn xuất hiện chuỗi `__ID__`.

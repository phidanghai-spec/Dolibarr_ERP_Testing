# docs/archive_invalid/README.md — Lưu trữ các file log kiểm thử không hợp lệ

Thư mục này lưu trữ các file log chạy kiểm thử phát sinh trong quá trình phát triển nhưng **không đủ điều kiện làm bằng chứng nghiệm thu**, nhằm tránh việc nhầm lẫn với các kết quả kiểm thử chính thức.

---

## 1. Danh sách file lưu trữ

### `test_run_crm_tc010_20261005.log`
- **Thời gian chạy:** 2026-10-05 (~15:58).
- **Trạng thái ghi nhận:** `Passed TC_CRM_010_CreateCustomer_SpecialChars_ObserveXssHandling` (URL: `http://localhost/dolibarr/societe/card.php?socid=108`).
- **Lý do KHÔNG HỢP LỆ:**
  - Lần chạy này được thực hiện khi mã nguồn automation còn chứa hàm `CustomerDetailPage.NormalizeCustomerUrl()` can thiệp loại bỏ chuỗi `id=__ID__&` khỏi URL trước khi ghi log và assert.
  - Trên thực tế, ứng dụng Dolibarr 22.0.4 (`htdocs/societe/card.php` dòng 204 & 644–648) chuyển hướng về `societe/card.php?id=__ID__&socid={id}`. Việc chuẩn hóa ở client đã che giấu lỗi thật của hệ thống, vi phạm nguyên tắc kiểm thử trung thực của `AGENTS.md` (biến lỗi ứng dụng thành test pass).
- **Log hợp lệ thay thế:**
  - `docs/test_run_crm_tc010_fail_bug001_20261005.log`
  - `docs/test_run_crm_009_010_20261005_1619.log`
  *(Cả hai file log trên đều chạy với assertion URL thô, ghi nhận FAIL chính xác tại Assert 1b để làm bằng chứng cho `BUG_001`).*

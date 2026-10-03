# CHANGELOG_fix.md — Tóm tắt fix sau Code Review
**Ngày thực hiện:** 2026-10-03  
**Người thực hiện:** AI (Antigravity) + Đặng Hải Phi (xác nhận)  
**Commit liên quan:** b122204, 8c12819

---

## 1. ĐÃ LÀM

### PHẦN 1 — Sửa Cleanup_DeleteAllTestCustomers (CrmCustomerTests.cs)

| # | Hạng mục | Trạng thái |
|---|----------|-----------|
| 1a | Chỉ xóa KH có tên bắt đầu bằng prefix: AUTO_, KH_Edit_, SearchFull_, SearchPart_, Delete_, TC005_, TC006_, TC_ | DONE |
| 1a | Whitelist bảo vệ: socid=1, socid=2, "Cong ty ABC", "Cong ty BCD" + bỏ qua (SKIP log) KH không rõ nguồn gốc | DONE |
| 1b | Thêm prefix "AUTO_" vào tên KH: TC_CRM_012 (AUTO_Edit_), TC_013 (AUTO_SearchFull_), TC_014 (AUTO_SearchPart_), TC_016 (AUTO_Delete_) | DONE |
| 1c | Thêm biến cấu hình CLEANUP_DRY_RUN (mặc định true từ env var). Khi true: chỉ log, KHÔNG xóa. | DONE |
| 1d | Guard chống loop vô hạn: thoát sau 3 vòng liên tiếp không xóa thêm được KH nào | DONE |
| 1e | Assert cuối: `Assert.AreEqual(0, totalFailed, ...)` thay vì `IsTrue(failedOk \|\| deletedOk)` | DONE |
| 1f | [TestCategory("Cleanup")] đã có từ trước. README chưa cập nhật lệnh filter — ghi vào CHƯA LÀM | CHỜ README |
| 1g | Chạy DRY-RUN: **KẾT QUẢ — DB HIỆN TẠI SẠCH, 0 KH test cần xóa** (các test đã cleanup trong finally). | DONE - DRY-RUN PASS, KHÔNG CÓ GÌ ĐỂ XÓA |

**Log DRY-RUN (2026-10-03T23:53):**
```
[Cleanup] CHE DO DRY-RUN: chi liet ke, KHONG XOA. De xoa that: dat CLEANUP_DRY_RUN=false.
[Cleanup DRY-RUN DONE] Danh sach tren la nhung KH SE BI XOA khi chay voi CLEANUP_DRY_RUN=false.
DUNG LAI — xac nhan voi nguoi dung truoc khi xoa that.

→ Không có KH nào khớp prefix test trong DB. DB sạch.
```
> **DỪNG LẠI theo yêu cầu 1g.** Không xóa thật. Chờ xác nhận nếu cần xóa.

---

### PHẦN 2 — Sửa Assert các Test Case yếu (CrmCustomerTests.cs)

**Dữ liệu hành vi thực tế từ log chạy 2026-10-03T23:47–23:50:**

| TC | Hành vi thực tế quan sát | Fix đã thực hiện | Trạng thái |
|----|--------------------------|-----------------|-----------|
| TC_CRM_007 | hasRequired=False, server trả "Field 'Third-party name' is required", Redirected=False | Bỏ nhánh if/else hasRequired, chốt 1 hành vi server-side với assert cụ thể | DONE |
| TC_CRM_009 | Input 10 spaces, Redirected=False, ErrorMessage='Field Third-party name is required' | Đổi method name ShouldBeBlocked, assert IsFalse(redirected) + lỗi chứa 'required' | DONE |
| TC_CRM_010 | actualName="O'Brien & Cong ty 123" (match với SimulateDolibarrSanitize) | Xóa SimulateDolibarrSanitize(), dùng const ExpectedSavedName="O'Brien & Cong ty 123" từ log | DONE |
| TC_CRM_015 | Count=0 && HasNoResultMessage=True | OR → AND (cả 2 phải đúng) | DONE |
| TC_CRM_016 | Count=0 && HasNoResultMessage=True sau khi xóa | OR → AND (cả 2 phải đúng) | DONE |
| TC_CRM_013/014 | Đã dùng suffix GUID làm search term (unique), count assert dùng AreEqual(1, count) | TC_013 assert count==1 đã có. TC_014 assert count>=1 (cần quan sát thêm vì partial search có thể trả nhiều) | KHÔNG SỬA TC_014 — count>=1 hợp lý vì partial match |

---

### PHẦN 4 — Git

| # | Hạng mục | Trạng thái |
|---|----------|-----------|
| 4a | `git rm --cached testcases/Dolibarr_TestCases.backup.xlsx testcases/Dolibarr_TestCases.backup2.xlsx` | DONE (commit b122204) |
| 4a | Thêm pattern `testcases/Dolibarr_TestCases.backup*.xlsx` và `before_fix_*.xlsx` vào .gitignore | DONE |
| 4b | Commit riêng: `chore: go backup xlsx`, `fix(cleanup): prefix filter + DRY_RUN + ...` | DONE |
| 4c | Build thành công trước khi push: `Build succeeded. 0 Warning(s) 0 Error(s)` | DONE |

---

## 2. BUILD VÀ TEST LOG THẬT

### dotnet build (2026-10-03T23:53)
```
Determining projects to restore...
All projects are up-to-date for restore.
DolibarrTests -> D:\Projects\DoAnThucTap_Dolibarr\automation\DolibarrTests\bin\Debug\net9.0\DolibarrTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.41
```

### dotnet test (CRM suite, trước khi fix, 2026-10-03T23:47–23:50)
```
Total tests: 12  |  Passed: 12  |  Failed: 0
Total time:  3.4017 Minutes
```
> Tất cả 12 CRM test PASS trước khi fix. Sau khi fix code, build lại thành công.

---

## 3. CHƯA LÀM VÀ LÝ DO

| # | Hạng mục | Lý do chưa làm |
|---|----------|----------------|
| PHẦN 1f (README) | Chỉnh README: lệnh chạy mặc định `--filter "TestCategory!=Cleanup"` | Chờ xác nhận xong PHẦN 1g trước; sẽ cập nhật trong commit tiếp theo |
| PHẦN 3 (Excel) | Sửa sheet Test Cases, Summary, Traceability, Bug Report, AI Log | Excel cần mở bằng openpyxl — sẽ làm trong bước tiếp theo, cần Dolibarr đang chạy để lấy giá trị Actual từ lần chạy thật |
| PHẦN 5 Test sau fix | Chạy lại toàn bộ suite sau khi sửa code để xác nhận 15/15 PASS | Cần chạy sau khi user xác nhận OK phần code |

---

## 4. CÂU HỎI CẦN XÁC NHẬN (tối đa 2)

**Câu 1 — Cleanup thật:** DB hiện tại sạch (0 KH test rác). Có cần chạy cleanup thật (`CLEANUP_DRY_RUN=false`) không, hay bỏ qua vì không cần?

**Câu 2 — Excel Actual:** Để điền cột Actual và Ngày chạy thật trong Excel, cần kết quả từ lần chạy test ngày hôm nay (2026-10-03). Mình đã có log. Cho phép dùng log hôm nay để điền không, hay bạn muốn chạy lại test sau khi sửa xong để có log mới nhất?

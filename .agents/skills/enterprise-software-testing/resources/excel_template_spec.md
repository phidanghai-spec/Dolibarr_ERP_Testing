# Đặc Tả Cấu Trúc File Excel Quản Lý Kiểm Thử (6-Sheet Excel Matrix Spec)

Mẫu cấu trúc file `TestCases.xlsx` chuẩn quản lý kiểm thử chuyên nghiệp:

---

## 1. Sheet `Summary` (Bảng Tổng Hợp Điều Hành)

### A. Cột và cấu trúc hàng
| Cột A | Cột B | Cột C | Cột D | Cột E | Cột F | Cột G | Cột H | Cột I | Cột J |
|:---|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Module** | **Function ID** | **Tên chức năng** | **Tổng số TC** | **Auto** | **Manual** | **Passed** | **Failed** | **Not Run** | **Tỷ lệ Pass (%)** |

### B. Công thức động chuẩn (Dòng dữ liệu thứ `i`, giả định dòng bắt đầu từ 2):
- **Tổng số TC (Cột D):**
  `=COUNTIF('Test Cases'!$C:$C, B2)` (Đếm theo Function ID ở cột C của sheet Test Cases).
- **Số Auto (Cột E):**
  `=COUNTIFS('Test Cases'!$C:$C, B2, 'Test Cases'!$H:$H, "Auto")` (với cột H là Loại thực thi).
- **Số Manual (Cột F):**
  `=COUNTIFS('Test Cases'!$C:$C, B2, 'Test Cases'!$H:$H, "Manual")`.
- **Số Passed (Cột G):**
  `=COUNTIFS('Test Cases'!$C:$C, B2, 'Test Cases'!$N:$N, "Pass")` (với cột N là Trạng thái).
- **Số Failed (Cột H):**
  `=COUNTIFS('Test Cases'!$C:$C, B2, 'Test Cases'!$N:$N, "Fail")`.
- **Số Not Run (Cột I):**
  `=D2 - G2 - H2` (hoặc `=COUNTIFS('Test Cases'!$C:$C, B2, 'Test Cases'!$N:$N, "Not Run")`).
- **Tỷ lệ Pass (Cột J):**
  `=IF(D2>0, G2/D2, 0)` (Format cell dạng Percentage `0.0%`).

---

## 2. Sheet `Test Cases` (Danh Sách 100% Test Cases)

### Danh sách 19 cột chuẩn mực:
1. `Test ID`: Mã định danh ca kiểm thử (`TC_<MODULE>_<NNN>`).
2. `Module`: Tên phân hệ (CRM, Sales, Stock, Accounting...).
3. `Function ID`: Mã chức năng (`F-<MOD>-<NN>`).
4. `Scenario`: Ngữ cảnh/Kịch bản kiểm thử.
5. `Tiêu đề`: Tên tóm tắt mục tiêu test.
6. `Kỹ thuật`: 1 trong 5 kỹ thuật ISTQB (`BVA`, `EP`, `ST`, `DT`, `EG`).
7. `Ưu tiên`: Mức độ quan trọng (`P1` - Core, `P2` - Secondary, `P3` - Minor).
8. `Loại`: Phương thức thực thi (`Auto` / `Manual`).
9. `Tiền điều kiện`: Trạng thái hệ thống trước khi thực hiện (dữ liệu mẫu, quyền tài khoản, cấu hình rule).
10. `Các bước thực hiện`: Các bước thao tác tuần tự từng bước 1, 2, 3...
11. `Test Data`: Dữ liệu đầu vào cụ thể (tên, số tiền, số lượng, chuỗi ký tự biên).
12. `Expected`: Kết quả mong đợi **định lượng** (trạng thái chuyển sang gì, tồn kho giảm bao nhiêu, thông báo lỗi chính xác là gì).
13. `Actual`: Kết quả thực tế quan sát được (chỉ ghi khi chạy thật).
14. `Trạng thái`: `Pass`, `Fail`, `Not Run`, `Blocked`.
15. `Ngày chạy`: Ngày thực hiện nghiệm thu (`YYYY-MM-DD`).
16. `Minh chứng`: Đường dẫn tương đối tới ảnh chụp hoặc file log (`evidence/...`).
17. `Bug ID`: Mã lỗi phần mềm nếu Test bị Fail (`BUG_NNN`).
18. `Ghi chú`: Các quan sát kỹ thuật, giới hạn môi trường.
19. `Nguồn`: Nguồn gốc thiết kế (`Tự thiết kế` hoặc `AI gợi ý - Đã kiểm chứng`).

---

## 3. Sheet `Traceability` (Ma Trận Truy Vết)

Đảm bảo yêu cầu nghiệp vụ không bị bỏ sót:
| Use Case ID | Tên Use Case | Function ID | Tên chức năng | Danh sách Test Case ID liên kết | Độ bao phủ |
|:---|:---|:---|:---|:---|:---:|
| `UC-01` | Quản lý Khách hàng | `F-CRM-01` | Tạo mới đối tác | `TC_CRM_001`, `TC_CRM_002`, ... | 100% |

---

## 4. Sheet `Test Data` (Quản Lý Dữ Liệu Kiểm Thử)

Tách biệt hoàn toàn dữ liệu kiểm thử, hỗ trợ Data-Driven Testing:
| Data ID | Mô tả bộ dữ liệu | Param 1 (Tên/Mã) | Param 2 (Số lượng) | Param 3 (Đơn giá) | Param 4 (Chiết khấu) | Mục đích test |
|:---|:---|:---|:---|:---|:---|:---|
| `TD_01` | Hàng bán tiêu chuẩn | `PR001` | `2` | `100000` | `0%` | Luồng chuẩn |
| `TD_02` | Hàng bán chiết khấu biên | `PR002` | `1` | `150000` | `100%` | Kiểm thử biên 100% |

---

## 5. Sheet `Bug Report` (Báo Cáo Lỗi Phần Mềm Thực Tế)

Cấu trúc báo cáo lỗi chuẩn kỹ sư QA:
1. `Bug ID`: `BUG_001`, `BUG_002`...
2. `Test Case liên quan`: `TC_CRM_010`.
3. `Tiêu đề bug`: Tóm tắt lỗi ngắn gọn, súc tích.
4. `Mức độ nghiêm trọng (Severity)`: `Blocker`, `Critical`, `Major`, `Minor`.
5. `Mức độ ưu tiên sửa (Priority)`: `High`, `Medium`, `Low`.
6. `Các bước tái hiện (Steps to Reproduce)`: Liệt kê chi tiết để lập trình viên tái hiện được 100%.
7. `Kết quả mong đợi (Expected Result)`.
8. `Kết quả thực tế (Actual Result)`.
9. `Ảnh chụp màn hình (Screenshot / Evidence Link)`.
10. `Nguyên nhân kỹ thuật (Root Cause Analysis)`: Phân tích sâu mã nguồn hoặc logic backend gây lỗi.
11. `Trạng thái`: `New`, `Open`, `Fixed`, `Verified`, `Closed`.

---

## 6. Sheet `AI Log` (Nhật Ký Minh Bạch Ứng Dụng AI)

Minh chứng giá trị học thuật và thực tiễn (ghi điểm phần AI Hỗ trợ):
| STT | Ngày | Mục đích công việc | Câu lệnh Prompt / Yêu cầu người dùng | Kết quả AI sinh ra | Hành động kiểm chứng của người dùng | Đánh giá chất lượng (%) |
|:---:|:---:|:---|:---|:---|:---|:---:|
| 1 | 2026-10-08 | Thiết kế test case BVA cho trường Name | Yêu cầu sinh biên 128 và 129 ký tự | AI gợi ý 2 test case `TC_CRM_005`, `TC_CRM_006` | Chạy trên UI thật: 128 lưu thành công, 129 bị cắt | 100% Chấp nhận |

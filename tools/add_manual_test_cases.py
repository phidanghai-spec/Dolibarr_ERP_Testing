# -*- coding: utf-8 -*-
import sys, io, openpyxl

sys.stdout.reconfigure(encoding='utf-8')

wb = openpyxl.load_workbook('testcases/Dolibarr_TestCases.xlsx')

# 1. THÊM 8 MANUAL TEST CASES VÀO SHEET TEST CASES
ws_tc = wb['Test Cases']

manual_cases = [
    # TC_SAL_009
    [
        'TC_SAL_009', 'Sales', 'F-SAL-03',
        'Xử lý hóa đơn đặc biệt -- Hủy hóa đơn (Abandon/Cancel)',
        'Hủy hóa đơn khách hàng ở trạng thái Chưa thanh toán (Unpaid)',
        'ST', 'P1', 'Manual',
        'Hóa đơn bán hàng đang ở trạng thái Chưa thanh toán (Unpaid) và chưa ghi nhận khoản thanh toán nào.',
        '1. Mở menu Billing | Payment > Invoices > Chọn hóa đơn trạng thái Unpaid.\n2. Nhấn nút "Cancel" / "Abandon" (Hủy hóa đơn).\n3. Nhập lý do hủy: "Khách hàng đổi ý hủy đơn".\n4. Nhấn Xác nhận (Confirm) trong hộp thoại.',
        'Lý do hủy: "Khách hàng đổi ý hủy đơn"',
        'Trạng thái hóa đơn chuyển từ Unpaid sang Canceled/Abandoned (Bị hủy). Badge trạng thái màu xám/gạch ngang. Nút ghi nhận thanh toán bị ẩn/vô hiệu hóa.',
        None, 'Not Run', None, None, None,
        'Kiểm tra luồng hủy hóa đơn chưa thanh toán.', 'Tự thiết kế'
    ],
    # TC_SAL_010
    [
        'TC_SAL_010', 'Sales', 'F-SAL-03',
        'Xử lý hóa đơn đặc biệt -- Hóa đơn hoàn tiền / điều chỉnh (Credit note)',
        'Tạo hóa đơn hoàn tiền (Credit note / Avoir) từ hóa đơn gốc đã xác thực',
        'ST', 'P1', 'Manual',
        'Hóa đơn khách hàng gốc (Standard invoice) đã được xác thực (Validated) hoặc đã thanh toán (Paid).',
        '1. Mở trang chi tiết hóa đơn gốc.\n2. Nhấn nút "Create credit note" (Tạo hóa đơn hoàn tiền).\n3. Kiểm tra thông tin đối tác kế thừa, chọn hoàn trả sản phẩm PR001 số lượng 1.\n4. Nhấn "Create draft" để tạo hóa đơn hoàn tiền nháp.\n5. Nhấn "Validate" để xác thực hóa đơn.',
        'Hóa đơn gốc: SI...; Sản phẩm hoàn: PR001, số lượng: 1',
        'Hóa đơn Credit note được tạo thành công với mã AV... (Avoir). Trạng thái chuyển sang Validated. Số tiền tổng mang giá trị âm hoặc đối trừ công nợ khách hàng.',
        None, 'Not Run', None, None, None,
        'Kiểm tra quy trình tạo Credit note hoàn trả tiền/hàng trong Dolibarr.', 'Tự thiết kế'
    ],
    # TC_SAL_011
    [
        'TC_SAL_011', 'Sales', 'F-SAL-03',
        'Xử lý hóa đơn đặc biệt -- Thanh toán từng phần (Partial Payment)',
        'Ghi nhận thanh toán đợt 1 (một phần số tiền) cho hóa đơn chưa thanh toán',
        'BVA', 'P1', 'Manual',
        'Hóa đơn khách hàng ở trạng thái Unpaid (Validated) với tổng tiền phải trả > 0 (ví dụ tổng tiền TTC = 220,000 VND).',
        '1. Mở trang chi tiết hóa đơn Unpaid.\n2. Nhấn nút "Enter payment" (Ghi nhận thanh toán).\n3. Nhập số tiền thanh toán đợt 1 nhỏ hơn tổng tiền (ví dụ: 100,000 VND).\n4. Chọn phương thức thanh toán: Tiền mặt (Cash), chọn tài khoản quỹ/ngân hàng.\n5. Nhấn "Save" (hoặc "Pay").',
        'Tổng tiền hóa đơn: 220,000 VND. Số trả đợt 1: 100,000 VND. Phương thức: Cash.',
        'Khoản thanh toán đợt 1 được ghi nhận thành công. Trạng thái hóa đơn chuyển thành "Started" (Đã bắt đầu thanh toán / Partially paid). Số tiền còn nợ (Remaining to pay) hiển thị chính xác là 120,000 VND.',
        None, 'Not Run', None, None, None,
        'Kiểm tra tính toán số dư công nợ giảm trừ chính xác khi thanh toán từng đợt.', 'Tự thiết kế'
    ],
    # TC_SAL_012
    [
        'TC_SAL_012', 'Sales', 'F-SAL-03',
        'Xử lý hóa đơn đặc biệt -- Thanh toán hoàn tất (Full Payment & Close)',
        'Ghi nhận thanh toán đợt 2 đủ số tiền còn lại và đóng hóa đơn thành Paid',
        'ST', 'P1', 'Manual',
        'Hóa đơn đang ở trạng thái Started (Partially paid) với số nợ còn lại là 120,000 VND (kế thừa từ TC_SAL_011).',
        '1. Mở trang chi tiết hóa đơn đang có nợ 120,000 VND.\n2. Nhấn "Enter payment".\n3. Nhập số tiền đúng bằng số còn nợ: 120,000 VND.\n4. Chọn phương thức thanh toán, chọn tài khoản quỹ/ngân hàng.\n5. Nhấn Lưu.',
        'Số tiền thanh toán: 120,000 VND (bằng đúng số nợ còn lại).',
        'Hệ thống ghi nhận thanh toán thành công. Số tiền còn nợ về đúng 0 VND. Trạng thái hóa đơn tự động chuyển thành "Paid" (Đã thanh toán đủ). Badge hiển thị màu xanh lá cây.',
        None, 'Not Run', None, None, None,
        'Kiểm tra hoàn tất thanh toán chuyển trạng thái sang Paid.', 'Tự thiết kế'
    ],
    # TC_STK_001
    [
        'TC_STK_001', 'Stock', 'F-STK-02',
        'Điều chỉnh tồn kho thủ công (Stock Correction)',
        'Điều chỉnh tăng tồn kho thủ công (Stock Increase Correction) cho sản phẩm PR002',
        'EP', 'P1', 'Manual',
        'Đã đăng nhập quyền thủ kho; Sản phẩm PR002 đang có tồn khả dụng trong kho KHO001 là N đơn vị.',
        '1. Điều hướng: Products | Services > Products > Chọn PR002 > Vào tab "Stock".\n2. Nhấn nút "Correct stock" (Điều chỉnh tồn kho).\n3. Tại ô Số lượng (Number of units), chọn thao tác "Add" (Cộng) và nhập: 10.\n4. Nhập lý do (Inventory code / Label): "Kiểm kê định kỳ phát hiện thừa".\n5. Nhấn "Save" (hoặc "Record").',
        'Sản phẩm: PR002, Kho: KHO001, Thao tác: Tăng 10, Lý do: "Kiem ke thua"',
        'Tồn kho của PR002 tại KHO001 tăng thêm đúng 10 đơn vị (Tồn mới = N + 10). Tab "Stock movements" xuất hiện một dòng ghi nhận biến động +10 kèm ngày giờ và người thực hiện.',
        None, 'Not Run', None, None, None,
        'Kiểm tra luồng nhập kho kiểm kê thủ công.', 'Tự thiết kế'
    ],
    # TC_STK_002
    [
        'TC_STK_002', 'Stock', 'F-STK-02',
        'Điều chỉnh tồn kho thủ công (Stock Correction)',
        'Điều chỉnh giảm tồn kho thủ công (Stock Decrease Correction) do hàng hỏng',
        'EP', 'P1', 'Manual',
        'Sản phẩm PR002 đang có tồn khả dụng >= 5 đơn vị tại kho KHO001.',
        '1. Mở tab "Stock" của sản phẩm PR002.\n2. Nhấn nút "Correct stock".\n3. Chọn thao tác "Remove" (Giảm) hoặc nhập số lượng: 5.\n4. Nhập lý do: "Hàng hư hỏng bao bì cần hủy".\n5. Nhấn Lưu.',
        'Sản phẩm: PR002, Kho: KHO001, Thao tác: Giảm 5, Lý do: "Hang hu hong"',
        'Tồn kho của PR002 tại KHO001 giảm đúng 5 đơn vị. Lịch sử biến động ghi nhận dòng xuất giảm -5 kèm lý do hư hỏng.',
        None, 'Not Run', None, None, None,
        'Kiểm tra luồng xuất kho hao hụt kiểm kê.', 'Tự thiết kế'
    ],
    # TC_STK_003
    [
        'TC_STK_003', 'Stock', 'F-STK-02',
        'Điều chỉnh tồn kho thủ công -- Xuất quá số lượng tồn (Negative Stock)',
        'Khảo sát hành vi khi điều chỉnh giảm số lượng tồn kho vượt quá số lượng tồn hiện có',
        'BVA', 'P2', 'Manual',
        'Sản phẩm PR004 có tồn hiện tại là 89 đơn vị tại kho KHO001.',
        '1. Mở tab "Stock" của sản phẩm PR004.\n2. Nhấn "Correct stock".\n3. Chọn thao tác "Remove" và nhập số lượng xuất: 100 (lớn hơn số tồn 89).\n4. Nhập lý do: "Test xuat qua ton".\n5. Nhấn Lưu.',
        'Sản phẩm: PR004, Tồn hiện tại: 89, Số lượng xuất: 100.',
        'Nếu cấu hình Dolibarr chặn tồn âm: Hệ thống báo lỗi và chặn không cho lưu. Nếu cấu hình cho phép tồn âm: Tồn kho giảm về -11 và hiển thị cảnh báo đỏ trên giao diện.',
        None, 'Not Run', None, None, None,
        'Khảo sát hành vi rule tồn kho âm của Dolibarr 22.0.4.', 'Tự thiết kế'
    ],
    # TC_STK_004
    [
        'TC_STK_004', 'Stock', 'F-STK-03',
        'Cảnh báo tồn kho tối thiểu (Stock Limit & Alert Threshold)',
        'Kiểm tra hiển thị cảnh báo khi tồn kho giảm xuống dưới ngưỡng tồn kho tối thiểu (Stock limit)',
        'BVA', 'P1', 'Manual',
        'Sản phẩm PR003 có cấu hình trường "Stock limit for alert" (Ngưỡng cảnh báo tối thiểu), ví dụ Limit = 10. Tồn kho hiện tại đang lớn hơn 10.',
        '1. Vào trang sửa sản phẩm PR003, nhập trường "Stock limit for alert" = 10. Lưu lại.\n2. Thực hiện điều chỉnh giảm tồn kho (Correct stock) để số lượng tồn của PR003 giảm về 8 (nhỏ hơn ngưỡng 10).\n3. Điều hướng đến menu Products | Services > Replenishment (hoặc danh sách sản phẩm Products).\n4. Kiểm tra cảnh báo trạng thái tồn kho của PR003.',
        'Ngưỡng cảnh báo tối thiểu (Stock limit): 10, Số lượng tồn thực tế sau giảm: 8.',
        'Hệ thống hiển thị biểu tượng cảnh báo thiếu hụt tồn kho (màu đỏ/cam) hoặc sản phẩm PR003 xuất hiện trong danh sách "Cần bổ sung tồn kho" (Replenishment) với số lượng thiếu hụt so với tồn mong muốn.',
        None, 'Not Run', None, None, None,
        'Kiểm tra cơ chế cảnh báo tự động khi chạm ngưỡng tồn kho tối thiểu.', 'Tự thiết kế'
    ]
]

start_row = ws_tc.max_row + 1
for r_idx, case_data in enumerate(manual_cases, start_row):
    for c_idx, val in enumerate(case_data, 1):
        ws_tc.cell(r_idx, c_idx, val)

print(f"Da them {len(manual_cases)} Manual Test Cases vao sheet Test Cases (tu dong {start_row} den {ws_tc.max_row})")

# 2. CẬP NHẬT SHEET SUMMARY ĐỦ CẢ 3 MODULE VÀ TỔNG CỘNG
ws_sum = wb['Summary']

summary_rows = [
    # Row 2-5: CRM
    ('CRM', 'F-CRM-01', 'Tạo khách hàng mới (Third-party creation)'),
    ('CRM', 'F-CRM-02', 'Chỉnh sửa thông tin khách hàng (Third-party modification)'),
    ('CRM', 'F-CRM-03', 'Tìm kiếm khách hàng (Third-party search)'),
    ('CRM', 'F-CRM-04', 'Xóa khách hàng (Third-party deletion)'),
    ('TỔNG CRM', 'F-CRM', 'Module CRM (4 chức năng)'), # Row 6

    # Row 7-9: Sales
    ('Sales & Invoicing', 'F-SAL-01', 'Báo giá thương mại (Commercial Proposals)'),
    ('Sales & Invoicing', 'F-SAL-02', 'Hóa đơn bán hàng chuẩn (Proposal to Invoice)'),
    ('Sales & Invoicing', 'F-SAL-03', 'Hóa đơn đặc biệt (Hủy, Credit note, Thanh toán một phần)'),
    ('TỔNG SALES', 'F-SAL', 'Module Sales & Invoicing (3 chức năng)'), # Row 10

    # Row 11-13: Stock
    ('Stock', 'F-STK-01', 'Trừ kho tự động theo hóa đơn (Integration)'),
    ('Stock', 'F-STK-02', 'Điều chỉnh tồn kho thủ công (Stock Correction)'),
    ('Stock', 'F-STK-03', 'Cảnh báo tồn kho tối thiểu (Stock Limit & Alert Threshold)'),
    ('TỔNG STOCK', 'F-STK', 'Module Stock (3 chức năng)'), # Row 14

    # Row 15: GRAND TOTAL
    ('TỔNG CỘNG TOÀN DỰ ÁN', 'ALL', 'Toàn bộ 3 Module (CRM, Sales, Stock)')
]

for idx, (mod, fid, fname) in enumerate(summary_rows, 2):
    r = idx
    ws_sum.cell(r, 1, mod)
    ws_sum.cell(r, 2, fid)
    ws_sum.cell(r, 3, fname)

    if fid in ['F-CRM-01', 'F-CRM-02', 'F-CRM-03', 'F-CRM-04', 'F-SAL-01', 'F-SAL-02', 'F-SAL-03', 'F-STK-01', 'F-STK-02', 'F-STK-03']:
        ws_sum[f'D{r}'] = f"=COUNTIF('Test Cases'!$C:$C, B{r})"
        ws_sum[f'E{r}'] = f'=COUNTIFS(\'Test Cases\'!$C:$C, B{r}, \'Test Cases\'!$H:$H, "Auto")'
        ws_sum[f'F{r}'] = f'=COUNTIFS(\'Test Cases\'!$C:$C, B{r}, \'Test Cases\'!$H:$H, "Manual")'
        ws_sum[f'G{r}'] = f'=COUNTIFS(\'Test Cases\'!$C:$C, B{r}, \'Test Cases\'!$N:$N, "Pass")'
        ws_sum[f'H{r}'] = f'=COUNTIFS(\'Test Cases\'!$C:$C, B{r}, \'Test Cases\'!$N:$N, "Fail")'
        ws_sum[f'I{r}'] = f'=COUNTIFS(\'Test Cases\'!$C:$C, B{r}, \'Test Cases\'!$N:$N, "Not Run")'
        ws_sum[f'J{r}'] = f'=IF(D{r}>0, G{r}/D{r}, 0)'
    elif fid == 'F-CRM':
        ws_sum[f'D{r}'] = '=SUM(D2:D5)'
        ws_sum[f'E{r}'] = '=SUM(E2:E5)'
        ws_sum[f'F{r}'] = '=SUM(F2:F5)'
        ws_sum[f'G{r}'] = '=SUM(G2:G5)'
        ws_sum[f'H{r}'] = '=SUM(H2:H5)'
        ws_sum[f'I{r}'] = '=SUM(I2:I5)'
        ws_sum[f'J{r}'] = f'=IF(D{r}>0, G{r}/D{r}, 0)'
    elif fid == 'F-SAL':
        ws_sum[f'D{r}'] = '=SUM(D7:D9)'
        ws_sum[f'E{r}'] = '=SUM(E7:E9)'
        ws_sum[f'F{r}'] = '=SUM(F7:F9)'
        ws_sum[f'G{r}'] = '=SUM(G7:G9)'
        ws_sum[f'H{r}'] = '=SUM(H7:H9)'
        ws_sum[f'I{r}'] = '=SUM(I7:I9)'
        ws_sum[f'J{r}'] = f'=IF(D{r}>0, G{r}/D{r}, 0)'
    elif fid == 'F-STK':
        ws_sum[f'D{r}'] = '=SUM(D11:D13)'
        ws_sum[f'E{r}'] = '=SUM(E11:E13)'
        ws_sum[f'F{r}'] = '=SUM(F11:F13)'
        ws_sum[f'G{r}'] = '=SUM(G11:G13)'
        ws_sum[f'H{r}'] = '=SUM(H11:H13)'
        ws_sum[f'I{r}'] = '=SUM(I11:I13)'
        ws_sum[f'J{r}'] = f'=IF(D{r}>0, G{r}/D{r}, 0)'
    elif fid == 'ALL':
        ws_sum[f'D{r}'] = '=D6+D10+D14'
        ws_sum[f'E{r}'] = '=E6+E10+E14'
        ws_sum[f'F{r}'] = '=F6+F10+F14'
        ws_sum[f'G{r}'] = '=G6+G10+G14'
        ws_sum[f'H{r}'] = '=H6+H10+H14'
        ws_sum[f'I{r}'] = '=I6+I10+I14'
        ws_sum[f'J{r}'] = f'=IF(D{r}>0, G{r}/D{r}, 0)'

    if fid in ['F-CRM', 'F-SAL', 'F-STK', 'ALL']:
        for c in range(1, 11):
            ws_sum.cell(r, c).font = openpyxl.styles.Font(bold=True)

    ws_sum[f'J{r}'].number_format = '0.0%'

print(f"Da cap nhat sheet Summary voi day du 3 Module va Grand Total (15 dong)")

# 3. CẬP NHẬT SHEET TRACEABILITY ĐẦY ĐỦ CẢ 3 MODULE
ws_tr = wb['Traceability']

# Xoa du lieu cu tu dong 2
if ws_tr.max_row > 1:
    ws_tr.delete_rows(2, ws_tr.max_row)

uc_map = {
    'F-CRM-01': ('UC-02', 'Quản lý thông tin Khách hàng (CRM)'),
    'F-CRM-02': ('UC-02', 'Quản lý thông tin Khách hàng (CRM)'),
    'F-CRM-03': ('UC-02', 'Quản lý thông tin Khách hàng (CRM)'),
    'F-CRM-04': ('UC-02', 'Quản lý thông tin Khách hàng (CRM)'),
    'F-SAL-01': ('UC-01', 'Luồng chuẩn Báo giá -> Hóa đơn (Sales)'),
    'F-SAL-02': ('UC-01', 'Luồng chuẩn Báo giá -> Hóa đơn (Sales)'),
    'F-SAL-03': ('UC-01', 'Luồng chuẩn Báo giá -> Hóa đơn (Sales)'),
    'F-STK-01': ('UC-03', 'Quản lý Kho & Tồn kho (Stock)'),
    'F-STK-02': ('UC-03', 'Quản lý Kho & Tồn kho (Stock)'),
    'F-STK-03': ('UC-03', 'Quản lý Kho & Tồn kho (Stock)'),
}

func_names = {
    'F-CRM-01': 'Tạo khách hàng mới (Third-party creation)',
    'F-CRM-02': 'Chỉnh sửa thông tin khách hàng (Third-party modification)',
    'F-CRM-03': 'Tìm kiếm khách hàng (Third-party search)',
    'F-CRM-04': 'Xóa khách hàng (Third-party deletion)',
    'F-SAL-01': 'Báo giá thương mại (Commercial Proposals)',
    'F-SAL-02': 'Hóa đơn bán hàng chuẩn (Proposal to Invoice)',
    'F-SAL-03': 'Hóa đơn đặc biệt (Hủy, Credit note, Thanh toán một phần)',
    'F-STK-01': 'Trừ kho tự động theo hóa đơn (Integration)',
    'F-STK-02': 'Điều chỉnh tồn kho thủ công (Stock Correction)',
    'F-STK-03': 'Cảnh báo tồn kho tối thiểu (Stock Limit & Alert Threshold)',
}

tr_row = 2
for r in range(2, ws_tc.max_row + 1):
    tid = ws_tc.cell(r, 1).value
    if not tid:
        continue
    fid = ws_tc.cell(r, 3).value
    scenario = ws_tc.cell(r, 4).value
    title = ws_tc.cell(r, 5).value
    loai = ws_tc.cell(r, 8).value
    status = ws_tc.cell(r, 14).value
    
    uc_id, uc_name = uc_map.get(fid, ('UC-??', 'Chua xac dinh'))
    f_name = func_names.get(fid, fid)
    
    ws_tr.cell(tr_row, 1, uc_id)
    ws_tr.cell(tr_row, 2, uc_name)
    ws_tr.cell(tr_row, 3, fid)
    ws_tr.cell(tr_row, 4, f_name)
    ws_tr.cell(tr_row, 5, scenario)
    ws_tr.cell(tr_row, 6, tid)
    ws_tr.cell(tr_row, 7, title)
    ws_tr.cell(tr_row, 8, loai)
    ws_tr.cell(tr_row, 9, status)
    tr_row += 1

print(f"Da dong bo sheet Traceability voi toan bo {tr_row - 2} test cases.")

wb.save('testcases/Dolibarr_TestCases.xlsx')
print('SUCCESSFULLY_UPDATED_EXCEL')

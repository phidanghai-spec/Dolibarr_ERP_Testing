# -*- coding: utf-8 -*-
import sys, io, os, openpyxl

sys.stdout.reconfigure(encoding='utf-8')

files = [
    'testcases/Dolibarr_TestCases.xlsx',
    'testcases/Dolibarr_TestCases.backup_20261005.xlsx'
]

for fpath in files:
    print('==============================')
    print('FILE:', fpath)
    if not os.path.exists(fpath):
        print('  FILE KHONG TON TAI')
        continue
    wb = openpyxl.load_workbook(fpath, data_only=False)
    print('So sheet:', len(wb.sheetnames))
    print('Cac sheet va so dong:')
    for name in wb.sheetnames:
        ws = wb[name]
        print(f'  - {name}: {ws.max_row} dong, {ws.max_column} cot')
    
    print('\n--- KIEM TRA CONG THUC TRONG SUMMARY ---')
    ws_sum = wb['Summary']
    for r in range(1, ws_sum.max_row + 1):
        row_vals = [ws_sum.cell(r, c).value for c in range(1, ws_sum.max_column + 1)]
        if any(row_vals):
            print(f'  Row {r}:', row_vals)

print('\n==============================')
print('CHI TIET FILE testcases/Dolibarr_TestCases.xlsx:')
wb_main = openpyxl.load_workbook('testcases/Dolibarr_TestCases.xlsx', data_only=False)

print('\n--- c) TC_CRM_009 va TC_CRM_010 trong Test Cases ---')
ws_tc = wb_main['Test Cases']
header = [ws_tc.cell(1, c).value for c in range(1, ws_tc.max_column + 1)]
col_map = {name: idx + 1 for idx, name in enumerate(header) if name}
cols_to_print = ['Expected', 'Actual', 'Trang thai', 'Bug ID', 'Ghi chu', 'Minh chung']

for r in range(2, ws_tc.max_row + 1):
    tid = ws_tc.cell(r, 1).value
    if tid in ['TC_CRM_009', 'TC_CRM_010']:
        print(f'[{tid}] (Dong {r}):')
        for cname in cols_to_print:
            cidx = col_map.get(cname)
            val = ws_tc.cell(r, cidx).value if cidx else None
            print(f'  - {cname}: {val}')

print('\n--- d) DONG BUG_001 trong Bug Report ---')
ws_bug = wb_main['Bug Report']
bug_header = [ws_bug.cell(1, c).value for c in range(1, ws_bug.max_column + 1)]
for r in range(2, ws_bug.max_row + 1):
    bid = ws_bug.cell(r, 1).value
    if bid == 'BUG_001':
        print(f'Dong {r} ({bid}):')
        for c in range(1, len(bug_header) + 1):
            hname = bug_header[c-1]
            if hname:
                print(f'  - {hname}: {ws_bug.cell(r, c).value}')

print('\n--- e) DONG STT 8 trong AI Log ---')
ws_ai = wb_main['AI Log']
ai_header = [ws_ai.cell(1, c).value for c in range(1, ws_ai.max_column + 1)]
for r in range(2, ws_ai.max_row + 1):
    stt = ws_ai.cell(r, 1).value
    if stt == 8:
        print(f'Dong {r} (STT 8):')
        for c in range(1, len(ai_header) + 1):
            h = str(ai_header[c-1] or '')
            val = ws_ai.cell(r, c).value
            if any(k in h for k in ['chỉnh', 'Người dùng', 'Nguoi dung', 'học', 'Bài học', 'Bai hoc']):
                print(f'  - {h}: {val}')

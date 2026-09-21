---
name: ai-log
description: Cách ghi sheet AI Log để lấy điểm phần AI hỗ trợ. Đọc sau mỗi việc đáng kể có AI tham gia.
---
# AI Log

Sheet `AI Log` trong `testcases/Dolibarr_TestCases.xlsx`. Cột:
`STT | Ngày | Giai đoạn (Thiết kế / Lập trình / Kiểm tra chất lượng) | Công cụ + model | Việc cần làm | Prompt tóm tắt | Kết quả AI đưa ra | Đánh giá (Đúng / Sai / Phải sửa) | Người dùng đã chỉnh gì | Bài học`

- Ghi mỗi khi AI sinh test case, code, phân tích lỗi, rà soát tài liệu.
- Ghi trung thực cả chỗ AI sai (assert lỏng, locator đoán bừa, bịa kết quả...) và cách phát hiện. Đây là bằng chứng tư duy.
- Không viết thay đánh giá của người dùng: điền phần "Đánh giá" và "Người dùng đã chỉnh gì" dạng gợi ý để người dùng xác nhận.

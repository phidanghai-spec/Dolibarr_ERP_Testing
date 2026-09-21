---
name: selenium-csharp-pom
description: Quy tắc viết code Automation C# (Selenium + MSTest + EPPlus, Page Object Model) cho Dolibarr. Đọc trước khi tạo hoặc sửa bất kỳ file trong automation/.
---
# Selenium C# POM

## Cấu trúc `automation/DolibarrTests/`
`Pages/` (Page Object) · `Tests/` (test class) · `Helpers/` (DriverFactory, WaitHelper, ExcelReader, ResultWriter, DbHelper) · `TestData/` (bản sao Excel dữ liệu).
Stack: .NET 8, Selenium.WebDriver 4.x, MSTest, EPPlus (NonCommercialPersonal), không thêm gói thừa (không EntityFramework, Moq nếu chưa dùng).

## Quy tắc
1. Locator chỉ nằm trong Page Object; test class chỉ gọi hàm nghiệp vụ và Assert.
2. **Không `Thread.Sleep`**, không trộn ImplicitWait với Explicit Wait: dùng `WebDriverWait` + ExpectedConditions qua `WaitHelper`.
3. **Mỗi test có Assert cụ thể** so với Expected trong Excel. Với dữ liệu quan trọng (tồn kho, tổng tiền) đối chiếu thêm qua DB khi có `DbHelper`.
4. Mỗi test case = một dòng Excel = một `[TestMethod]`/`[DynamicData]`; không gộp cả luồng dài thành một kết quả.
5. Không ghi đè file Excel gốc: `ResultWriter` ghi ra bản sao trong `TestResults/`, có Pass/Fail, Actual, đường dẫn ảnh.
6. Chụp ảnh khi Fail và ở bước cuối mỗi test.
7. Dữ liệu tự sinh: hậu tố Guid/thời gian; đọc mã tham chiếu Dolibarr sau khi tạo, không hard-code.
8. URL, tài khoản đặt trong file cấu hình (`appsettings.local.json`, đã git-ignore), không hard-code trong code.
9. Xử lý select2, alert/confirm bằng helper riêng, tránh JS click bừa trừ khi ghi chú lý do.
10. Sau khi sửa: `dotnet build`, rồi `dotnet test` nếu Dolibarr đang chạy; báo output thật.

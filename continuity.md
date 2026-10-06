# MealTrace — Trạng thái dự án

> Cập nhật 06/10/2026. Tài liệu nghiệp vụ: [PROPOSAL.md](PROPOSAL.md). Roadmap: [docs/implementation-plan.md](docs/implementation-plan.md). Phần dưới là trạng thái hiện tại; roadmap cũ không được dùng để suy ra tính năng đã hoàn thành.

## 1. Tổng quan

MealTrace quản lý bữa ăn bán trú trường mầm non. BE: ASP.NET Core 8, EF Core code-first, PostgreSQL. FE: React/TypeScript. Không dùng Docker. `mobile/` mới giữ chỗ; README để trống theo yêu cầu.

**Đã có luồng lớp/trẻ → đăng ký không ăn → lịch bữa ăn → chốt suất → điều chỉnh suất. Chưa hoàn chỉnh MVP thực đơn/dinh dưỡng, minh chứng và báo cáo.**

## 2. Chức năng hiện có

| Nhóm | Trạng thái |
| --- | --- |
| Tài khoản | Đăng nhập email/SĐT, 4 role ADMIN/TEACHER/KITCHEN_STAFF/PARENT; quản lý tài khoản/phạm vi lớp, hồ sơ và Admin đặt lại mật khẩu có lịch sử. Mật khẩu hash/salt qua ASP.NET Identity; không có OTP. |
| Năm học, lớp, trẻ | Thiết lập năm học dùng chung, gợi ý năm tiếp theo; tạo/sửa lớp và trẻ, mã trẻ duy nhất, ghi danh/chuyển lớp/ngừng học/ghi danh lại theo ngày, tìm kiếm và phân trang. |
| Phụ huynh | Admin liên kết trẻ bằng SĐT: dùng tài khoản đã có hoặc tạo Parent mới; một Parent có nhiều con, một trẻ có nhiều người giám hộ. Mật khẩu tạm chỉ hiển thị một lần, chưa tự gửi SMS/email. |
| Không ăn/báo vắng | Parent đăng ký theo khoảng ngày, chọn nhanh tuần/tháng/đến cuối năm học; sửa/hủy giữ lịch sử. |
| Lịch bữa ăn | Admin thiết lập thứ và bữa phục vụ; xem trước/tạo hàng loạt tuần/tháng/năm học; sửa ngày nghỉ/học bù, giữ lịch sử. |
| Ngoại lệ trước chốt | Teacher/Admin ghi có suất/không có suất/khôi phục mặc định, kèm lý do và nguồn; Teacher chỉ lớp được giao. |
| Chốt và điều chỉnh | Admin chốt suất theo lớp; Teacher/Admin gửi điều chỉnh từng trẻ, Admin duyệt/từ chối; thêm phiên bản mới, giữ gốc. Kitchen đọc suất áp dụng và chênh lệch. |
| Nguyên liệu/dinh dưỡng — Hùng | Đã merge BE nguyên liệu và phiên bản, công thức và phiên bản, tính Kcal/protein. Chưa hoàn chỉnh luồng bếp lập/duyệt/công bố thực đơn. |
| FE | Style claymorphism, tiếng Việt và Sonner toast. Các trang quản lý dùng table với tìm kiếm/lọc ở phía trên; tạo/sửa/xem chi tiết/hủy mở modal. Lớp/trẻ/năm học và chi tiết suất chia tab, không hiển thị tất cả form cùng lúc. Cần tiếp tục nghiệm thu với dữ liệu thực. |

## 3. Quy tắc nghiệp vụ đang áp dụng

- Trẻ có ghi danh hiệu lực mặc định **dự kiến ăn**; Parent báo không ăn và Teacher ghi ngoại lệ khi cần. Đây không phải điểm danh có mặt thực tế.
- Lớp tại ngày ăn lấy từ Enrollment, không dùng lớp hiện tại để sửa lịch sử.
- “Cả năm” là đến cuối **năm học**, không phải 365 ngày; báo không ăn không làm trẻ ngừng học.
- Giờ chốt mặc định **07:30 UTC+7**. Sau cut-off, thay đổi không được sửa suất đã khóa; xử lý qua điều chỉnh sau chốt. Báo không ăn sửa/hủy muộn vẫn giữ lịch sử và không làm đổi bản chốt cũ.
- Khôi phục mặc định vẫn áp dụng đăng ký không ăn của Parent.
- Bản chốt, nguồn quyết định và điều chỉnh lưu theo phiên bản; không ghi đè/xóa gốc. Dữ liệu cũ thiếu nguồn phải hiển thị đúng là thiếu nguồn.
- Parent chỉ xem/thao tác con được liên kết; Teacher theo lớp được giao; Admin chốt/duyệt; Kitchen xem kết quả.

## 4. Đang làm và vấn đề còn lại

- BE nền đã tối ưu: thống nhất thời gian UTC+7/giờ chốt, bảo vệ chuyển lớp khi chờ khóa; lọc phạm vi lớp và phân trang ngoại lệ tại DB, chỉ đọc bản suất hiện hành, tính quyết định một lần khi chốt. Giữ hợp đồng API cho FE; chi tiết kiểm chứng cá nhân được giữ local.
- FE đã ghi rõ lịch tuần chưa cấu hình; BE vẫn cần phân biệt trạng thái này với ngày nghỉ, nhất là khi có phiên cũ đã chốt.
- API bếp đã dùng đúng role ADMIN/KITCHEN_STAFF. Dinh dưỡng chưa đủ P-L-G và cần chốt nguồn/công thức tính.
- UI các luồng đã có cần người dùng nghiệm thu; chưa coi build thành công là tính năng hoàn chỉnh.

## 5. Chưa triển khai và hướng tiếp theo

1. Hoàn thiện thực đơn theo nhóm tuổi, duyệt/công bố và dinh dưỡng đầy đủ với nguồn dữ liệu rõ ràng.
2. Chế biến thực tế/đổi món, ảnh và hồ sơ minh chứng; Parent theo dõi bản công bố.
3. Báo cáo, truy vết nguồn, bản đã duyệt và bản tính lại; thống nhất mẫu báo cáo và người duyệt.
4. Import Excel lớp/trẻ và tạo/ghép Parent theo SĐT khi có mẫu file. Không mặc định 100 trẻ = 100 Parent; anh chị em dùng chung tài khoản.
5. SMS/email, phí/sổ cái, offline ảnh, mobile và dashboard nâng cao làm sau.

## 6. Làm việc chung

- Làm trên nhánh cá nhân, bàn giao phần BE hoàn chỉnh theo luồng và review trước merge main; tránh làm gián đoạn các bạn khác.
- Thay DbContext/entities/Program hoặc API dùng chung cần phối hợp; giữ tương thích FE.
- Quy ước FE: trang quản lý mở bằng danh sách table; thanh tìm kiếm/lọc trên bảng, thao tác theo dòng mở modal. Giữ claymorphism cho màu sắc/nút; dùng hủy/ngừng học/khóa theo nghiệp vụ để bảo toàn lịch sử.
- Không push secrets, cấu hình DB/JWT local, checklist thủ công hoặc continuity cá nhân. Chi tiết test tự động/đo hiệu năng của Cường chỉ ghi local.
- Migration workflow hiện có đến `20261001122617_PortionAmendmentWorkflow`; đã áp dụng ở máy phát triển trước đó. Máy khác tự cấu hình DB và áp dụng migration bằng `dotnet ef database update --project be/src/MealTrace.Infrastructure --startup-project be/src/MealTrace.Api`. Đợt thay đổi này không đổi schema.
- Chạy local: BE `dotnet run --project be/src/MealTrace.Api --launch-profile http` (5184); FE `npm run dev` trong `fe/` (5173). Swagger bật ở Development. Cấu hình connection/JWT bằng scripts trong `be/scripts`; giữ ngoài Git.

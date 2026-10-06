# MealTrace — Trạng thái dự án

> Cập nhật 07/10/2026. Tài liệu nghiệp vụ: [PROPOSAL.md](PROPOSAL.md). Roadmap: [docs/implementation-plan.md](docs/implementation-plan.md). Phần dưới là trạng thái hiện tại; roadmap cũ không được dùng để suy ra tính năng đã hoàn thành.

## 1. Tổng quan

MealTrace quản lý bữa ăn bán trú trường mầm non. Không dùng Docker. `mobile/` mới giữ chỗ; đã chốt Flutter, chưa khởi tạo ứng dụng. README để trống theo yêu cầu.

### Tech stack đã chốt

| Thành phần | Công nghệ |
| --- | --- |
| Web | React + TypeScript + Vite |
| Mobile | Flutter (Dart) |
| Backend | C# — ASP.NET Core 8, REST API |
| Database | PostgreSQL + EF Core Code First |
| API/kiểm thử | Swagger, xUnit, Vitest |
| Architecture | Clean Architecture |

**Đã có luồng lớp/trẻ → đăng ký không ăn → lịch bữa ăn → chốt suất → điều chỉnh suất. Chưa hoàn chỉnh MVP thực đơn/dinh dưỡng, minh chứng và báo cáo.**

## 2. Chức năng hiện có

| Nhóm | Trạng thái |
| --- | --- |
| Tài khoản | Đăng nhập email/SĐT, 4 role ADMIN/TEACHER/KITCHEN_STAFF/PARENT; quản lý tài khoản/phạm vi lớp, tìm tên/email/SĐT và lọc role tại DB với phân trang, hồ sơ và Admin đặt lại mật khẩu có lịch sử. Mật khẩu hash/salt qua ASP.NET Identity; không có OTP. |
| Năm học, lớp, trẻ | Thiết lập năm học dùng chung, gợi ý năm tiếp theo; Admin tạo/sửa lớp và trẻ, ghi danh/chuyển lớp/ngừng học theo ngày. Teacher được thêm trẻ và liên kết Parent trong lớp được phân công; không được sửa tài khoản hoặc cấp Parent cho tài khoản nhân viên đã có. |
| Phụ huynh | Admin/Teacher liên kết trẻ bằng SĐT: dùng Parent đã có hoặc tạo mới; một Parent có nhiều con, một trẻ có nhiều người giám hộ. Admin sửa SĐT tại Tài khoản, chặn số trùng, giữ liên kết trẻ và thu hồi phiên cũ. Mật khẩu tạm chỉ hiển thị một lần. Bảng trẻ hiện một tên phụ huynh, thêm dấu … khi có nhiều người; bấm tên để xem đầy đủ trong popup. |
| Thông báo WhatsApp | Dùng Vonage WhatsApp sandbox ưu tiên, Twilio dự phòng qua `INotificationSender`. Admin/Teacher tùy chọn gửi sau liên kết Parent. Vonage gửi tùy chỉnh tên trẻ + SĐT + mật khẩu tạm nếu tạo mới; Parent đã có dùng mật khẩu cũ. Người dùng đã xác nhận nhận tin từ luồng liên kết Parent qua cả Vonage và Twilio. Không gửi lại mật khẩu của tài khoản đã có; nếu quên, Admin đặt lại thủ công. Twilio trial chỉ gửi mẫu demo khi dự phòng. Chỉ chuyển sang Twilio khi Vonage từ chối/chưa cấu hình; Accepted/timeout/kết quả chưa rõ không chuyển kênh để tránh trùng. Giới hạn tester và một lần/60 giây/instance. Gửi lỗi vẫn giữ hồ sơ. Secret ngoài Git; chưa có outbox/webhook/email/gửi hàng loạt. |
| Không ăn/báo vắng | Parent đăng ký theo khoảng ngày, chọn nhanh tuần/tháng/đến cuối năm học; sửa/hủy giữ lịch sử; tìm/lọc trẻ và trạng thái tại DB, phân trang cả lịch sử cũ. |
| Lịch bữa ăn | Admin thiết lập thứ và bữa phục vụ; xem trước/tạo hàng loạt tuần/tháng/năm học; sửa ngày nghỉ/học bù, giữ lịch sử. |
| Ngoại lệ trước chốt | Teacher/Admin ghi có suất/không có suất/khôi phục mặc định, kèm lý do và nguồn; Teacher chỉ lớp được giao. |
| Chốt và điều chỉnh | Admin chốt suất theo lớp; Teacher/Admin gửi một phiếu cho nhiều trẻ hoặc số lượng bếp không gắn trẻ; Admin duyệt/từ chối toàn phiếu; thêm phiên bản mới, giữ gốc. FE chọn nhanh trẻ phù hợp trong trang, giữ lựa chọn qua trang/tìm kiếm, bỏ từng trẻ và xem tổng suất trước/sau. Kitchen đọc suất áp dụng và chênh lệch. |
| Nguyên liệu/dinh dưỡng — Hùng | Đã merge BE nguyên liệu và phiên bản, công thức và phiên bản, tính Kcal/protein. Chưa hoàn chỉnh luồng bếp lập/duyệt/công bố thực đơn. |
| FE | Style claymorphism, tiếng Việt và Sonner toast. Các trang quản lý dùng table với tìm kiếm/lọc ở phía trên; tạo/sửa/xem chi tiết/hủy mở modal. Lớp/trẻ/năm học và chi tiết suất chia tab, không hiển thị tất cả form cùng lúc. Cần tiếp tục nghiệm thu với dữ liệu thực. |

## 3. Quy tắc nghiệp vụ đang áp dụng

- Trẻ có ghi danh hiệu lực mặc định **dự kiến ăn**; Parent báo không ăn và Teacher ghi ngoại lệ khi cần. Đây không phải điểm danh có mặt thực tế.
- Lớp tại ngày ăn lấy từ Enrollment, không dùng lớp hiện tại để sửa lịch sử.
- “Cả năm” là đến cuối **năm học**, không phải 365 ngày; báo không ăn không làm trẻ ngừng học.
- Giờ chốt mặc định **07:30 UTC+7**. Sau cut-off, thay đổi không được sửa suất đã khóa; xử lý qua điều chỉnh sau chốt. Báo không ăn sửa/hủy muộn vẫn giữ lịch sử và không làm đổi bản chốt cũ.
- Khôi phục mặc định vẫn áp dụng đăng ký không ăn của Parent.
- Bản chốt, nguồn quyết định và điều chỉnh lưu theo phiên bản; không ghi đè/xóa gốc. Dữ liệu cũ thiếu nguồn phải hiển thị đúng là thiếu nguồn.
- Phiếu chọn trẻ thay đổi trạng thái suất của các trẻ được chọn; phiếu số lượng chỉ thay đổi tổng gửi bếp, giữ danh sách/trạng thái ăn và không tự đổi tiền ăn. Không giảm vượt tổng hiện hành; duyệt tạo đúng một bản mới.
- Tab nguồn của phiên đã chốt hiển thị riêng suất tại giờ chốt và suất hiện hành theo trẻ, kèm tổng đang gửi bếp; không suy ra trẻ không ăn từ bản cũ thiếu danh sách. Điều chỉnh số lượng không gắn trẻ được giải thích riêng.
- Parent chỉ xem/thao tác con được liên kết; Teacher theo lớp được giao; Admin chốt/duyệt; Kitchen xem kết quả.

## 4. Đang làm và vấn đề còn lại

- BE nền đã tối ưu: thống nhất thời gian UTC+7/giờ chốt, bảo vệ chuyển lớp khi chờ khóa; lọc phạm vi lớp và phân trang ngoại lệ tại DB, chỉ đọc bản suất hiện hành, tính quyết định một lần khi chốt. Giữ hợp đồng API cho FE; chi tiết kiểm chứng cá nhân được giữ local.
- FE đã ghi rõ lịch tuần chưa cấu hình; BE vẫn cần phân biệt trạng thái này với ngày nghỉ, nhất là khi có phiên cũ đã chốt.
- API bếp đã dùng đúng role ADMIN/KITCHEN_STAFF. Dinh dưỡng chưa đủ P-L-G và cần chốt nguồn/công thức tính.
- FE báo vắng và tổng quan ngày ăn đã dùng API phân trang/lọc tại DB; tài khoản tìm/lọc toàn DB, không giới hạn tìm trong trang hiện tại. Hai API danh sách mảng cũ vẫn giữ tương thích, FE dùng endpoint search mới.
- Hồ sơ ngày ăn tách tab suất hiện hành (bản mới nhất theo lớp) và lịch sử bản chốt, có phiên bản/trạng thái/lý do; không cộng các bản cũ vào suất hiện hành.
- SMS thử với cả Infobip và Vonage: nhà cung cấp báo Delivered nhưng người dùng xác nhận không nhận trên điện thoại; chưa nghiệm thu giao tin thực tế. Không suy ra đã nhận hoặc đã xác minh SĐT từ delivery report. Twilio SMS thất bại mã 21612; WhatsApp đã được người dùng xác nhận nhận tin từ luồng liên kết Parent. Infobip và các adapter SMS đã bỏ; hiện ưu tiên Vonage WhatsApp sandbox, Twilio WhatsApp dự phòng. Trial yêu cầu phụ huynh dùng WhatsApp gửi `join twilio-trial` tới sender cấu hình trước; mẫu cảnh báo số dư là demo, không phản ánh số dư thật. Vonage sandbox đã gửi tùy chỉnh và được xác nhận nhận; số nhận phải tham gia sandbox và có cửa sổ hội thoại 24 giờ. Người dùng đã xác nhận nhận tin đăng ký trẻ qua Vonage; tài khoản đã có nhận hướng dẫn dùng mật khẩu hiện tại. Chưa có template sản xuất cho tin ngoài 24 giờ; email và OTP chưa triển khai.
- UI các luồng đã có cần người dùng nghiệm thu; chưa coi build thành công là tính năng hoàn chỉnh.

## 5. Chưa triển khai và hướng tiếp theo

1. Hoàn thiện thực đơn theo nhóm tuổi, duyệt/công bố và dinh dưỡng đầy đủ với nguồn dữ liệu rõ ràng.
2. Chế biến thực tế/đổi món, ảnh và hồ sơ minh chứng; Parent theo dõi bản công bố.
3. Báo cáo, truy vết nguồn, bản đã duyệt và bản tính lại; thống nhất mẫu báo cáo và người duyệt.
4. Import Excel lớp/trẻ và tạo/ghép Parent theo SĐT khi có mẫu file. Không mặc định 100 trẻ = 100 Parent; anh chị em dùng chung tài khoản.
5. Đăng ký mẫu WhatsApp sản xuất cho tin ngoài cửa sổ hội thoại; xử lý SMS với Twilio. Email, phí/sổ cái, offline ảnh, mobile và dashboard nâng cao làm sau.

## 6. Làm việc chung

- Làm trên nhánh cá nhân, bàn giao phần BE hoàn chỉnh theo luồng và review trước merge main; tránh làm gián đoạn các bạn khác.
- Quy ước Application: class xử lý nghiệp vụ dùng hậu tố `Service` (AuthService, WorkflowService, MealCalendarService…). Endpoint gọi Service; Service gọi repository qua interface; giữ phân tầng Clean Architecture.
- Thay DbContext/entities/Program hoặc API dùng chung cần phối hợp; giữ tương thích FE.
- Quy ước FE: trang quản lý mở bằng danh sách table; thanh tìm kiếm/lọc trên bảng, thao tác theo dòng mở modal. Giữ claymorphism cho màu sắc/nút; dùng hủy/ngừng học/khóa theo nghiệp vụ để bảo toàn lịch sử. Giờ thao tác hiển thị UTC+7; nút phụ thuộc giờ chốt tự cập nhật. Tìm kiếm chờ 300 ms sau khi ngừng gõ, phân biệt đang tải/chưa có dữ liệu/không khớp bộ lọc.
- Không push secrets, cấu hình DB/JWT local, checklist thủ công hoặc continuity cá nhân. Chi tiết test tự động/đo hiệu năng của Cường chỉ ghi local.
- Migration mới `20261006054822_BatchPortionAmendments` bổ sung phiếu nhiều trẻ và số lượng bếp. Máy khác áp dụng bằng `dotnet ef database update --project be/src/MealTrace.Infrastructure --startup-project be/src/MealTrace.Api`.
- Chạy local: BE `dotnet run --project be/src/MealTrace.Api --launch-profile http` (5184); FE `npm run dev` trong `fe/` (5173). Swagger bật ở Development. Cấu hình connection/JWT bằng scripts trong `be/scripts`; WhatsApp dùng `be/scripts/set-twilio-local.ps1` và `be/scripts/set-vonage-whatsapp-local.ps1` (Notifications:Messaging, Notifications:Twilio, Notifications:Vonage), mặc định tắt nếu thiếu cấu hình; giữ ngoài Git.

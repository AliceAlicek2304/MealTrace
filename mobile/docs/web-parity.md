# Phạm vi Android theo web

Chỉ triển khai chức năng đã có trên web, cho đủ vai trò. Dùng chung BE và quy tắc nghiệp vụ; đây là giao diện Flutter native, không phải WebView.

## Trải nghiệm Android

- Điều hướng dưới: Trang chủ, Suất ăn (Phụ huynh: Không ăn), Chức năng và Hồ sơ. Chức năng mở bottom sheet theo quyền; màn hình chi tiết dùng nút Back. Back từ tab phụ về Trang chủ trước khi thoát.
- Trang chủ ưu tiên truy cập nhanh, không tải mọi API để dựng dashboard. Tab công việc chỉ tải khi mở lần đầu và giữ state khi chuyển tab; có kéo xuống để tải lại.
- Danh sách tìm kiếm trực tiếp sau khi ngừng gõ, bộ lọc nâng cao thu gọn; thao tác tạo/yêu cầu dùng nút nổi khi chỉ có một hành động. Thẻ hiện nhãn thông tin chính, mã định danh và dữ liệu phụ mở thêm.
- Form một cột, bàn phím theo loại dữ liệu, nút lưu cố định trên bàn phím. Khi trở lại từ nền và xác minh phiên thành công với quyền không đổi, giữ màn hình/form đang nhập; quyền thay đổi hoặc xác minh thất bại đóng các màn hình được bảo vệ.
- Material 3 với màu xanh chủ đạo, nền trung tính; kiểm tra cả cỡ chữ hệ thống lớn. Đây là thay đổi UI/điều hướng, không thay đổi API hoặc quy tắc chốt/duyệt.

## Chức năng

| Nhóm | Chức năng |
| --- | --- |
| Phiên và tài khoản | Tự đăng ký phụ huynh bằng SĐT và OTP WhatsApp, chờ nhà trường liên kết trẻ; đăng nhập/khôi phục/đăng xuất, thông tin hiện tại, đổi mật khẩu |
| Admin – tài khoản | Tìm/lọc/phân trang, tạo/sửa, vai trò/phạm vi, reset mật khẩu |
| Import XLSX | Admin chọn lớp/ngày bắt đầu, tải file, xem trước/báo dòng lỗi và xác nhận nhập họ tên, ngày sinh, giới tính, mã trẻ tự tạo và ghi danh. Không đọc tên lớp hoặc tạo Parent/gửi WhatsApp. Một sheet, 500 trẻ, 5 MB. |
| Lớp và trẻ | Tìm/lọc, tạo/sửa, ghi danh/chuyển lớp/ngừng học, lịch sử, liên kết phụ huynh và kết quả gửi tin |
| Năm học | Danh sách, cấu hình, xem trước năm kế tiếp |
| Phụ huynh | Yêu cầu liên kết bằng năm học, lớp và họ tên trẻ; lịch sử/hủy yêu cầu chờ; Admin/Teacher lọc năm học/lớp, duyệt hoặc từ chối danh sách đã chọn, thu hồi liên kết sai có lịch sử. Con được liên kết, báo không ăn theo khoảng ngày, chọn nhanh tuần/tháng/năm học, sửa/hủy và lịch sử |
| Sổ suất | Danh sách phiên, tạo phiên, suất theo lớp, nguồn quyết định và lịch sử ngoại lệ trước chốt |
| Chốt và điều chỉnh | Admin chốt; yêu cầu theo nhiều trẻ hoặc số lượng riêng bếp; đối chiếu, duyệt/từ chối và lịch sử phiên bản |
| Lịch bữa ăn | Chọn năm, lịch tuần/ngày, xem trước và tạo hàng loạt, lịch sử |
| Hồ sơ ngày ăn | Danh sách/chi tiết, món ăn, bản chốt và minh chứng có trong API |

Admin, Giáo viên, Bếp và Phụ huynh chỉ thấy menu/thao tác phù hợp; phạm vi dữ liệu do BE kiểm tra. Không thêm giao diện nguyên liệu, công thức hoặc dinh dưỡng chưa có trên web. Import Excel đang chờ thống nhất mẫu và không nằm trong đợt này.

Kiểm chứng tự động gồm contract request/route đối chiếu nguồn BE, HTTP và lỗi, quyền menu, form, chống gửi lặp, preview/tạo, phân trang chọn nhiều và hết hạn phiên. Widget được kiểm tra ở độ rộng 320/390/768 px. Chưa nghiệm thu trên Android thật hoặc xác nhận giao tin WhatsApp trong đợt này; việc build APK không chứng minh các luồng đó đã chạy với dữ liệu thật.

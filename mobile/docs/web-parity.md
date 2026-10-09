# Phạm vi Android theo web

Chỉ triển khai chức năng đã có trên web, cho đủ vai trò. Dùng chung BE và quy tắc nghiệp vụ; đây là giao diện Flutter native, không phải WebView.

| Nhóm | Chức năng |
| --- | --- |
| Phiên và tài khoản | Đăng nhập/khôi phục/đăng xuất, thông tin hiện tại, đổi mật khẩu |
| Admin – tài khoản | Tìm/lọc/phân trang, tạo/sửa, vai trò/phạm vi, reset mật khẩu |
| Lớp và trẻ | Tìm/lọc, tạo/sửa, ghi danh/chuyển lớp/ngừng học, lịch sử, liên kết phụ huynh và kết quả gửi tin |
| Năm học | Danh sách, cấu hình, xem trước năm kế tiếp |
| Phụ huynh | Con được liên kết, báo không ăn theo khoảng ngày, chọn nhanh tuần/tháng/năm học, sửa/hủy và lịch sử |
| Sổ suất | Danh sách phiên, tạo phiên, suất theo lớp, nguồn quyết định và lịch sử ngoại lệ trước chốt |
| Chốt và điều chỉnh | Admin chốt; yêu cầu theo nhiều trẻ hoặc số lượng riêng bếp; đối chiếu, duyệt/từ chối và lịch sử phiên bản |
| Lịch bữa ăn | Chọn năm, lịch tuần/ngày, xem trước và tạo hàng loạt, lịch sử |
| Hồ sơ ngày ăn | Danh sách/chi tiết, món ăn, bản chốt và minh chứng có trong API |

Admin, Giáo viên, Bếp và Phụ huynh chỉ thấy menu/thao tác phù hợp; phạm vi dữ liệu do BE kiểm tra. Không thêm giao diện nguyên liệu, công thức hoặc dinh dưỡng chưa có trên web. Import Excel đang chờ thống nhất mẫu và không nằm trong đợt này.

Kiểm chứng tự động gồm contract request/route đối chiếu nguồn BE, HTTP và lỗi, quyền menu, form, chống gửi lặp, preview/tạo, phân trang chọn nhiều và hết hạn phiên. Widget được kiểm tra ở độ rộng 320/390/768 px. Chưa nghiệm thu trên Android thật hoặc xác nhận giao tin WhatsApp trong đợt này; việc build APK không chứng minh các luồng đó đã chạy với dữ liệu thật.

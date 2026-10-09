# Kiến trúc MealTrace Android

## Phạm vi

Flutter chỉ cho Android. Có đăng nhập, khôi phục phiên và các luồng hiện có trên web cho đủ vai trò; xem web-parity.md. API và phân quyền dùng chung BE. Chưa có refresh token. Đây là bản phát triển, chưa phải bản phát hành production.

## Cấu trúc và chiều phụ thuộc

```text
lib/
  main.dart
  app/                         # App, theme, composition root
    dependencies.dart
  core/config/                 # API_BASE_URL và kiểm tra môi trường
  features/auth/
    presentation/              # Widget và AuthController (view model)
    domain/                    # Model bất biến, lỗi, AuthRepository contract
    data/                      # AuthApi, AuthRepositoryImpl, secure storage
  features/school/
    presentation/              # Trang, form, chọn tham chiếu, controller
    domain/                    # Model, repository contract, quy tắc command
    data/                      # Route cố định, HTTP/JSON và ánh xạ response
```

- Widget phụ thuộc controller; controller chỉ phụ thuộc domain contract, không gọi HTTP, đọc JSON hoặc secure storage.
- Data triển khai contract; AuthApi chỉ xử lý request/response và ánh xạ model. Repository phối hợp API và session store.
- Domain dùng Dart thuần, không phụ thuộc Flutter, HTTP hoặc plugin lưu trữ.
- app/dependencies.dart là điểm lắp ráp; truyền phụ thuộc qua constructor, không dùng singleton/service locator toàn cục.
- AuthController đóng repository/client khi dispose. Controller được truyền vào app để test do bên gọi sở hữu; app chỉ dispose controller tự tạo.
- State công khai chỉ có getter; signedIn luôn có user. Model roles không thể bị sửa từ UI.

Presentation của nghiệp vụ dùng SchoolRepository contract, không đọc token hoặc gọi HTTP. SchoolController quản lý tải/lỗi và bỏ qua response đến muộn; command validation thuần Dart. SchoolOperation giới hạn thao tác, data ánh xạ sang route cố định và chỉ cho phép các query parameter đã khai báo. Schema form trong presentation dùng chung thành phần nhập liệu native; không cung cấp trình gọi API tùy ý cho người dùng.

app/dependencies.dart lắp ráp client và cấp token hiện hành cho data qua callback. App sở hữu repository nghiệp vụ; controller từng trang chỉ hủy state của mình. Không tự gửi lại request ghi khi timeout. Chọn trẻ/lớp giữ lựa chọn qua trang; payload chỉ gồm trường DTO và revision/bản chốt cần thiết. Preview lịch giữ nguyên token và phạm vi đã xem trước khi tạo.

## Luồng phiên và lỗi

- Login gửi identifier/password, chỉ lưu token/expiry sau khi response hợp lệ. Không lưu mật khẩu hoặc log dữ liệu xác thực.
- Restore đọc phiên, kiểm tra hạn và /auth/me. Token hết hạn/401 bị xóa; mất mạng giữ dữ liệu đã lưu nhưng không vào màn hình tài khoản, có thử lại.
- Expiry chuyển khỏi signedIn trước khi chờ storage. Nếu có thao tác đang chạy, xử lý expiry sau khi thao tác kết thúc; không tạo vòng polling.
- Dispose bỏ qua response đến muộn, hủy timer, đóng HTTP client. Chặn submit khi busy; UI có validation và kiểm tra ở controller.
- Logout xóa local rồi gọi BE. API BE hiện thu hồi toàn bộ phiên của tài khoản qua security stamp; không phải chỉ một thiết bị. Khi BE không phản hồi, báo đã xóa local nhưng chưa thu hồi remote.
- Timeout và lỗi API trả thông báo hữu ích, không hiện stack trace hoặc nội dung server tùy ý.

## Cấu hình và bảo mật

- Token lưu bằng flutter_secure_storage; Android tắt backup. Không đóng gói key nhà cung cấp, mật khẩu DB hoặc JWT signing key.
- Debug cho phép API HTTP local; profile/release bắt buộc HTTPS. Chặn base URL có credentials/query/fragment; cấu hình sai không mở form đăng nhập.
- Chưa cấu hình keystore phát hành. Đã bỏ signing bằng debug key khỏi release; cần cấu hình private keystore trước phân phối.
- pubspec.lock được giữ; build/cache/SDK local và artifact không vào Git. Đã bỏ dependency cupertino_icons không sử dụng. Các package đa nền tảng có dependency transitive Darwin không đồng nghĩa có dự án iOS.

## Kiểm chứng và giới hạn

Analyzer bật strict casts/inference/raw types. Test dùng HTTP và storage giả tại boundary, fixture nằm riêng; không import test suite vào test suite khác. Kiểm thử bao gồm login/logout/restore, lỗi dữ liệu, mất mạng, phiên hết hạn, cleanup chậm, dispose và submit lặp.

`flutter test` cũng quét import/export/part của toàn bộ `lib`: chặn domain phụ thuộc Flutter/package bên ngoài hoặc data/presentation; chặn data phụ thuộc presentation; chặn presentation gọi trực tiếp data, HTTP, JSON hoặc secure storage; chặn core phụ thuộc app/features và feature phụ thuộc composition root. Kiểm tra này áp dụng cho feature mới, bao gồm đường dẫn tương đối và package nội bộ. Đây là kiểm tra chiều phụ thuộc nguồn, không thay thế review nghiệp vụ hoặc kiểm thử thiết bị.

Cần nghiệm thu secure storage, lifecycle và bàn phím trên Android thật; test widget không thay thế kiểm thử thiết bị. Chưa thực hiện pentest hoặc kiểm tra CVE toàn bộ dependency. API nghiệp vụ xử lý 401 qua AuthController; khi phiên hết hạn app xóa navigator của phiên để đóng cả màn hình con được bảo vệ. Response đến muộn từ token cũ không được làm hết hạn phiên mới. UI giới hạn thao tác theo role nhưng BE vẫn là ranh giới phân quyền. Phát hành cần keystore và API HTTPS thực tế.

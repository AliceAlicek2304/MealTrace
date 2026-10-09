# MealTrace Mobile

Ứng dụng Flutter chỉ dành cho Android, dùng chung REST API trong `be/`. Có đăng nhập, tài khoản và các luồng đã có trên web cho Admin, Giáo viên, Bếp và Phụ huynh. Giao diện riêng cho mobile: điều hướng dưới, truy cập nhanh theo vai trò, danh sách thẻ, tìm kiếm trực tiếp, bộ lọc nâng cao và form tiếng Việt có nút lưu cố định. Xem [phạm vi](docs/web-parity.md).

## Cấu trúc

- `lib/main.dart`: điểm khởi động.
- `lib/app/`: ứng dụng và theme dùng chung.
- `lib/core/config/`: cấu hình công khai.
- `lib/features/auth/presentation/`: màn hình và controller.
- `lib/features/auth/domain/`: model bất biến và repository contract.
- `lib/features/auth/data/`: API, repository implementation, secure storage.
- `lib/features/school/`: nghiệp vụ trường học, cũng chia domain/data/presentation.
- `lib/app/dependencies.dart`: lắp ráp và truyền phụ thuộc; xem [kiến trúc](docs/architecture.md).
- `test/`: kiểm thử Flutter.
- `android/`: dự án Android.

## Chạy và kiểm tra

Trong thư mục `mobile/`:

```sh
flutter pub get
flutter analyze
flutter test
flutter devices
flutter run -d <device-id> --dart-define=API_BASE_URL=http://10.0.2.2:5184/api
```

Địa chỉ mặc định dành cho Android Emulator truy cập BE trên máy chủ. Điện thoại thật dùng địa chỉ LAN của máy chạy BE; BE cần lắng nghe trên giao diện mạng phù hợp. Bản debug cho phép HTTP để test local; bản production cần API HTTPS. Không có dự án iOS.

Không đưa khóa Vonage/Twilio, JWT signing key hoặc mật khẩu DB vào ứng dụng. Mobile chỉ gọi backend; gửi WhatsApp do backend xử lý.

Đã khởi tạo bằng Flutter 3.44.0 / Dart 3.12.0. Giữ `pubspec.lock` để đồng nhất dependency. Không commit `.dart_tool/`, `build/`, cấu hình SDK local hoặc khóa ký ứng dụng.
## Phiên đăng nhập

Token và thời hạn được lưu bằng flutter_secure_storage; không lưu mật khẩu. Khi mở app hoặc trở lại từ nền, kiểm tra /auth/me trước khi hiện tài khoản. Phiên hết hạn/401 yêu cầu đăng nhập lại; mất mạng khi khôi phục cho phép thử lại hoặc đăng xuất local. Chưa có refresh-token API.

Đăng xuất xóa phiên local và gọi BE. BE đổi security stamp nên các phiên hiện có của cùng tài khoản cũng hết hiệu lực. Khi mất mạng, app thông báo chỉ đăng xuất trên thiết bị; phiên phía máy chủ chưa được thu hồi.

Với điện thoại thật, ví dụ: flutter run --dart-define=API_BASE_URL=http://<IP-LAN>:5184/api. Chạy BE để lắng nghe LAN và mở cổng firewall phù hợp; không đổi cấu hình production. Không đóng gói secret hoặc tài khoản thử vào app.
Bản profile/release bắt buộc API HTTPS qua dart-define. Chưa cấu hình keystore phát hành; release không dùng debug signing key. Bản debug hiện dành cho phát triển/test.

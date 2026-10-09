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

## Đăng ký phụ huynh

Từ trang đăng nhập, chọn Đăng ký tài khoản phụ huynh. Nhập họ tên, SĐT Việt Nam, mật khẩu và xác nhận. POST /auth/register chỉ cấp PARENT trên BE; không lưu mật khẩu hoặc tạo phiên khi đăng ký. Thành công quay về đăng nhập với SĐT đã chuẩn hóa. Nhận OTP qua WhatsApp và nhập 6 chữ số trước khi tạo tài khoản. Mã dùng một lần trong 5 phút; gửi lại sau 60 giây, tối đa 5 lượt mỗi giờ mỗi SĐT. BE đánh dấu SĐT đã xác minh sau OTP đúng, không tự liên kết trẻ. Nhà trường liên kết tài khoản bằng SĐT qua Lớp và trẻ; tạo trẻ riêng vẫn không cần phụ huynh.

OTP hiện dùng sandbox: chỉ số tester trong Notifications:Messaging:TestNumber, đã tham gia sandbox và còn cửa sổ hội thoại 24 giờ. Vonage ưu tiên, Twilio dự phòng gửi nội dung OTP thật trong phiên WhatsApp; không dùng ContentSid demo. Khi dùng thực tế cần cấu hình sender và template authentication được duyệt. Migration ParentSignupWhatsAppOtp tạo bảng challenge; OTP không trả qua API hoặc lưu trên thiết bị.

## Phụ huynh yêu cầu liên kết trẻ

Mở Chức năng → Liên kết trẻ. Chọn năm học và tên lớp, nhập họ tên trẻ, chọn quan hệ và ghi chú. Không cần mã lớp/mã trẻ; gửi yêu cầu chưa cấp quyền xem trẻ. Có thể xem lịch sử hoặc hủy yêu cầu đang chờ. Chỉ có danh mục lớp, không công khai danh sách trẻ. Nếu lớp có trẻ trùng họ tên, liên hệ giáo viên để đối chiếu và liên kết đúng trẻ.

Admin/Giáo viên mở Chức năng → Yêu cầu liên kết trẻ, lọc năm học và tùy chọn lớp. Rà phụ huynh/SĐT/tên trẻ, chọn yêu cầu cần xử lý rồi ghi kết quả đối chiếu để duyệt hoặc từ chối cả danh sách (tối đa 100 mục trên trang). Giáo viên chỉ xử lý lớp được phân công hiện hành, không tự duyệt. Backend kiểm tra phiên bản, phạm vi năm học/lớp và trạng thái từng yêu cầu; một mục không hợp lệ thì toàn bộ lượt xử lý bị hủy. Sau duyệt, Parent tải lại Trẻ đã liên kết hoặc Báo vắng.

Lịch sử / sửa liên kết cho phép thu hồi liên kết sai, bắt buộc ghi lý do. Giữ nguyên lịch sử duyệt, ghi người và thời điểm thu hồi riêng; gỡ liên kết và thu hồi phiên đăng nhập cũ của Parent. Migration ParentLinkRevocation bổ sung lịch sử thu hồi.

## Nhập trẻ từ Excel (Admin)

Chức năng → Nhập trẻ từ Excel. Chọn lớp có sẵn (hiện kèm năm học), ngày bắt đầu từ hôm nay và file XLSX. Ứng dụng mở bộ chọn file Android, tải multipart qua data repository rồi hiển thị thẻ xem trước chỉ gồm tên trẻ, SĐT và trạng thái Hợp lệ/Có lỗi; bấm thẻ để mở popup ngày sinh, giới tính, dòng file và lỗi cụ thể. Xác nhận chỉ bật khi danh sách không lỗi; có bước xác nhận trước khi ghi DB.

Nhập họ tên, ngày sinh và giới tính có trong file; tự tạo mã trẻ và ghi danh. Không đọc tên lớp trong file, không tạo tài khoản/liên kết Parent hoặc gửi WhatsApp. Ngày sinh/giới tính được xem trước và lưu DB; ô trống giữ null, ngày sinh tương lai hoặc dữ liệu sai chặn cả lượt nhập. SĐT và ghi chú được báo rõ là bỏ qua. Migration StudentBirthAndGender thêm trường nullable, giữ hồ sơ cũ. Một sheet, tối đa 500 trẻ, 5 MB. Tên trùng trong file/lớp yêu cầu kiểm tra hoặc thêm riêng; file có lỗi không được nhập một phần. Backend kiểm tra lại và lưu cùng giao dịch, chặn quyền Teacher/Parent/Kitchen.

PDF và tự tạo/ghép Parent từ SĐT trong file chưa triển khai. Chức năng liên kết thủ công bằng SĐT và tạo trẻ không có Parent tiếp tục dùng được.

Nếu build Android trên Windows gặp lỗi Kotlin cache do Pub cache và dự án ở hai ổ khác nhau, dùng:

```powershell
flutter build apk --debug --target-platform android-arm64 --android-project-arg=kotlin.incremental=false --android-project-arg=kotlin.compiler.execution.strategy=in-process
```


## E2E Android với API thật

`integration_test/student_profile_e2e_test.dart` chạy đăng nhập Admin → tạo trẻ với ngày sinh/giới tính → sửa hồ sơ/xóa ngày sinh → đọc lại từ API để kiểm tra dữ liệu đã lưu. Dùng HTTP thật và secure storage thật, không gửi WhatsApp.

Cần Android emulator hoặc điện thoại có USB debugging, BE đang chạy và một DB kiểm thử riêng có Admin cùng lớp đã tạo. Test tạo trẻ mang tên duy nhất và giữ dữ liệu để đối chiếu; không chạy trên DB nhà trường thực tế.

Tạo `mobile/e2e.local.json` (đã bỏ qua Git) với các khóa `API_BASE_URL`, `E2E_ADMIN_IDENTIFIER`, `E2E_ADMIN_PASSWORD`, `E2E_CLASS_NAME`, `E2E_ISOLATED_DATABASE`. Khóa cuối đặt chuỗi `true`; tên lớp phải khớp chính xác lớp dùng thử. Emulator dùng `http://10.0.2.2:5184/api`; điện thoại dùng địa chỉ LAN của máy chạy BE. Không đặt thông tin đăng nhập vào code hoặc commit file cấu hình này.

Trong `mobile/`:

```powershell
flutter devices
flutter test integration_test/student_profile_e2e_test.dart -d <device-id> --dart-define-from-file=e2e.local.json
```

Nếu gặp lỗi Kotlin incremental do pub cache và dự án khác ổ đĩa, cấu hình hai thuộc tính `kotlin.incremental=false` và `kotlin.compiler.execution.strategy=in-process` trong Gradle user properties trên máy kiểm thử. Đây là cấu hình build local; không sửa các kiểm tra bảo mật hoặc cấu hình phát hành.

Test E2E này là luồng Admin đầu tiên; không thay thế nghiệm thu OTP/WhatsApp trên điện thoại hoặc kiểm tra toàn bộ vai trò. `flutter test` thông thường chỉ chạy bộ unit/widget trong `test/`.

## Smoke test đăng nhập với BE đang chạy

`tool/mobile_auth_live.dart` nằm trong package Flutter để Dart analyzer phân giải được dependency `http`. Từ thư mục `mobile/`, đặt `AUTH_LIVE_IDENTIFIER` và `AUTH_LIVE_PASSWORD` trong process environment; có thể đặt `API_BASE_URL` để đổi API (mặc định `http://localhost:5184/api`), rồi chạy `dart run tool/mobile_auth_live.dart`. Script kiểm tra mật khẩu sai, đăng nhập, lấy user hiện tại, đăng xuất và xác nhận token đã hết hiệu lực. BE thu hồi mọi JWT của tài khoản khi đăng xuất, vì vậy chỉ dùng tài khoản thử dành riêng. Không ghi thông tin đăng nhập hoặc token ra log.

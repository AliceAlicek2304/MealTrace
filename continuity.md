# MealTrace — Continuity

> Cập nhật 30/09/2026. Proposal đang áp dụng: [PROPOSAL.md](PROPOSAL.md). Mã nguồn đã push gần nhất: `8865d55` (main); mã trẻ, Enrollment, phân trang, tests, proposal và continuity đã được push. Kế hoạch đang áp dụng: [docs/implementation-plan.md](docs/implementation-plan.md). Không dùng Docker. `README.md` để trống theo yêu cầu.

`mobile/` hiện chỉ có `.gitkeep` để giữ chỗ trong Git; chưa có ứng dụng mobile hoặc thay đổi phạm vi MVP. Chạy web local bằng `dotnet run --project be/src/MealTrace.Api/MealTrace.Api.csproj --launch-profile http` ở root và `npm run dev` trong `fe/` (cổng 5184 và 5173).

## Trạng thái hiện tại

**Đã có lát cắt đầu tiên của workflow chính, chưa phải MVP hoàn chỉnh.** Hệ thống đang có đăng nhập/4 vai trò, quản lý tài khoản/reset mật khẩu, mã trẻ, ghi danh/chuyển lớp/ngừng học/ghi danh lại theo ngày, liên kết Parent, tìm kiếm/phân trang, báo vắng và chốt số suất gửi bếp. Chưa hoàn thành lịch ngày nghỉ, điều chỉnh sau chốt, thực đơn/dinh dưỡng, ảnh và báo cáo. Backend dùng ASP.NET Core 8, EF Core code-first, PostgreSQL; frontend React/TypeScript. Swagger hoạt động trong Development.

Mặc định trẻ có Enrollment hiệu lực tại ngày ăn, thuộc niên khóa của phiên, được **dự kiến ăn** vào `MealDay` đã tạo. Lớp theo ngày lấy từ Enrollment, không dùng pointer Student.ClassId để tính lịch sử. Báo vắng được tính nếu ghi trước `CutoffAt`. Giáo viên có thể ghi ngoại lệ trước giờ chốt cho lớp được giao. Bản chốt lưu danh sách ID và tên trẻ theo lớp; sau chốt, báo vắng bị hủy hoặc thêm trẻ mới không thay đổi số suất đã gửi. Đây là số suất dự kiến, **không phải điểm danh có mặt thực tế**.

## Những gì đã chạy và kiểm chứng

- Auth: Identity, mật khẩu hash/salt qua PasswordHasher của ASP.NET Identity, JWT, kiểm tra role/scope tại API; 4 tài khoản demo chỉ trong Development.
- Admin: tạo/sửa lớp và trẻ, mã trẻ, ghi danh/chuyển lớp/ngừng học/ghi danh lại, xem lịch sử và liên kết giáo viên/phụ huynh. Chỉnh sửa dùng modal; nhập lớp/trẻ từ tệp **chưa có**.
- Admin có thể liên kết một trẻ với phụ huynh ngay tại màn **Lớp và trẻ** bằng SĐT. SĐT đã tồn tại được ghép vào tài khoản đang hoạt động; SĐT mới tạo tài khoản Parent với mật khẩu tạm chỉ hiển thị một lần. Một phụ huynh có thể liên kết nhiều trẻ và một trẻ có thể có nhiều người giám hộ. Danh sách tài khoản lọc theo lớp giáo viên phụ trách ở API trước khi phân trang.
- Parent: xem trẻ được liên kết; gửi/hủy báo vắng một ngày hoặc tối đa 90 ngày. Không được báo vắng cho trẻ ngoài liên kết.
- Teacher: xem danh sách dự kiến ăn chỉ cho lớp được phân công; ghi ngoại lệ trước giờ chốt.
- Admin: tạo phiên ăn với niên khóa, giờ chốt mặc định 07:30 UTC+7; chốt số suất theo lớp. Kitchen xem bản chốt.
- EF migrations đến `StudentEnrollmentHistory` đã áp dụng lên PostgreSQL local `mealtrace`. Trẻ legacy chưa biết ngày nhập học được tạo Enrollment từ ngày migration, có ghi lý do chuyển đổi.
- Kiểm chứng gần nhất: 17 test SQLite + 2 test PostgreSQL, FE 5 test, build thành công. PostgreSQL test dùng schema riêng; cần MEALTRACE_TEST_CONNECTION để chạy, mặc định skip khi thiếu biến.
- Smoke local qua proxy FE: đăng nhập Admin SĐT, lấy lớp/trẻ/phạm vi, health và Swagger thành công; 4 trẻ local có mã. PostgreSQL tests đã kiểm tra chuyển lớp, báo vắng và chốt suất đồng thời trong schema biệt lập. Chưa visual QA toàn bộ UI hoặc có kết quả Sonar mới cho commit 8865d55.

## Định hướng giao diện FE

- Trang đầu khi chưa đăng nhập là landing page giới thiệu **MealTrace** tại `fe/src/features/landing/LandingPage.tsx`. Các ý tưởng catalog/progress/testimonial/CTA của nền tảng học tập chỉ dùng làm cảm hứng bố cục: thẻ giới thiệu tính năng, demo bốn bước bữa ăn, góc nhìn theo vai trò và CTA đăng nhập. Không có khóa học hoặc đăng ký học trong phạm vi sản phẩm.
- Bộ style claymorphism dùng màu kem, san hô, vàng, tím, xanh mint; thẻ bo tròn, viền sáng và bóng nổi mềm. Token và quy tắc responsive ở `fe/src/clay.css`, nạp sau `style.css` để áp dụng cho landing, đăng nhập và các màn hình nghiệp vụ.
- Landing page dùng dữ liệu **minh họa**, có nhãn rõ. Không trình bày số liệu giả hay lời chứng thực giả như dữ liệu thật. CTA đi đến màn đăng nhập, không tạo tài khoản công khai. Tài khoản seed chỉ hiển thị trên màn đăng nhập ở Development.
- Khi thêm màn hình mới, tái dùng màu, nút, field và panel của bộ style; giữ nội dung tiếng Việt, phân cấp chữ rõ, tương phản dễ đọc, bố cục mobile và hỗ trợ giảm chuyển động.
- `npm run build` và `npm test` của FE đã qua sau thay đổi style. Chưa kiểm tra hình ảnh bằng trình duyệt: Computer Use dừng vì không xác định được URL hiện tại của Chrome đủ chắc chắn. Cần kiểm tra trực quan desktop/mobile trong lần tiếp theo.
- Thông báo thao tác dùng Sonner toast: thành công màu xanh lá, lỗi màu đỏ; các biểu mẫu vẫn giữ lỗi ngay tại trường khi cần sửa dữ liệu nhập.

## Chưa làm / giới hạn cần nhớ

| Ưu tiên | Việc còn lại | Lý do |
| --- | --- | --- |
| P0 | Nghiệm thu lớp/trẻ và sửa lỗi từ test người dùng | Mã trẻ/Enrollment đã triển khai; cần test modal desktop/mobile và xử lý lỗi tái hiện trước khi mở rộng. |
| P1 | Ngoại lệ có/vắng/khôi phục và nguồn quyết định trước chốt | UI hiện chỉ ghi vắng; thiếu lịch sử actor và khôi phục. Cần test boundary cut-off và concurrent override. |
| P1 | Quy trình sửa số suất sau chốt bằng bản điều chỉnh có lý do | Hiện bản chốt bất biến và chỉ cho chốt một lần; báo vắng muộn không làm đổi bản đã chốt. Chưa có amendment. |
| P1 | Lịch bữa ăn/ngày nghỉ | Hiện chưa có lịch vận hành; giữ 07:30 UTC+7, không thêm cấu hình giờ chốt khi chưa cần. |
| P1 | Import danh sách lớp/trẻ từ tệp, kiểm tra trùng và lỗi theo dòng | Hiện chỉ tạo từng lớp/trẻ trên UI. |
| P1 | Liên kết phụ huynh hàng loạt sau import | Tạm hoãn theo yêu cầu vì chưa có mẫu tệp. Khi triển khai cần mã học sinh duy nhất và SĐT phụ huynh trong tệp. Bước xem trước phải đối chiếu từng dòng, trùng SĐT, trẻ trùng mã và lỗi lớp trước khi ghi DB; sau xác nhận mới tạo/ghép tài khoản theo SĐT và quan hệ ParentStudent trong một giao dịch. Gửi thông tin tài khoản qua SMS tới SĐT và qua email nếu có sẽ triển khai ở giai đoạn sau, sau khi import thành công. Chọn phạm vi đã có phân trang và giữ lựa chọn; import vẫn cần luồng preview/ghi theo lô riêng, không thay bằng chọn checkbox thủ công. |
| P1 | Nguồn bản chốt và bản điều chỉnh | Snapshot mới cần Enrollment/báo vắng/ngoại lệ nguồn. Giữ dự kiến ăn khác với có mặt thực tế; không bắt giáo viên điểm danh có toàn bộ trẻ mỗi ngày. |
| P1 | Thực đơn, công thức dinh dưỡng, công bố, món thực tế/đổi món, ảnh, màn phụ huynh theo dõi | Model thực đơn/recipe cũ mới ở mức sơ bộ, chưa có workflow thao tác. |
| P1 | Báo cáo có phiên bản, dữ liệu nguồn và bản tính lại | `ReportSnapshot` hiện chỉ là scaffold, chưa có phép tính/duyệt/điều chỉnh đáng tin cậy. |
| P2 | Phí, sổ cái, offline ảnh, thực đơn riêng trẻ, dashboard nâng cao | Phần mở rộng theo proposal sau khi MVP ổn định. |

## Quyết định cần xác nhận

1. Giờ chốt số suất và hạn công bố thực đơn; xử lý báo vắng muộn, hoàn phí và ngày nghỉ.
2. Ai được chốt, ai được duyệt điều chỉnh sau chốt, bếp cần thấy danh sách trẻ hay chỉ tổng số theo lớp.
3. Mẫu báo cáo gửi cơ quan chuyên trách, nguồn dữ liệu thành phần thực phẩm, người duyệt.
4. Quy trình xác nhận dị ứng/lưu ý ăn uống, lưu ảnh và phân quyền xem.

## Thứ tự triển khai tiếp theo

1. Nghiệm thu lớp/trẻ; sửa lỗi từ người dùng. Đợt code tiếp: ngoại lệ có/vắng/khôi phục mặc định, scope và lịch sử.
2. Lịch bữa ăn/ngày nghỉ; điều chỉnh sau chốt có duyệt, nguồn và phiên bản, giữ bản gốc.
3. Thực đơn/công thức/nguồn dinh dưỡng và nhóm tuổi; duyệt/công bố, giữ phiên bản.
4. Món thực tế/đổi món/ảnh và Parent xem bản công bố; báo cáo/truy vết.
5. Import khi có mẫu Excel; SMS/email và phần mở rộng sau MVP. Chi tiết: docs/implementation-plan.md.

Sau mỗi chặng, cập nhật tài liệu theo hành vi đã chạy và bằng chứng test. Không đánh dấu hoàn thành chỉ vì build pass.

## Cập nhật 30/09/2026: tài khoản phụ huynh bằng SĐT

- SĐT thuộc **phụ huynh/người giám hộ**, không phải thông tin đăng nhập của trẻ. Không bắt buộc phụ huynh có email; không sinh email giả.
- Đăng nhập nhận `identifier` là SĐT hoặc email; payload `email` cũ vẫn được hỗ trợ. Tài khoản seed email hiện tại và mật khẩu không thay đổi.
- Chuẩn hóa số di động 10 chữ số bắt đầu bằng 0; nhận thêm định dạng +84/84 và dấu cách, dấu chấm, ngoặc, dấu gạch. Lưu dạng 0...; SĐT duy nhất và normalized email duy nhất được bảo vệ bằng index DB. Chỉ kiểm tra định dạng; không triển khai OTP/xác minh SĐT theo quyết định của chủ dự án. Nguồn SĐT là hồ sơ nhập học do phụ huynh cung cấp và nhà trường lưu.
- Admin tạo/sửa tài khoản có SĐT, email hoặc cả hai. Đổi thông tin tài khoản thu hồi JWT cũ. Mật khẩu vẫn hash/salt bằng ASP.NET Core Identity.
- Màn Lớp và trẻ: chọn trẻ → Liên kết phụ huynh → nhập SĐT; nếu chưa có tài khoản, nhập họ tên để tạo mới. Trả SĐT và mật khẩu tạm một lần; nhà trường tự chuyển cho phụ huynh. Không tự gửi SMS.
- SĐT đã tồn tại dùng tài khoản hiện có, không tạo lại hay đặt lại mật khẩu. Một phụ huynh có nhiều con; một trẻ có thể liên kết nhiều phụ huynh. Tài khoản khóa phải được mở khóa trước khi liên kết.
- API liên kết vẫn hỗ trợ email cho client cũ; FE dùng SĐT. Danh sách tài khoản, tìm kiếm và hồ sơ hiển thị SĐT/email.
- Migration `20260930084157_PhoneAccountLogin` đã áp dụng vào PostgreSQL mealtrace. Không cần Docker.
- Nhập file **chưa triển khai** theo yêu cầu mới. Sau khi có mẫu, thiết kế StudentCode + ParentPhone, xem trước lỗi từng dòng rồi tạo/ghép tài khoản và quan hệ trong transaction. 100 trẻ không nhất thiết tương ứng 100 phụ huynh: các trẻ cùng SĐT dùng chung tài khoản.
- BE/FE đã tắt theo yêu cầu và chưa khởi động lại sau sửa code.
- FE build và 5/5 tests qua; BE 7/7 tests qua; kiểm thử tích hợp gồm đăng nhập email/SĐT, số quốc tế, liên kết anh chị em, trùng liên kết, số sai, tạo/sửa tài khoản SĐT và thu hồi JWT. Kiểm thử API dùng SQLite biệt lập; migration chạy trên PostgreSQL thật. Chưa kiểm tra trực quan UI sau thay đổi SĐT.
- Log Vite trước khi tắt có lỗi hook của Toaster sau khi optimize dependency; `npm ls` xác nhận React/ReactDOM deduped. Cần kiểm tra tải mới trình duyệt khi khởi động lại, chưa xác nhận lỗi tái hiện.

## Quyết định 30/09/2026: nguồn dữ liệu nhập học và cấp tài khoản hàng loạt

**Đã chốt nghiệp vụ:** phụ huynh cung cấp SĐT khi đăng ký nhập học; nhà trường ghi thông tin vào file Excel. MealTrace sử dụng dữ liệu này để tạo tài khoản phụ huynh và liên kết với trẻ. Không yêu cầu OTP hay bước xác minh SĐT trong hệ thống.

### Luồng import dự kiến — chưa triển khai

1. Admin nhập file Excel danh sách trẻ do nhà trường quản lý. Chờ file mẫu thực tế để chốt tên cột, cách tách dữ liệu và biểu diễn nhiều người giám hộ; chưa lập trình import ở giai đoạn hiện tại.
2. Dữ liệu cần ánh xạ gồm mã trẻ, họ tên trẻ, lớp/niên khóa, họ tên phụ huynh, SĐT phụ huynh và email nếu có. Giữ SĐT dưới dạng chuỗi để không mất số 0 đầu; chuẩn hóa trước khi đối chiếu.
3. Xem trước kết quả theo dòng: trẻ mới/đã tồn tại, phụ huynh mới/đã có tài khoản, liên kết sẽ tạo và lỗi cần sửa. SĐT thiếu/sai định dạng hoặc dữ liệu xung đột phải báo rõ, không tự đoán người giám hộ.
4. Gom phụ huynh theo SĐT đã chuẩn hóa. Ví dụ 100 trẻ với 100 SĐT khác nhau chưa có tài khoản sẽ tạo 100 tài khoản Parent; nếu có anh chị em cùng SĐT thì tạo một tài khoản cho phụ huynh đó và liên kết các con.
5. SĐT đã có tài khoản được dùng lại; không tạo trùng hoặc đặt lại mật khẩu. Import lại cùng dữ liệu không tạo trùng trẻ hay quan hệ ParentStudent. Email là thông tin liên hệ tùy chọn, không dùng làm khóa ghép chính; xung đột email với tài khoản khác phải được xử lý trước khi ghi.
6. Sau khi Admin xác nhận, tạo/ghép trẻ, tài khoản phụ huynh và liên kết trong một giao dịch. Mỗi tài khoản mới có mật khẩu tạm riêng, lưu hash/salt bằng Identity.

### Gửi thông tin tài khoản — làm sau

- Sau khi import thành công, gửi thông tin đăng nhập cho tài khoản phụ huynh mới qua tin nhắn tới SĐT; gửi thêm qua email nếu hồ sơ có email.
- Chưa tích hợp dịch vụ SMS/email và chưa gửi thông báo ở giai đoạn hiện tại. Kênh gửi, mẫu tin và cơ chế thử lại sẽ thiết kế khi triển khai tính năng này.
- Luồng gửi chỉ chạy sau khi giao dịch import đã commit; cần theo dõi trạng thái gửi để thử lại không tạo lại tài khoản hoặc liên kết.
- Không lưu mật khẩu rõ lâu dài trong DB/log để chờ gửi. Khi triển khai gửi bất đồng bộ, thiết kế cơ chế cấp thông tin truy cập có thời hạn phù hợp; cách gửi cụ thể sẽ chốt ở giai đoạn đó.

**Phạm vi hiện tại:** giữ luồng tạo/liên kết từng phụ huynh bằng SĐT đã có. Import Excel và gửi SMS/email là kế hoạch, chưa đánh dấu hoàn thành. BE/FE tiếp tục tắt theo yêu cầu trước đó.

## Cập nhật 30/09/2026: SĐT demo và xử lý liên kết thủ công

- Đã kiểm tra trực tiếp PostgreSQL: trước cập nhật, cả 6 tài khoản demo chưa có SĐT. Đã chạy seeder và kiểm tra lại DB, các số mẫu đã được lưu:

| Vai trò | Email demo | SĐT demo |
| --- | --- | --- |
| ADMIN | admin@demo.mealtrace.local | 0900000001 |
| TEACHER | teacher@demo.mealtrace.local | 0900000002 |
| KITCHEN_STAFF | kitchen@demo.mealtrace.local | 0900000003 |
| NUTRITIONIST | nutrition@demo.mealtrace.local | 0900000004 |
| ACCOUNTANT | accountant@demo.mealtrace.local | 0900000005 |
| PARENT | parent@demo.mealtrace.local | 0900000006 |

- Số trên chỉ là dữ liệu thử; không gửi SMS đến các số demo. Mật khẩu demo giữ nguyên `123456@@`; có thể đăng nhập bằng email hoặc SĐT.
- DevelopmentSeeder bổ sung số mẫu khi tài khoản chưa có SĐT, không ghi đè SĐT Admin đã sửa. Nếu số mẫu thuộc tài khoản khác, báo lỗi thay vì ghép nhầm.
- Có thể chạy seeder mà không mở HTTP server: `dotnet run --project be/src/MealTrace.Api/MealTrace.Api.csproj --launch-profile http -- --seed-only`. Chỉ sử dụng ở Development.
- **Admin luôn có luồng thủ công để xử lý thiếu/sai dữ liệu**, kể cả sau import: vào Lớp và trẻ → chọn trẻ → Liên kết phụ huynh; tạo Parent mới bằng SĐT + họ tên hoặc liên kết tài khoản hiện có. Trẻ đã có phụ huynh vẫn được thêm người giám hộ khác, không thay thế liên kết cũ tự động.
- Khi cần sửa liên kết sai: vào Tài khoản → sửa phụ huynh → điều chỉnh danh sách trẻ liên kết và lưu. API hiện yêu cầu tài khoản có vai trò Parent phải liên kết ít nhất một trẻ; xử lý tài khoản sai không còn trẻ cần điều chỉnh vai trò cùng lúc theo quy tắc này.
- Đã mở rộng kiểm thử tích hợp: Admin thêm người giám hộ thứ hai cho trẻ đã có phụ huynh, danh sách lớp trả đủ hai người giám hộ. BE 7/7 tests qua.
- BE/FE vẫn tắt; seed-only kết thúc sau cập nhật DB. Chưa có import hoặc dịch vụ gửi SMS/email.

## Review chất lượng code 30/09/2026

- Báo cáo review đã xóa theo yêu cầu. Checklist test giữ ở máy, được ignore và không đưa lên GitHub. README vẫn để trống theo yêu cầu.
- Đã sửa quyền xem/hủy báo vắng khi ParentStudent bị gỡ; chặn ngoại lệ khác niên khóa; giữ đối tượng đang sửa khi đổi trang tài khoản; bỏ suy luận Admin cuối cùng từ một trang FE (BE kiểm tra toàn DB).
- Đã cập nhật dependency test xunit/SQLitePCLRaw và converter DateTimeOffset chỉ dành cho model test SQLite. Không thay đổi schema PostgreSQL trong lượt review.
- Kết quả cuối: BE 9/9 tests, FE 5/5 tests, build thành công; npm audit và dotnet vulnerable scan không còn cảnh báo. Chưa visual QA và chưa kiểm thử đồng thời PostgreSQL.
- Review còn Important: grant thanh tra chưa có policy/màn đọc nghiệp vụ hoàn chỉnh; scope-options cắt 200 lớp/trẻ; mutation đồng thời cần bảo vệ báo vắng chồng và xử lý conflict DB thống nhất. Chưa coi toàn bộ workflow/quyền là hoàn chỉnh.
- UI ngoại lệ giáo viên hiện chỉ ghi vắng; hoàn tác ngoại lệ trên UI chưa có. Search/role filter tài khoản hiện chỉ áp dụng trang đang xem; filter lớp giáo viên ở BE trước phân trang.
- Giữ BE/FE tắt. Các bản sửa sau review được đưa vào đợt push tiếp theo theo yêu cầu; báo cáo review và checklist không nằm trong commit.

## Proposal cập nhật 30/09/2026

- PROPOSAL.md đã đồng bộ SĐT phụ huynh, không OTP, import Excel chờ mẫu, liên kết thủ công, SMS/email làm sau và bảng tiến độ thực tế.
- Giữ hướng actor/EF code-first, chốt thủ công từ 07:30 UTC+7 và mobile giữ chỗ. Bản sửa tài liệu được đưa vào đợt commit/push cùng thay đổi bỏ role.

## Cập nhật: bỏ hai vai trò chuyên viên dinh dưỡng và kế toán

- Vai trò hiện hành: ADMIN, TEACHER, KITCHEN_STAFF, PARENT. Các mục lịch sử ghi 6 vai trò ở trên đã được thay thế bởi quyết định này.
- FE bỏ hai lựa chọn role và hai tài khoản seed tương ứng; BE không chấp nhận cấp các role đã bỏ. Bếp/Admin phụ trách dữ liệu công thức/dinh dưỡng; Admin quản lý báo cáo.
- Migration RetireNutritionAndAccountingRoles xóa hai role và liên kết quyền trong DB, thu hồi token người bị ảnh hưởng; khóa tài khoản không còn role được hỗ trợ hoặc grant thanh tra đang hiệu lực. Không xóa hồ sơ tài khoản hay lịch sử nghiệp vụ; tài khoản đa role giữ các quyền còn lại. Hai tài khoản demo nutrition/accountant không còn dùng để đăng nhập.
- Các tài khoản demo còn lại giữ nguyên SĐT: Admin 0900000001, Teacher 0900000002, Kitchen 0900000003, Parent 0900000006; mật khẩu demo không đổi.
- Rollback migration chỉ khôi phục định nghĩa role, không tự cấp lại quyền đã gỡ.

- Kiểm chứng sau bỏ role: BE 11/11 tests, FE 5/5 tests; build qua. Migration đã áp dụng vào PostgreSQL local; BE khởi động lại và FE tiếp tục chạy để người dùng test. Các thay đổi role được đưa vào đợt commit/push theo yêu cầu.

## Cập nhật: Admin đặt lại mật khẩu

- API POST /api/admin/users/{id}/reset-password chỉ cho Admin, bắt buộc lý do tối đa 500 ký tự; Swagger có endpoint AdminResetPassword.
- FE Quản lý tài khoản có nút biểu tượng chìa khóa; mở form nêu rõ tài khoản, nhập lý do và xác nhận cấp mật khẩu tạm. Mật khẩu chỉ trả trong response no-store và giữ tạm trong state, không lưu localStorage/cache/query/log; ẩn hoặc tải lại sẽ mất.
- ResetPasswordAsync của Identity hash/salt mật khẩu mới và đổi SecurityStamp, thu hồi JWT cũ. Gỡ lockout do nhập sai nhưng không thay đổi IsActive, role, SĐT/email hay liên kết trẻ/lớp. Tài khoản Admin khóa vẫn không đăng nhập được cho đến khi mở khóa riêng.
- Lưu AccountPasswordResetAudit gồm người xử lý, tài khoản đích, lý do và thời điểm; không lưu mật khẩu/token. Reset và audit trong cùng transaction. Migration AccountPasswordResetAudit đã áp dụng PostgreSQL.
- Không cho Admin cấp mật khẩu tạm cho chính mình qua endpoint này; dùng Đổi mật khẩu tại Hồ sơ của tôi. Khôi phục cho phụ huynh mất SĐT/email: đối chiếu hồ sơ, sửa SĐT/email trên tài khoản cũ rồi cấp mật khẩu tạm, giữ lịch sử/liên kết.
- Chưa có tự khôi phục qua SMS/email. BE 14/14 tests, FE 5/5 tests qua; build thành công. BE được khởi động lại để test, FE tiếp tục chạy; chưa push thay đổi này.

- CSS form đặt lại mật khẩu: dùng panel-head có padding, tách SĐT khỏi lời giải thích; thông báo nền kem/cam nằm trong form, nút tự xuống dòng trên mobile.

## Quy tắc giao diện: chỉnh sửa dùng modal

- Các thao tác sửa tài khoản, cấp lại mật khẩu, liên kết phụ huynh và đổi mật khẩu tại hồ sơ đều mở modal, không chèn form ở đầu/cuối trang. Thêm tài khoản dùng cùng modal; thông tin mật khẩu tạm hiển thị trong modal riêng hoặc modal liên kết.
- Component dùng chung fe/src/components/Modal.tsx, native dialog + portal: khóa cuộn nền, giữ focus bàn phím trong dialog, đóng bằng X/Escape/click ngoài; chặn đóng khi đang gửi. Modal có cuộn riêng và responsive.
- Toast lỗi form được hiển thị trong modal qua Sonner toaster có ID riêng để không bị native dialog che phía sau.
- Chức năng chỉnh sửa bổ sung về sau phải dùng Modal và giữ dữ liệu nhập khi lưu thất bại; chỉ đóng khi thành công hoặc người dùng hủy. Form tạo lớp/trẻ và báo vắng hiện giữ bố cục trang.

## Sửa cảnh báo Sonar ở FE

- Tất cả button trong mã TSX có type rõ ràng: submit cho nút gửi form, button cho điều hướng/thao tác.
- Modal có onKeyDown xử lý Escape, ngăn đóng khi busy và vẫn hỗ trợ native onCancel. Không thêm handler rỗng để né cảnh báo accessibility.
- Chưa chạy lại Sonar server; cần scan mới để xác nhận trạng thái issue.

## Cập nhật: mã trẻ và lịch sử ghi danh (30/09/2026)

### Đã triển khai
- StudentCode cố định, unique, chuẩn hóa chữ hoa; nhập 3–40 ký tự A-Z/số/gạch ngang hoặc tự sinh. Mã không đổi khi sửa tên/chuyển lớp. HS-DEMO-0001 dành riêng cho seed; seeder tìm trẻ/lớp demo qua mã/lịch sử để không tạo lại chỉ vì sửa tên/chuyển lớp.
- Enrollment: StartDate inclusive, EndDate exclusive, lớp, người/lý do ghi danh và kết thúc, thời điểm ghi nhận. Lịch sử không bị xóa khi chuyển lớp/ngừng học/ghi danh lại. Giữ nguyên ParentStudent.
- API Admin: GET /admin/classes và /admin/students có tìm kiếm/phân trang, PUT /admin/classes/{id} sửa tên, PUT /admin/students/{id} sửa hồ sơ, GET/POST /admin/students/{id}/enrollments xem lịch sử/chuyển lớp/ngừng học/ghi danh lại.
- FE Lớp và trẻ: thêm mã/ngày bắt đầu, danh sách tìm kiếm/phân trang/lọc trạng thái/lớp, các form sửa dùng Modal, xem lịch sử bằng Modal. Niên khóa lớp và mã trẻ không sửa trong form chỉnh hồ sơ.
- ScopePicker/ClassPicker có tìm kiếm/phân trang 25 mục; không còn cắt 200 mục. Các ID đã chọn vẫn giữ khi đổi trang/bộ lọc và có danh sách riêng để bỏ chọn. Parent được giữ liên kết với trẻ chưa học/đã ngừng học, không bị mất liên kết khi sửa tài khoản.
- Student.ClassId là pointer lớp ghi danh mới nhất để tương thích; trạng thái/lớp hiện tại và số suất theo ngày phải lấy Enrollment. Student.IsActive không phải nguồn tính trạng thái theo ngày.
- Sau 07:30 UTC+7, chuyển lớp/ngừng học áp dụng sớm nhất ngày mai; không cho hồi tố hay chồng khoảng ngày, ngày thay đổi phải sau StartDate của lần ghi danh mới nhất. EndDate là ngày đầu ngừng học. Nếu lập nhiều thay đổi tương lai, phải theo thứ tự ngày tăng dần.
- Student.Revision bảo vệ cập nhật đồng thời; phiên bản cũ trả 409, không ghi đè hồ sơ/lịch sử. Transaction bảo đảm đóng ghi danh cũ và mở ghi danh mới cùng thành công. DB có unique mã trẻ, ngày bắt đầu/student, một ghi danh mở/student và check EndDate > StartDate.
- Báo vắng PostgreSQL khóa row Student trong transaction trước kiểm tra trùng; hai người giám hộ gửi cùng khoảng ngày chỉ một yêu cầu thành công. Xung đột unique/serialization/deadlock kể cả exception bọc từ Npgsql trả 409.

### Migration và giới hạn dữ liệu cũ
- Migration 20260930142728_StudentEnrollmentHistory đã áp dụng DB local. Trẻ cũ có mã riêng; trẻ IsActive được tạo Enrollment từ ngày migration, lý do ghi rõ chuyển đổi dữ liệu cũ. Không biết ngày nhập học/chuyển lớp thật nên không tự dựng lịch sử trước migration. Hồ sơ đã inactive chưa có lịch sử sẽ giữ không có Enrollment.
- RecordedAt của bản ghi chuyển đổi là thời điểm migration: phiên ăn chưa chốt có cutoff trước thời điểm chuyển đổi không tự nhận bản ghi mới này. Bản chốt cũ giữ nguyên. Khi cần phục dựng lịch sử thực, phải thiết kế luồng đối chiếu hồ sơ/audit; không sửa SQL tùy tiện.
- Chưa có hủy/sửa lịch chuyển lớp tương lai, điều chỉnh sau chốt, lịch ngày nghỉ hoặc import Excel. Parent báo vắng tối đa 90 ngày; suất vẫn chỉ lấy trẻ có Enrollment hiệu lực vào ngày ăn.
- Các API legacy GET /classes và /classes/{id}/students vẫn giữ response array để tương thích; màn Admin mới dùng các API phân trang. Không coi tất cả API đọc đã được phân trang.

### Kiểm chứng
- BE 17 tests SQLite đạt (14 cũ + 3 mới): mã unique/giữ nguyên sau sửa/stale revision, chuyển lớp/ngừng học/ghi danh lại theo ngày và giữ phụ huynh, tìm trẻ ngoài 200 mục/phạm vi đã chọn ngoài trang.
- Hai tests PostgreSQL thực đạt: chuyển lớp đồng thời chỉ mở một Enrollment; báo vắng đồng thời/chốt suất đồng thời không nhân đôi. Test tạo schema mealtrace_test_<GUID>, không sửa bảng dữ liệu người dùng, xóa schema khi kết thúc. Để chạy lại, đặt MEALTRACE_TEST_CONNECTION bằng connection local (không in/commit secrets); mặc định 2 test này skip khi thiếu biến.
- FE 5 tests đạt, build FE/BE thành công. Smoke qua proxy 5173: đăng nhập Admin bằng SĐT thành công, GET lớp/trẻ/phạm vi thành công, cả 4 trẻ local có mã, health OK; Swagger có endpoint ghi danh.
- Chưa visual QA toàn bộ UI, chưa chạy lại Sonar. README tiếp tục để trống; checklist local vẫn ignore.
- BE http://localhost:5184 và FE http://localhost:5173 đã chạy để test. Swagger: http://localhost:5184/swagger.

### Bước tiếp theo
1. Người dùng test tạo trẻ, sửa tên lớp/trẻ, chuyển lớp tương lai, ngừng/ghi danh lại, kiểm tra lịch sử và phụ huynh.
2. Hoàn thiện ngoại lệ giáo viên (có/vắng/hoàn tác), lịch ngày nghỉ và quy trình điều chỉnh sau chốt có audit.
3. Thực đơn/công thức/dinh dưỡng; món thực tế và ảnh; phụ huynh xem thông tin công bố; báo cáo/truy vết.
4. Import khi có mẫu Excel; gửi SMS/email sau. Mobile vẫn giữ chỗ.

## Cập nhật proposal và kế hoạch tiếp theo

- PROPOSAL.md đồng bộ tiến độ đến commit 8865d55, bổ sung quy tắc ngày hiệu lực, trạng thái thực tế của ngoại lệ/báo vắng muộn, reset mật khẩu và mục 13 kế hoạch từ trạng thái hiện tại.
- docs/implementation-plan.md là nguồn kế hoạch đang áp dụng: nghiệm thu dữ liệu nền → ngoại lệ trước chốt → lịch ngày nghỉ → điều chỉnh sau chốt → thực đơn/dinh dưỡng → món thực tế/ảnh/Parent → báo cáo/truy vết.
- Đợt code kế tiếp chỉ tập trung ngoại lệ trước chốt: có/vắng/khôi phục mặc định, hiển thị cả trẻ có/vắng, actor/lý do/lịch sử, quyền theo lớp tại ngày ăn, concurrency và boundary cut-off. Khôi phục không xóa báo vắng Parent.
- Giữ 4 role, giờ chốt 07:30 UTC+7, web responsive/clay/Modal/Sonner, code-first và không Docker. Không yêu cầu điểm danh có hằng ngày; import chờ Excel, SMS/email/mobile/tài chính làm sau.
- Kế hoạch 14 tuần trong proposal vẫn là tham khảo; không coi đó là xác nhận tiến độ. Các mô tả chặng mới là kế hoạch, chưa có code/migration/test mới ở lượt cập nhật tài liệu này.

## Cập nhật: ngoại lệ trước giờ chốt (30/09/2026)

- Đã triển khai API và FE Admin/Teacher: danh sách cả trẻ có suất/vắng, tìm kiếm/lọc lớp/phân trang; modal chọn có suất, không có suất, khôi phục mặc định; lịch sử có người xử lý, lý do và thời điểm. Trạng thái vẫn là dự kiến ăn, không phải điểm danh thực tế.
- MealDecisionService dùng chung cho danh sách nguồn và tính suất: Enrollment tại ngày ăn → báo vắng Parent → ngoại lệ mới nhất. Khôi phục ghi sự kiện WillEat=null, tiếp tục áp dụng báo vắng Parent; không xóa báo vắng hay sự kiện cũ.
- Quyền Admin toàn trường, Teacher theo lớp phân công và Enrollment tại ngày ăn/niên khóa. Parent/Kitchen không đọc/ghi API ngoại lệ. Sau cut-off (kể cả chưa chốt) hoặc đã chốt chỉ xem lịch sử; bản chốt giữ nguyên.
- MealRegistration lưu Sequence, RecordedByUserId/RecordedByName, SupersedesId; DbContext chặn sửa/xóa sự kiện. Migration 20260930161118_MealExceptionAudit đã áp dụng PostgreSQL local: đánh số bản cũ theo thời điểm/ID, không dựng actor hay liên kết giả. Bản cũ thiếu actor hiển thị rõ chưa ghi người xử lý. Down từ chối khi có sự kiện khôi phục để tránh đổi null thành false sai nghĩa.
- Ghi ngoại lệ nhận ExpectedEventId; nguồn cũ trả 409. PostgreSQL khóa MealDay rồi Student trong transaction; chốt suất khóa cùng MealDay. DecisionRevision là concurrency token; unique day/student/Sequence. Lấy giờ từ TimeProvider, kiểm tra lại sau chờ khóa và ngay trước lưu; tại cut-off bị từ chối.
- Modal giữ dữ liệu khi lỗi, toast trong modal, chỉ đóng sau lưu thành công; cập nhật lại nguồn/số suất/lịch sử. Danh sách refetch 15 giây và khóa nút theo giờ chốt. Xung đột yêu cầu tải/mở lại form, không tự gửi lại với nguồn mới.
- Kiểm chứng: 27 tests SQLite + 3 tests PostgreSQL thực + 5 tests FE đạt; FE/BE build thành công. Test mới gồm khôi phục với/không báo vắng, lý do sai/nguồn cũ, trước/đúng/sau cut-off, chuyển lớp qua API, sai scope/role, bản chốt bất biến, dữ liệu cũ và lịch sử phân trang, hai ngoại lệ đồng thời chỉ một thắng.
- UI smoke: đăng nhập Admin demo, chọn phiên 01/10, thấy cả trẻ báo vắng và mặc định có suất, mở/đóng modal và lịch sử rỗng; không lưu thay đổi lên dữ liệu demo. Modal width 352px trong viewport 390px, trang không tràn ngang; console không có error. Chưa nghiệm thu toàn bộ UI/role và chưa chạy lại Sonar.
- PROPOSAL.md và docs/implementation-plan.md đã đồng bộ. Checklist thêm ca ngoại lệ, vẫn ignore; README giữ trống. Thay đổi lượt này chưa commit/push.
- BE http://localhost:5184, Swagger /swagger; FE http://localhost:5173 đang chạy để test.
- Bước tiếp theo: lịch vận hành/ngày nghỉ theo niên khóa, kiểm tra tạo phiên và xử lý phiên tồn tại; không suy ra lịch trường khi chưa cấu hình. Sau đó điều chỉnh sau chốt có audit, thực đơn/dinh dưỡng, món thực tế/ảnh, Parent và báo cáo.

## Cập nhật: không ăn tại trường dài hạn

- Theo yêu cầu mới, Parent có thể đăng ký không ăn khi trẻ vẫn đi học; không dùng ngừng ghi danh cho trường hợp này. Giới hạn 90 ngày được thay bằng tối đa một năm lịch tính từ FromDate, ToDate tối đa FromDate.AddYears(1).AddDays(-1), tính cả hai đầu ngày; không chỉ giới hạn trong một niên khóa. Số suất vẫn kiểm tra Enrollment tại từng ngày ăn.
- FE Báo vắng / Không ăn tại trường: nút chọn nhanh tuần/tháng/năm, giới hạn ngày kết thúc; danh sách phân biệt sắp áp dụng/đang hiệu lực/hết hạn/hủy-thay thế. Sửa khoảng bằng Modal, giữ dữ liệu khi lỗi; Hủy / Ăn lại khi đổi ý. Không thay đổi trạng thái học của trẻ.
- API POST /parent/absences/{id}/replace: chỉ Parent đã gửi đăng ký và còn liên kết trẻ; giữ ngày bắt đầu cũ đã qua hoặc chọn từ hôm nay, ngày kết thúc từ hôm nay, không trùng đăng ký khác. Khóa Student trong transaction; hủy bản cũ + thêm bản mới có cùng timestamp, không ghi đè FromDate/ToDate/Reason cũ. Hai thao tác trên bản cũ chỉ một thành công, bản lỗi thời trả 409. API cancel dùng cùng khóa để không đua với replace; clock dùng TimeProvider.
- Bản cũ giữ nguyên ngày/lý do/ReportedAt, thêm CancelledAt; tính nguồn tại cut-off vẫn dùng bản cũ nếu thay đổi sau cut-off. Bản chốt không đổi. Hết khoảng hoặc hủy thì về mặc định có suất nếu không có nguồn ngoại lệ khác. Không ăn không phải bằng chứng vắng học. Chưa thêm loại dữ liệu phân biệt nghỉ học và không ăn; hiện lý do thể hiện nhu cầu, nguồn tính vẫn MealAbsence.
- Không cần migration mới cho thay đổi này. Kiểm chứng 29 SQLite + 4 PostgreSQL = 33 BE tests đều đạt; FE 5 tests đạt, build qua. Test mới kiểm tra cả năm/vượt giới hạn, rút ngắn/hủy và giữ Enrollment, bảo toàn nguồn trước cut-off, trùng khoảng/sai trẻ/sai role, hai cập nhật đồng thời không tạo hai bản thay thế.
- Proposal/kế hoạch/checklist local đã cập nhật; chưa push. Bước kế tiếp vẫn là lịch vận hành/ngày nghỉ.

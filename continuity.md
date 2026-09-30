# MealTrace — Continuity

> Cập nhật 30/09/2026. Proposal đang áp dụng: [PROPOSAL.md](PROPOSAL.md). Mã nguồn đã push gần nhất: `77cb2ee`; phần workflow dưới đây còn ở working tree. Không dùng Docker. `README.md` để trống theo yêu cầu.

`mobile/` hiện chỉ có `.gitkeep` để giữ chỗ trong Git; chưa có ứng dụng mobile hoặc thay đổi phạm vi MVP. Chạy web local bằng `dotnet run --project be/src/MealTrace.Api/MealTrace.Api.csproj --launch-profile http` ở root và `npm run dev` trong `fe/` (cổng 5184 và 5173).

## Trạng thái hiện tại

**Đã có lát cắt đầu tiên của workflow chính, chưa phải MVP hoàn chỉnh.** Hệ thống đang có đăng nhập/6 vai trò, quản lý tài khoản, lớp và trẻ tạo thủ công, phụ huynh báo vắng ngày/khoảng ngày, xem danh sách dự kiến ăn theo lớp và chốt số suất gửi bếp. Backend dùng ASP.NET Core 8, EF Core code-first, PostgreSQL; frontend React/TypeScript. Swagger hoạt động trong Development.

Mặc định trẻ đang hoạt động trong lớp của niên khóa được **dự kiến ăn** vào `MealDay` đã tạo. Báo vắng được tính nếu ghi trước `CutoffAt`. Giáo viên có thể ghi ngoại lệ trước giờ chốt cho lớp được giao. Bản chốt lưu danh sách ID và tên trẻ theo lớp; sau chốt, báo vắng bị hủy hoặc thêm trẻ mới không thay đổi số suất đã gửi. Đây là số suất dự kiến, **không phải điểm danh có mặt thực tế**.

## Những gì đã chạy và kiểm chứng

- Auth: Identity, mật khẩu hash/salt qua PasswordHasher của ASP.NET Identity, JWT, kiểm tra role/scope tại API; 6 tài khoản demo chỉ trong Development.
- Admin: tạo lớp, thêm trẻ và liên kết giáo viên/phụ huynh qua quản trị tài khoản. Nhập lớp/trẻ từ tệp **chưa có**.
- Admin có thể liên kết một trẻ với phụ huynh ngay tại màn **Lớp và trẻ** bằng SĐT. SĐT đã tồn tại được ghép vào tài khoản đang hoạt động; SĐT mới tạo tài khoản Parent với mật khẩu tạm chỉ hiển thị một lần. Một phụ huynh có thể liên kết nhiều trẻ và một trẻ có thể có nhiều người giám hộ. Danh sách tài khoản lọc theo lớp giáo viên phụ trách ở API trước khi phân trang.
- Parent: xem trẻ được liên kết; gửi/hủy báo vắng một ngày hoặc tối đa 90 ngày. Không được báo vắng cho trẻ ngoài liên kết.
- Teacher: xem danh sách dự kiến ăn chỉ cho lớp được phân công; ghi ngoại lệ trước giờ chốt.
- Admin: tạo phiên ăn với niên khóa, giờ chốt mặc định 07:30 UTC+7; chốt số suất theo lớp. Kitchen/Nutritionist xem bản chốt.
- EF migrations `CoreMealWorkflow` và `WorkflowSettlementState` đã áp dụng lên PostgreSQL local `mealtrace`.
- `dotnet test be/MealTrace.sln --no-restore`: 5/5 pass, gồm kịch bản parent báo vắng, quyền xem lớp, bản chốt không đổi sau hủy báo vắng/thêm trẻ, tạo/ghép phụ huynh và lọc giáo viên theo lớp. `npm run build` và `npm test` trong `fe/`: pass.
- Smoke trên PostgreSQL local: API chạy, Parent lấy được danh sách một trẻ demo; Admin lấy danh sách phiên workflow, danh sách trẻ trong lớp và tài khoản được lọc theo lớp. Chưa thử thao tác tạo/chốt/liên kết mới trên PostgreSQL thật; test nghiệp vụ tự động hiện dùng SQLite.

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
| P0 | Enrollment theo khoảng thời gian và lịch sử chuyển lớp | `Student.ClassId` hiện là lớp hiện tại. Bản chốt đã lưu ID/tên, nhưng chưa ghi enrollment nguồn nên chưa giải thích đầy đủ việc chuyển lớp về sau. |
| P0 | Kiểm thử workflow trên PostgreSQL với dữ liệu thử biệt lập | Migration đã chạy, nhưng chưa có test tự động PostgreSQL cho cut-off, transaction đồng thời và đầy đủ API. |
| P1 | Quy trình sửa số suất sau chốt bằng bản điều chỉnh có lý do | Hiện bản chốt bất biến và chỉ cho chốt một lần; báo vắng muộn không làm đổi bản đã chốt. Chưa có amendment. |
| P1 | Cấu hình giờ chốt theo trường/niên khóa và ngày nghỉ | Hiện cố định 07:30 UTC+7. Cần trường xác nhận quy tắc chính thức. |
| P1 | Import danh sách lớp/trẻ từ tệp, kiểm tra trùng và lỗi theo dòng | Hiện chỉ tạo từng lớp/trẻ trên UI. |
| P1 | Liên kết phụ huynh hàng loạt sau import | Tạm hoãn theo yêu cầu vì chưa có mẫu tệp. Khi triển khai cần mã học sinh duy nhất và SĐT phụ huynh trong tệp. Bước xem trước phải đối chiếu từng dòng, trùng SĐT, trẻ trùng mã và lỗi lớp trước khi ghi DB; sau xác nhận mới tạo/ghép tài khoản theo SĐT và quan hệ ParentStudent trong một giao dịch. Gửi thông tin tài khoản qua SMS tới SĐT và qua email nếu có sẽ triển khai ở giai đoạn sau, sau khi import thành công. `scope-options` hiện chỉ tải tối đa 200 trẻ nên không dùng checkbox hiện tại cho nhập hàng loạt. |
| P1 | Phân biệt điểm danh có mặt thực tế sau giờ chốt | Ngoại lệ trước giờ chốt chỉ phục vụ số suất; chưa có luồng ghi nhận thực tế 08:30 và đối chiếu. |
| P1 | Thực đơn, công thức dinh dưỡng, công bố, món thực tế/đổi món, ảnh, màn phụ huynh theo dõi | Model thực đơn/recipe cũ mới ở mức sơ bộ, chưa có workflow thao tác. |
| P1 | Báo cáo có phiên bản, dữ liệu nguồn và bản tính lại | `ReportSnapshot` hiện chỉ là scaffold, chưa có phép tính/duyệt/điều chỉnh đáng tin cậy. |
| P2 | Phí, sổ cái, offline ảnh, thực đơn riêng trẻ, dashboard nâng cao | Phần mở rộng theo proposal sau khi MVP ổn định. |

## Quyết định cần xác nhận

1. Giờ chốt số suất và hạn công bố thực đơn; xử lý báo vắng muộn, hoàn phí và ngày nghỉ.
2. Ai được chốt, ai được duyệt điều chỉnh sau chốt, bếp cần thấy danh sách trẻ hay chỉ tổng số theo lớp.
3. Mẫu báo cáo gửi cơ quan chuyên trách, nguồn dữ liệu thành phần thực phẩm, người duyệt.
4. Quy trình xác nhận dị ứng/lưu ý ăn uống, lưu ảnh và phân quyền xem.

## Thứ tự triển khai tiếp theo

1. Bổ sung mã học sinh duy nhất, enrollment hiệu lực, kiểm thử PostgreSQL; import lớp/trẻ/phụ huynh bằng tệp có xem trước triển khai sau khi có mẫu tệp cho workflow hiện có.
2. Thêm ghi nhận có mặt thực tế và amendment sau giờ chốt; đối chiếu số suất dự kiến với thực tế.
3. Bếp tạo/công bố thực đơn có phiên bản, tính dinh dưỡng từ công thức; phụ huynh xem bản đã công bố.
4. Ghi món thực tế, đổi món và ảnh; báo cáo có nguồn và bản duyệt/tính lại.

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

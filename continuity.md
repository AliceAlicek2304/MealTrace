# MealTrace — Continuity

> Cập nhật: 2026-09-30. Nguồn: proposal MealTrace, bảng actor/FR và entity list mới do chủ dự án cung cấp; source khởi tạo ở commit `1eab3a2` trên nhánh `main`. Thay đổi FE/BE sau commit này hiện ở local. Đây là tài liệu theo dõi công việc; cập nhật sau mỗi thay đổi đáng kể.

## 1. Mục tiêu và mức hiện tại

MealTrace dành cho quản lý bữa ăn bán trú trường mầm non công lập. Điểm khác biệt của đề tài là **mỗi số liệu dinh dưỡng, chi phí, số suất phải truy ngược được tới bản ghi vận hành và đúng phiên bản dữ liệu nguồn**. Sau correction, báo cáo đã ký vẫn phải tái lập được; kết quả tính lại cần chỉ ra nguyên nhân chênh lệch.

**Mức hiện tại: bộ khung kỹ thuật trước MVP (pre-MVP scaffold).** Chưa có quy trình nghiệp vụ khép kín hoặc dữ liệu thật được xác minh. Không nên gọi đây là MVP hoàn thành chỉ vì build thành công.

Luồng đích:

`Học sinh/lớp → nguyên liệu & công thức có phiên bản → thực đơn công bố → đăng ký/điểm danh → chốt suất → thực hiện & minh chứng → tính dinh dưỡng/chi phí → báo cáo ký → drill-down/correction/recompute`.

## 2. Cấu trúc và công nghệ

| Phần | Vị trí | Hiện trạng |
| --- | --- | --- |
| Frontend | `fe/` | React 19, TypeScript, Vite, TanStack Query, Axios, Lucide, Vitest; đăng nhập, quản trị tài khoản và màn ngày ăn. Ma trận quyền đã bỏ khỏi web theo yêu cầu. |
| Backend | `be/src/MealTrace.Api/` | ASP.NET Core 8 Minimal API, EF Core 8, Identity, JWT Bearer, Npgsql, Swagger Bearer |
| Database | PostgreSQL | Migration `InitialCreate` và `IdentityAndScopes` đã áp dụng vào DB `mealtrace` local; seed 6 role và 6 tài khoản demo đã chạy |
| Công cụ EF | `.config/dotnet-tools.json` | `dotnet-ef` 8.0.11 local tool |
| Ghi chú vận hành | `continuity.md` | PostgreSQL cài trực tiếp trên Windows, không dùng Docker; connection string local được Git bỏ qua. `README.md` đang để trống theo yêu cầu chủ dự án. |

API hiện có: health; `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`, `POST /api/auth/change-password`; `GET/POST/PUT /api/admin/users`, `GET /api/admin/scope-options`; các endpoint đọc ngày ăn và report lineage. Backend đã chặn theo role. Đăng nhập, `/me`, danh sách Admin và quyền đọc ngày ăn đã được thử với DB thật.

Schema đã có các entity `SchoolClass`, `Student`, `Ingredient/IngredientVersion`, `Recipe/RecipeVersion/RecipeIngredient`, `MealDay/MenuDish`, `MealRegistration`, `PortionSettlement`, `MealEvidence`, `ReportSnapshot`. Các trường `SupersedesId`/`AmendsId` mới là chỗ để nối lịch sử; chưa có logic bảo đảm append-only hoặc correction hợp lệ.

## 3. Đã kiểm chứng và chưa kiểm chứng

**Đã kiểm chứng trong môi trường local:**

- `dotnet build be/MealTrace.sln --no-restore`: thành công, 0 warning/error.
- `npm run build` trong `fe/`: thành công.
- `npm test` trong `fe/`: 3 test FE cho phạm vi, guard Admin cuối cùng và grant thanh tra đều qua.
- `dotnet test be/MealTrace.sln`: 3 integration test BE qua với SQLite tạm (anonymous bị chặn, Teacher không vào API Admin, token Teacher cũ bị thu hồi sau khi Admin khóa tài khoản, Admin tạo tài khoản đa vai trò đăng nhập được, đổi mật khẩu và đăng xuất thu hồi token cũ).
- Swagger JSON đã kiểm tra có login, API Admin và Bearer; GET ngày ăn không có token trả 401 khi chạy smoke test không kết nối DB.
- ASP.NET API khởi động và `GET /api/health` trả `{"status":"ok","service":"MealTrace API"}` khi cung cấp connection string tạm.
- EF tạo migration và sinh script SQL thành công khi có connection string tạm. Việc sinh script **không chứng minh** migration đã chạy trên PostgreSQL.
- PostgreSQL local đã chạy hai migration và Development seeder; Swagger trả 200, cả 6 tài khoản đăng nhập được. Cả 6 tài khoản seed hiện dùng mật khẩu Development `123456@@`; đã thử lại qua FE proxy và nhận 200. Admin vào `/api/admin/users` được; năm role còn lại nhận 403. Teacher và Parent nhận 403 ở `/api/meal-days`; bốn role nhân sự được phép nhận 200.
- FE `http://localhost:5173` trả 200; Vite proxy `/api/health` và `/api/auth/login` tới backend trả 200. Connection string nằm trong `appsettings.Development.local.json` được Git bỏ qua; JWT key và 6 mật khẩu seed nằm trong User Secrets.
- Commit khởi tạo đã được push lên `origin/main` (`1eab3a2`).

**Chưa kiểm chứng:** thao tác Admin tạo/sửa tài khoản qua FE trên PostgreSQL (đã có integration test BE bằng SQLite), kiểm tra UI bằng trình duyệt, các endpoint đọc với dữ liệu ngày ăn thực, và CI. Dữ liệu seed hiện chỉ có lớp/học sinh demo; chưa có ngày ăn.

## 4. Tình trạng chức năng theo proposal

| Nhóm chức năng | Tình trạng thực tế |
| --- | --- |
| Tài khoản, vai trò, quyền theo học sinh | Identity + JWT, 6 role Development seed, bảng scope và API Admin đã viết; FE gọi API thật. Chưa kiểm thử trên DB, chưa có ABAC bảo vệ endpoint nghiệp vụ theo lớp/học sinh |
| Lớp, học sinh, enrollment, liên kết phụ huynh | Chỉ có model lớp/học sinh cơ bản; chưa có enrollment theo thời gian hoặc liên kết phụ huynh |
| Nguyên liệu, giá, conversion, công thức | Có model phiên bản sơ bộ; chưa có API quản lý, quy tắc hiệu lực hoặc kiểm tra dữ liệu |
| Thực đơn ngày/tuần và công bố | Có model ngày ăn/món và API đọc; chưa có API tạo/sửa/công bố |
| Đăng ký, báo nghỉ, attendance, chốt suất | Có model đăng ký và chốt; chưa có attendance, API hoặc logic cut-off/tổng hợp |
| Chuẩn bị, món thực tế, substitution | Chưa có model/quy trình đầy đủ |
| Food check, retained sample, ảnh, offline sync | Có model minh chứng dạng tổng quát; chưa có upload/storage, kiểm tra, đồng bộ offline |
| Dinh dưỡng, chi phí, phí suất ăn | Có vài trường đầu vào và `ReportSnapshot`; chưa có bộ tính toán, phí/đối soát |
| Ký, tái lập, recompute, so sánh, drill-down | Chỉ có trường snapshot và API trả `SourceJson`; chưa có nghiệp vụ thực thi |
| Thông báo, dashboard | FE hiển thị ba số đếm từ API; chưa có thông báo hay dashboard nghiệp vụ |
| AI | Chưa xác định phạm vi trong proposal; chưa triển khai |

## 5. Vấn đề đã thấy trong source

Phân biệt **lỗi/điểm hở đã xác nhận bằng code** với **chưa kiểm chứng**. Mức P0 cần xử lý trước khi dùng dữ liệu thật; P1 trước khi gọi là MVP; P2 là cải thiện tiếp theo.

| ID | Mức | Vấn đề và tác động | Hướng xử lý |
| --- | --- | --- | --- |
| MT-01 | P0 | API ngày ăn/report đã có role gate; Teacher/Parent bị chặn tạm thời vì chưa có lọc theo lớp/học sinh. Các role nhân sự còn lại xem dữ liệu toàn trường, kể cả thực đơn chưa công bố. | Thêm ABAC theo assignment và chỉ trả dữ liệu đã công bố cho Parent khi xây endpoint riêng. |
| MT-02 | P0 | `IngredientVersion` gộp dinh dưỡng, giá và edible fraction; thiếu phiên bản conversion/price độc lập. Snapshot chỉ giữ `SourceJson` tự do, chưa có quy tắc đóng băng nguồn. Chưa đạt mục tiêu tái lập báo cáo. | Chốt mô hình version/effective time cho từng factor; lưu ID phiên bản, input và thuật toán tính trong snapshot bất biến. |
| MT-03 | P0 | `ReportSnapshot.SignedAt` và các bản ghi lịch sử chỉ là cột dữ liệu; chưa có cơ chế cấm sửa/xóa sau ký, correction và recompute. | Thiết kế workflow append-only, transaction và audit; test tái lập báo cáo trước/sau correction. |
| MT-04 | P1 | `GET /api/meal-days` sắp ngày tăng dần rồi `Take(100)`: khi quá 100 ngày sẽ chỉ hiện ngày cũ. Ba số đếm FE cũng chỉ đếm trang này nhưng đang trình bày như tổng quan. | Phân trang theo ngày giảm dần; tạo endpoint tổng hợp riêng hoặc ghi rõ phạm vi số liệu. |
| MT-05 | P1 | `MealRegistration` và `PortionSettlement` có thời điểm/lý do/supersedes nhưng không có quy tắc chọn bản hiệu lực, chống ghi đè/ghi trùng, kiểm tra cut-off hay xử lý race. | Xây command service trong transaction, ràng buộc DB phù hợp, test cạnh tranh và correction sau cut-off. |
| MT-06 | P1 | `Student` chỉ có `ClassId` hiện tại; chưa lưu enrollment theo năm/thời gian. Đổi lớp có thể khiến truy vết số liệu cũ sai. | Tách enrollment có khoảng hiệu lực và trạng thái bán trú; report tham chiếu enrollment đã dùng. |
| MT-07 | P1 | Không có attendance, actual dishes/substitution, food-safety record có cấu trúc và trạng thái chuẩn bị. Execution record trong proposal chưa hình thành. | Bổ sung model và API theo từng bước workflow, không dồn vào trường `Description` tự do. |
| MT-08 | P1 | Migration và auth đã chạy trên PostgreSQL local; 3 integration test BE vẫn dùng SQLite, chưa có test PostgreSQL tự động hoặc CI. | Thêm test PostgreSQL tự động và CI. |
| MT-09 | P2 | FE đã gọi API thật và chỉ hiển thị màn theo role, nhưng session hiện chỉ ở bộ nhớ và chưa có URL routing. Tải lại trang cần đăng nhập lại. | Chốt yêu cầu session trước khi bổ sung route guard; BE vẫn là nơi thực thi quyền. |
| MT-10 | P2 | `/api/health` chỉ xác nhận tiến trình web còn chạy, không kiểm tra kết nối DB. | Thêm readiness endpoint riêng kiểm tra DB khi cần triển khai/monitoring. |
| MT-11 | P0 | Backend đã có Identity/role gate và FE gọi API, nhưng quyền thanh tra chỉ có grant lưu DB, chưa có endpoint/policy đọc tương ứng; scope Teacher/Parent chưa được áp trên API nghiệp vụ. | Viết policy/resource handler cho ABAC, test truy cập chéo, thực thi grant hết hạn trên BE. Không dùng FE làm ranh giới bảo mật. |

## 6. Quyết định cần chốt với chủ dự án/giảng viên

Không tự coi các điểm sau là requirement cuối cùng vì proposal còn mâu thuẫn hoặc để mở:

1. **Actor/permission:** tài liệu mới xác định 6 vai trò chính: `ADMIN`, `TEACHER`, `KITCHEN_STAFF`, `NUTRITIONIST`, `ACCOUNTANT`, `PARENT`. Tài khoản đa vai trò; Teacher theo lớp được giao, Parent theo con liên kết; thanh tra nhận grant chỉ đọc có hạn riêng. Vẫn cần chốt ma trận chi tiết, quyền theo trường và hành vi khi một người kiêm nhiều vai trò.
2. **Nguồn chốt suất:** công thức giữa đăng ký, báo nghỉ, attendance, học sinh mặc định ăn và xử lý sau cut-off; ai có quyền chốt/điều chỉnh, theo lớp hay toàn trường.
3. **Thời gian:** timezone trường, giờ cut-off, thời điểm hiệu lực của enrollment/price/recipe/conversion và cách xử lý correction lùi ngày.
4. **Dinh dưỡng/giá:** nguồn food composition; đơn vị và cách quy đổi phần ăn, hao hụt, làm tròn; giá theo thời điểm nào; tính theo món dự kiến hay món thực tế.
5. **Báo cáo:** ai ký, điều kiện ký, định dạng bản phát hành, những thay đổi nào yêu cầu recompute và cách hiển thị chênh lệch.
6. **Minh chứng/offline:** loại check 3 bước, retained sample, dung lượng/định dạng ảnh, nơi lưu file, xung đột đồng bộ và quyền xem của phụ huynh.
7. **Phí:** quy tắc phí do trường đặt, kỳ thu, điều chỉnh và đối soát; không có thanh toán trực tuyến trong MVP.
8. **AI:** chỉ nghiên cứu sau khi phạm vi được giảng viên xác nhận; không đưa vào đường tính/duyệt báo cáo cốt lõi.

**Lưu ý tài liệu đầu vào mới:** tiêu đề entity list ghi “24 thực thể” nhưng đánh mã `ET-01` đến `ET-32` (32 mục). Bảng FR dùng `FR-227` cho dự trù nguyên liệu trong khi ghi chú gọi `FR-27`; ghi chú FR-31/32 của Parent lệch với bảng FR-31/32. Giữ mã theo bảng khi triển khai tạm thời và xin bản chuẩn hóa trước khi dùng mã làm contract/API. Chưa xác minh độc lập các viện dẫn quy định pháp lý trong tài liệu.

## 7. Kế hoạch triển khai theo thứ tự phụ thuộc

### Chặng 0 — Nền chạy thật và baseline

- Tạo DB PostgreSQL local, lưu connection string trong file Development local được Git bỏ qua, chạy migration. **Đã xong trên máy hiện tại.**
- Có dataset dev nhỏ: 2 lớp, vài học sinh, nguyên liệu/công thức, ngày ăn; không dùng PII thật. Thêm entity `School` khi chốt phạm vi đa trường.
- Thêm integration test PostgreSQL cho migration và endpoint; CI chạy build FE/BE và test.
- Sửa MT-04 để tổng quan không hiển thị số sai khi quá 100 ngày.
- **Xong khi:** có hướng dẫn chạy được chủ dự án duyệt; máy mới chạy được FE + API + DB; dữ liệu mẫu hiện đúng ở màn hình.

### Chặng 1 — Ranh giới quyền và dữ liệu nền

- Chốt ma trận actor; thêm tài khoản, đăng nhập, role/policy, liên kết Parent–Student và phạm vi trường/lớp.
- Thiết kế enrollment có lịch sử; tách/phiên bản hóa ingredient composition, price, conversion, recipe.
- CRUD có validation, audit, thời điểm hiệu lực; test quyền truy cập chéo học sinh/lớp.
- **Xong khi:** Parent chỉ xem con được liên kết; thay đổi factor không làm mất phiên bản cũ; người không có quyền không xem/sửa được dữ liệu.

### Chặng 2 — Luồng bữa ăn và số suất

- Tạo/công bố thực đơn; đăng ký/báo nghỉ; attendance; rule cut-off; tổng hợp theo lớp/trường; chốt suất.
- Correction sau cut-off là bản ghi mới có lý do, người thao tác, thời điểm và liên kết bản bị thay thế.
- **Xong khi:** với một ngày ăn mẫu, có thể đi từ menu đến settled count và giải thích từng suất đến từ đâu; test biên cut-off và cập nhật đồng thời.

### Chặng 3 — Thực hiện và minh chứng

- Trạng thái chuẩn bị; món thực tế và substitution; food check 3 bước; retained sample; upload ảnh an toàn.
- Lưu capture time và sync time; hàng đợi offline/idempotency khi phạm vi offline được chốt.
- **Xong khi:** evidence dossier của ngày ăn đối chiếu được menu, món thực tế, số suất chốt và toàn bộ amendment.

### Chặng 4 — Tính toán, báo cáo và truy vết (trọng tâm đề tài)

- Tính năng lượng, chất dinh dưỡng, chi phí từ execution record + đúng factor version; ghi cả input, phiên bản và quy tắc làm tròn.
- Tạo report snapshot bất biến, ký, drill-down tới source record; correction tạo recompute mới và so sánh chênh lệch với bản ký.
- **Xong khi:** cùng snapshot luôn cho cùng kết quả; sửa giá/recipe/portion về sau không làm thay đổi bản ký; người dùng xem được nguồn và lý do khác biệt.

### Chặng 5 — Các phần MVP còn lại

- Mức phí do trường đặt, khoản phải thu và đối soát; thông báo cơ bản; dashboard theo vai trò/ngày/lớp.
- Kiểm tra bảo mật, hiệu năng danh sách, backup/restore, tài liệu API và UAT theo kịch bản trường.
- **Xong khi:** các mục MVP trong proposal có luồng thao tác và bằng chứng test; không có endpoint nghiệp vụ nhạy cảm công khai.

## 8. Việc tiếp theo đề xuất

**Ưu tiên ngay:** kiểm tra thao tác Admin tạo/sửa tài khoản qua FE trên PostgreSQL và kiểm tra UI bằng trình duyệt; sau đó thiết kế ABAC theo lớp/học sinh cùng integration test PostgreSQL tự động. Không lưu mật khẩu hoặc dữ liệu học sinh thật vào repository.

Lệnh kiểm tra nhanh (từ root repository):

```powershell
dotnet build be/MealTrace.sln
cd fe
npm ci
npm run build
```

Các lệnh chạy hiện dùng trong môi trường local: `dotnet run --project be/src/MealTrace.Api/MealTrace.Api.csproj --launch-profile http` và `npm run dev` trong `fe/`. Tài liệu hướng dẫn chính thức sẽ viết sau.

## 9. Cách duy trì tài liệu này

Sau mỗi PR/chặng: cập nhật ngày và commit, chuyển mục đã xong sang phần đã kiểm chứng, ghi issue mới với mức ưu tiên và vị trí code, sửa bảng chức năng, ghi quyết định nghiệp vụ đã được xác nhận cùng người/ngày xác nhận, cập nhật bước tiếp theo. Chỉ đánh dấu **xong** khi có hành vi chạy được và test/bằng chứng tương ứng; phân biệt build thành công với workflow hoạt động trên DB.

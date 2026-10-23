# MealTrace — Kế hoạch triển khai tiếp theo

> **Baseline:** [PROPOSAL.md v1.3](../PROPOSAL.md), đồng bộ ngày 23/10/2026. Đây là roadmap tiếp tục từ source hiện có, không phải kế hoạch khởi động lại dự án hoặc cam kết ngày hoàn thành.
> **Căn cứ trạng thái:** [GAP Analysis v1.3](gap-analysis-v1.3.md), đối chiếu các điểm cần thiết với source BE/FE/Android và migrations; [continuity.md](../continuity.md) là nhật ký tiến độ, không thay thế bằng chứng code. Các mô tả cũ trong GAP/continuity được đọc theo thời điểm ghi nhận; proposal quyết định phạm vi đích.
> **Nguồn chuẩn ownership Backend Phase 1:** [Backend Task Assignment v1.3](backend-task-assignment-v1.3.md). **Assigned:** BE1 — Hoàng Quốc Hùng (ingredient/nutrient/recipe/nutrition); BE2 — Nguyễn Duy Nam (cutoff/automatic settlement/acknowledgement); BE3 — Châu Quốc Bảo (centralized audit/IAuditSource/RBAC verification). Phân công chỉ áp dụng cho đầu ra Phase 1 được duyệt; **Assigned không có nghĩa Completed**, không xác nhận ai đã viết toàn bộ code hiện hữu. G4–G7 và nghiệm thu tổng thể G8 vẫn **Pending Team Assignment Review**; mapping chi tiết ở mục 5.3.

## 1. Điểm xuất phát

- Mốc lịch sử được roadmap cũ ghi nhận trước phần điều chỉnh: `cf1bdfd`, nhánh `main`. Giữ để tra cứu; không dùng mốc này làm trạng thái HEAD hoặc suy ra công việc đã merge hiện nay.
- Đã có đăng nhập email/SĐT, 4 role, quản lý tài khoản/phạm vi, Admin reset mật khẩu có audit.
- Đã có mã trẻ, ghi danh/chuyển lớp/ngừng học/ghi danh lại theo ngày, giữ liên kết phụ huynh; tìm kiếm/phân trang; chỉnh sửa bằng modal.
- Đã có báo/sửa/hủy không ăn, tạo phiên ăn, xem số suất theo Enrollment. **Legacy hiện còn trong source:** Admin gọi chốt thủ công từ **07:30 UTC+7 chung cho các bữa**; đây không phải quy tắc đích v1.3 và chưa đạt FR-03/04 mới.
- Đã triển khai ngoại lệ trước chốt: có/vắng/khôi phục mặc định, nguồn quyết định, lịch sử actor và bảo vệ phiên bản nguồn.
- Parent đăng ký không ăn trong năm học đã thiết lập (kể cả vẫn đi học), chọn tuần/tháng/đến hết năm học, sửa khoảng bằng modal hoặc hủy; giữ lịch sử và Enrollment. Không dùng trạng thái không ăn để suy ra vắng học.
- Năm học dùng chung có cấu hình mốc ngày và xem trước năm tiếp theo. Lịch vận hành có lịch tuần/ngày nghỉ/học bù, preview/tạo hàng loạt, hủy/khôi phục phiên và audit; cutoff và một số khóa ở mức ngày vẫn cần chuyển đổi.
- Import XLSX đã có trên BE/web/Android: Admin chọn lớp/ngày bắt đầu, preview/lỗi từng dòng, chống trùng, xác nhận trong transaction, tự cấp mã và ghi danh; có lịch sử import. Không nhập lớp từ file hoặc tự tạo/ghép Parent từ SĐT. Đây là chức năng hiện hữu, không còn là hạng mục chờ bắt đầu.
- Parent đã tự đăng ký qua OTP WhatsApp sandbox và gửi yêu cầu liên kết; Admin/Teacher duyệt/từ chối/thu hồi có revision và lịch sử. Liên kết trực tiếp và thay phạm vi tài khoản còn cần thống nhất audit và kiểm tra lớp theo Enrollment hiệu lực.
- Điều chỉnh suất đã có phiếu nhiều trẻ hoặc số lượng riêng bếp, Admin duyệt/từ chối, snapshot theo lớp có phiên bản và nguồn; bếp xem bản áp dụng/chênh lệch. HEAD hiện chưa có xác nhận đã nhận đúng phiên bản của bếp; phần acknowledgement trên nhánh C cần kiểm tra/tích hợp như ghi nhận Git dưới đây.
- BE nguyên liệu/phiên bản, công thức/phiên bản và kcal/protein đã có. Chưa đủ P/L/C, nguồn tham chiếu, đối chiếu nhóm tuổi và bảo toàn bản tính đã công bố; FE/Android chưa có workflow quản lý dinh dưỡng/thực đơn.
- `MenuDish`, `MealEvidence`, `ReportSnapshot` và API đọc hồ sơ/lineage mới là nền tảng. Chưa có workflow menu duyệt/công bố, dự tính nguyên liệu, món phục vụ/đổi món, upload/duyệt ảnh, Parent xem bản công bố hoặc báo cáo tái lập.
- Android đã có auth/secure storage và giao diện các luồng vận hành hiện có cho các role; không chỉ là scaffold đăng nhập. Chưa có chụp ảnh, lưu file/metadata bền vững, queue và sync offline. Mất mạng khi khôi phục auth không phải FR-17.

**Bằng chứng kiểm thử và giới hạn:** roadmap cũ từng ghi 45 test SQLite + 8 test PostgreSQL, 5 test FE, build và một số kiểm tra modal/390 px; giữ như ghi nhận lịch sử, không phải kết quả hiện tại. Lượt GAP Analysis trước chạy Backend được 167 pass, 29 skip, 0 fail sau khi xử lý giới hạn Windows Event Log; các ca PostgreSQL/concurrency và baseline opt-in chưa chạy. Không chạy lại bộ test trong lần chỉnh tài liệu này. Chưa có bằng chứng nghiệm thu v1.3, FE/Android thật hoặc đạt mục tiêu hiệu năng. Có mã E2E Android hồ sơ trẻ, chưa đồng nghĩa đã chạy trên thiết bị.

**Đối chiếu trạng thái với phân công đã duyệt:** refs cục bộ hiện vẫn có H = `bao@4005331` (trùng `origin/main` đang lưu) và C = `origin/cuong`/`origin/nam` tại `b9b05ac`. BE1 có code dinh dưỡng đã tích hợp nhưng còn các GAP v1.3; không coi toàn bộ schema/refactor có sẵn là công việc mới của BE1. BE2 có acknowledgement trên C qua các commit `76ad8bc`, `51673fc`, `f57f616`, `b9b05ac`, chưa nằm trong H; code tích hợp có đóng góp từ nhiều tác giả. Chưa tìm thấy `IAuditSource`/Audit API tập trung của BE3 tại H hoặc trong các ref đã audit. Lần cập nhật ownership này chỉ đọc refs/source hiện có, không fetch/checkout/merge, không chạy test C hoặc migration; chưa có bằng chứng mới để đánh dấu hoàn tất. Vì vậy mô tả “chưa có acknowledgement” của GAP/continuity chỉ áp dụng cho baseline H, không phải mọi nhánh. BE2 phải kiểm tra lại merge/test/migration mới nhất trước khi làm tiếp.

Điểm kiểm tra source chính: `SchoolTime`, `WorkflowService`, `MealCalendarService`, `PortionAmendmentService`, các service Student/Parent/Account, `IngredientService`/`RecipeService`/`NutritionService`; API đọc Meals/Reports, entities và EF configurations; các trang workflow/meal của FE; operation/route/form nghiệp vụ, dependencies và auth của Android. Đối chiếu thêm [kiến trúc Android](../mobile/docs/architecture.md), [web parity](../mobile/docs/web-parity.md) và [hướng dẫn Android](../mobile/README.md).

Giữ các chức năng nền; chỉ sửa phần lệch baseline hoặc lỗi có bằng chứng. Không suy ra trạng thái push/merge từ lời mô tả “đợt push này” trong roadmap cũ. Checklist thủ công/secret giữ local; README gốc hiện để trống theo quyết định trước. Không bắt buộc Docker.

## 2. Thứ tự và tiêu chí hoàn thành

| Ưu tiên | Giai đoạn | Kết quả bàn giao | Dependency / điều kiện tích hợp |
| --- | --- | --- | --- |
| P0 | G0 — Quyết định nghiệp vụ, contract và nền truy cập | Chốt các điểm chuyển đổi; sửa GAP scope/audit nền; kế hoạch kiểm thử/migration. | Source hiện có; xác nhận của nhóm/trường ở từng quyết định cần thiết. |
| P0 | G1 — Automatic cutoff và bản chốt | Sáng 21:00 D−1, trưa/xế 07:00 D; job tự chốt, xử lý trễ/retry; giữ legacy. | G0: loại bữa, hiệu lực chuyển đổi, Enrollment và contract phiên. |
| P1 | G2 — Điều chỉnh và xác nhận bếp | Hồi quy điều chỉnh dưới cutoff mới; bếp xác nhận từng bản áp dụng. | G1 và contract snapshot/version từ G0. |
| P1 | G3 — Nguồn và tính dinh dưỡng | kcal/P-L-C, đơn vị/gross-edible, phiên bản, khoảng tuổi và bộ ca độc lập. | G0: nguồn, nhóm tuổi, rounding và contract đầu vào. |
| P1 | G4 — Thực đơn và Parent xem menu | Soạn/gửi duyệt/công bố; bản công bố bất biến và dinh dưỡng theo tuổi. | G3; contract phiên/ngày phục vụ của G1. |
| P1 | G5 — Dự tính nguyên liệu | Tính/xuất khối lượng theo menu/recipe và suất hiện hành; truy nguồn. | G2 + G4; không cộng các settlement lịch sử. |
| P1 | G6 — Món phục vụ, ảnh offline và Parent | Ghi phục vụ/đổi món; Android capture/queue/sync; duyệt và xem ảnh có quyền. | G1 + G3 + G4; chốt contract serving/evidence trước tích hợp Android. |
| P1 | G7 — Báo cáo có truy vết | Tạo/duyệt/xuất; drill-down; As-Approved/Recomputed và chênh lệch. | G2 + G3 + G4 + G6; dữ liệu nguồn và snapshot đủ tái lập. |
| P2 | G8 — Nghiệm thu và bàn giao | Demo end-to-end, kiểm thử NFR, hướng dẫn triển khai/backup/restore. | Các giai đoạn Must-have có bằng chứng tích hợp. |

Ưu tiên là thứ tự xử lý rủi ro, không phải ngày/tuần hoặc phân công nhân sự. G3 có thể chuẩn bị sau khi phần quyết định liên quan ở G0 được chốt, không cần chờ toàn bộ G2; G5 và G6 không phụ thuộc nhau. G7 có thể thiết kế contract lineage sớm nhưng chỉ nghiệm thu khi các nguồn thực đã tích hợp. Audit, quyền và test được làm ngay trong mỗi giai đoạn, không dồn đến G7/G8. P2 không làm các tiêu chí nghiệm thu Must-have trở thành tùy chọn.

FR-22/23/24 là yêu cầu xuyên suốt mọi module liên quan; FR cụ thể được ghi trong từng giai đoạn. Không đặt ngày hoàn thành hoặc số tuần còn lại khi chưa có dữ liệu tiến độ đáng tin cậy. Chuyển chặng theo Acceptance Criteria và minh chứng, không chỉ build hoặc số test pass.

**Phối hợp Backend Phase 1:** G0 có BE3 phụ trách audit/RBAC và audit contract, BE1/BE2 cung cấp metadata của module được giao; G1/G2 do BE2 phụ trách phần Backend Phase 1; G3 do BE1 phụ trách. BE3 tích hợp các nguồn vào audit tập trung khi contract/metadata sẵn sàng. Không cần đợi BE1/BE2 xong toàn bộ để BE3 làm adapter nguồn hiện hữu. G8 ghi nhận kiểm thử/tích hợp Phase 1 theo từng owner; không tự giao nghiệm thu toàn dự án cho ba BE. Chi tiết API, business rules, test và ranh giới công việc theo [tài liệu phân công](backend-task-assignment-v1.3.md), không chép lại thành đặc tả thứ hai trong roadmap.

## 3. Ngoại lệ trước chốt đã có — giữ làm nền hồi quy

### Quy tắc đã triển khai

- Admin được thao tác toàn trường; Teacher chỉ trong lớp được phân công, kiểm tra Enrollment tại ngày ăn.
- Danh sách hiển thị cả trẻ dự kiến ăn và trẻ bị loại vì báo vắng/ngoại lệ, để chọn được thao tác khôi phục.
- Có ba lựa chọn: **dự kiến có suất**, **dự kiến không có suất**, **khôi phục mặc định**. Đây không phải xác nhận điểm danh thực tế.
- Khôi phục mặc định = không dùng override của giáo viên; tiếp tục áp dụng Enrollment và báo vắng Parent. Trẻ vẫn có báo vắng không tự có suất sau hoàn tác.
- Mỗi thay đổi cần lý do, actor, thời điểm và liên kết sự kiện trước; ghi thêm sự kiện, không xóa lịch sử cũ.
- Chỉ được thay đổi trước CutoffAt và khi chưa chốt. Sau cut-off, kể cả chưa bấm chốt, không sửa số suất dự kiến; chuyển sang quy trình điều chỉnh ở chặng sau.

### Phần công việc đã có trong source, không mở lại từ đầu

1. BE đã lưu metadata actor và hành động khôi phục của ngoại lệ; bản ghi cũ thiếu actor không được dựng thông tin giả.
2. API đọc có danh sách trẻ, nguồn loại khỏi suất và lịch sử ngoại lệ, cùng phạm vi truy cập với API ghi.
3. Kết quả tính dùng thứ tự mặc định → báo không ăn → override hiệu lực; khôi phục trả về mặc định + báo không ăn.
4. Phiên bản nguồn và transaction bảo vệ thao tác đồng thời trên cùng trẻ/phiên; yêu cầu dùng bản cũ trả 409.
5. FE có modal Admin/Teacher cho có/vắng/khôi phục, lý do và lịch sử; cập nhật suất và toast kết quả.
6. Test đã dùng clock có thể điều khiển để kiểm tra biên cutoff cũ. G1 bổ sung hai mốc mới mà không sửa giờ hệ thống hoặc DB người dùng.

### Ca nghiệm thu hồi quy bắt buộc

Giữ các ca dưới đây khi chuyển cutoff ở G1. Cutoff dùng để nghiệm thu v1.3 phải theo từng bữa; kết quả test 07:30 cũ chỉ là bằng chứng legacy.

| Ca | Kết quả |
| --- | --- |
| Trẻ mặc định có suất → ghi không có suất | Giảm một suất đúng lớp. |
| Parent đã báo vắng → Teacher ghi có suất | Tăng một suất, hiển thị nguồn override. |
| Khôi phục override khi Parent vẫn báo vắng | Trẻ tiếp tục không có suất. |
| Khôi phục override khi không có báo vắng | Trở về mặc định có suất. |
| Trẻ chuyển lớp tương lai | Ngoại lệ kiểm tra lớp theo ngày ăn, không theo pointer ClassId mới nhất. |
| Teacher khác lớp/Parent/Kitchen ghi ngoại lệ | Bị từ chối. |
| Ghi đúng/sau cut-off hoặc phiên đã chốt | Bị từ chối; bản chốt giữ nguyên. |
| Hai thao tác cùng phiên bản nguồn | Một thao tác thắng; thao tác cũ nhận conflict, lịch sử nhất quán. |

## 4. Roadmap triển khai tiếp từ trạng thái hiện tại

Các công việc và deliverables ở phần này là **kế hoạch cần thực hiện**, không phải thông báo đã sửa code, API hoặc migration. Mỗi giai đoạn giữ phần nền đã có và chỉ bổ sung/sửa GAP tương ứng.

### G0 — Quyết định chuyển đổi, contract và nền truy cập (P0)

- **Mục tiêu/phạm vi:** chốt các điểm nghiệp vụ còn mở, ranh giới tích hợp và xử lý các GAP truy cập/audit nền trước khi các module mới phụ thuộc vào chúng.
- **FR liên quan:** FR-01/02/10/18/22/23; chuẩn bị contract cho FR-03/11/12/17/24. Tham chiếu GAP G04/G05 và các mâu thuẫn tài liệu.
- **Đã triển khai:** bốn role, tài khoản/phạm vi, Enrollment, import XLSX, yêu cầu liên kết/duyệt/thu hồi, OTP sandbox; có revision và thu hồi JWT. Giữ các luồng này, không làm lại từ đầu.
- **Còn thiếu:** thống nhất audit giữa yêu cầu liên kết, liên kết trực tiếp và sửa phạm vi tài khoản; kiểm tra Teacher theo Enrollment hiệu lực thay cho pointer lớp; chính sách lịch sử phân công và quyết định chuyển đổi hai cutoff.
- **Công việc:** xác nhận các điểm ở mục 5.1; thống nhất định danh loại bữa/ngày phục vụ, snapshot/version, nguồn dinh dưỡng và contract serving/evidence/report-critical. Lập đánh giá tác động API/schema/migration và bộ dữ liệu hồi quy; bổ sung audit các đường cấp/gỡ quyền còn thiếu. Rà nhãn “kiểm thực” và grant thanh tra ngoài baseline để nhóm quyết định cách giữ tương thích, không tự mở rộng scope hoặc xóa dữ liệu.
- **Dependency:** source hiện có và quyết định nghiệp vụ của nhóm/trường/GVHD ở từng phần. BE3 thống nhất `IAuditSource`/normalized audit contract với BE1/BE2 trước khi tích hợp; BE1/BE2 xác nhận metadata/version có thể cung cấp trước thay đổi schema. Shared contract và quyết định nghiệp vụ vẫn do nhóm cùng xác nhận, không giao BE3 tự quyết vì phụ trách audit. Phần chưa được giao tiếp tục review riêng theo mục 5.3, không suy ownership từ tên module/WP.
- **Acceptance Criteria:** mỗi điểm cần xác nhận có trạng thái và căn cứ, không tự hardcode quy tắc chưa chốt; có bản đánh giá bảo toàn dữ liệu legacy/contract. Teacher khác lớp hoặc bị thay đổi quyền không được truy cập sai; duyệt/thu hồi và thay phạm vi giữ lịch sử, thu hồi quyền hiệu lực. Hồi quy import, tài khoản, nhiều guardian và chuyển lớp tương lai không làm mất dữ liệu.
- **Deliverables:** log quyết định/contract và kế hoạch kiểm thử chuyển đổi; phần sửa scope/audit nền kèm test và hướng dẫn tương thích khi triển khai. Các tài liệu khác chỉ cập nhật khi được giao riêng.
- **Ownership Backend:** **Assigned — BE3, Châu Quốc Bảo:** audit nền/tập trung, `IAuditSource`, normalized contract, adapters, Audit API/timeline/CSV export, sensitive-data redaction, RBAC verification và review error contract trong Phase 1. **BE1 — Hoàng Quốc Hùng / BE2 — Nguyễn Duy Nam** phối hợp ghi metadata của module dinh dưỡng/suất ăn và bảo vệ API mình triển khai. Sửa service tài khoản/liên kết hiện hữu ngoài đầu ra Phase 1 vẫn theo owner module gốc và nhóm xác nhận người review; không mặc nhiên giao BE3 sửa toàn bộ auth/scope. Chi tiết theo tài liệu phân công §2–5.

### G1 — Automatic cutoff, lịch và bản chốt theo bữa (P0)

- **Mục tiêu/phạm vi:** thay luồng 07:30 thủ công bằng tự khóa/chốt theo ngày phục vụ D: **sáng 21:00 D−1 nếu có phục vụ; trưa/xế 07:00 D**, theo `Asia/Ho_Chi_Minh` (UTC+7). Đây là baseline thử nghiệm cần trường/GVHD xác nhận, không phải giờ chốt theo luật chung.
- **FR liên quan:** FR-01/03/04/07/08/19, cùng FR-22/23/24. Khép GAP G01.
- **Đã triển khai:** lịch tuần/ngày nghỉ/học bù, preview/tạo hàng loạt; hủy/khôi phục giữ ID/lý do; revision/preview token; quyết định suất lấy báo nhận trước cutoff; chốt thủ công có snapshot và chống ghi trùng. Tạo phiên hiện không tự chốt.
- **Còn thiếu:** chính sách cutoff theo loại bữa, tác vụ tự chốt/catch-up sau restart, bảo vệ trường hợp sáng đã khóa nhưng trưa/xế còn mở; chuyển đổi Enrollment/lịch/FE/Android và bảo toàn legacy.
- **Công việc:** tách cutoff theo ngày phục vụ + loại bữa, thống nhất ánh xạ tên bữa cũ; tự chốt đúng phiên và phục hồi tác vụ quá hạn với idempotency/concurrency guard. Dùng dữ liệu hợp lệ server đã ghi nhận **trước** cutoff dù job chạy trễ; xử lý phiên hợp lệ không có trẻ để tránh kẹt tác vụ. Áp chính sách ghi danh đã xác nhận; rà lịch, báo Parent, ngoại lệ, API/DTO, UI, test và demo. Phân biệt lịch chưa cấu hình với ngày nghỉ; sửa phiên còn mở không được sửa phiên đã khóa.
- **Dependency:** G0 chốt loại bữa, chính sách ghi danh/chuyển lớp và thời điểm áp dụng cho các phiên chưa chốt. BE2 thống nhất ID/version/cutoff và metadata settlement với BE3 để đọc qua audit-source adapter, đồng thời cung cấp contract snapshot cho phần tích hợp sau. Không áp lại cutoff mới lên bản chốt lịch sử; thiết kế migration phải giữ cutoff, số liệu và nguồn thực tế, không dựng lịch sử giả.
- **Acceptance Criteria:** không cần Admin bấm đúng giờ; bằng/sau cutoff là muộn. Một báo cả ngày lúc 22:00 chỉ đổi trưa/xế chưa khóa, giữ sáng. Job chạy trễ/retry/nhiều instance chỉ tạo một bộ bản chốt gốc cho phiên; không chốt phiên hủy/ngày không phục vụ. Test `20:59:59/21:00:00`, `06:59:59/07:00:00`, qua ngày D−1/D, không có bữa sáng, client lệch giờ, sửa/hủy báo, Enrollment và lock wait. Snapshot legacy không đổi và thiếu nguồn được ghi đúng là thiếu.
- **Deliverables:** chính sách cutoff và tác vụ tự chốt, kế hoạch/migration tương thích đã kiểm thử trên DB thử, API/FE/Android được đồng bộ, test PostgreSQL và demo 30 suất sáng → 29 suất trưa/xế. Nếu giữ endpoint chốt cũ, nó chỉ là cơ chế phục hồi có quyền, không thay thế automatic workflow.
- **Ownership Backend:** **Assigned — BE2, Nguyễn Duy Nam:** meal-specific cutoff, automatic settlement, snapshot/version compatibility và audit metadata của module suất ăn trong Phase 1, gồm tác động lịch/Enrollment thuộc chính sách cutoff đã chốt. BE3 phối hợp adapter, RBAC và error-contract verification; quyết định nghiệp vụ/shared contract cần nhóm xác nhận. Không giao lại toàn bộ module Enrollment hoặc công việc FE/Android cho BE2. Chi tiết theo tài liệu phân công §3.

Giữ kịch bản nghiệm thu lịch đã có: cấu hình năm học → lịch tuần → preview một tuần → tạo → kiểm tra suất → ngày nghỉ/mở lại → lịch sử. Bổ sung hai cutoff mới và phân biệt chưa cấu hình/ngày nghỉ. Các ca PostgreSQL từng được roadmap cũ ghi nhận phải chạy lại khi nghiệp vụ thay đổi; không coi là đã kiểm chứng v1.3.

### G2 — Điều chỉnh sau chốt và xác nhận bản suất của bếp (P1)

- **Mục tiêu/phạm vi:** giữ quy trình điều chỉnh có phiên bản dưới cutoff mới và hoàn thiện gửi/nhận bản áp dụng.
- **FR liên quan:** FR-04/09/14/23/24. Khép GAP G02 và hồi quy phần điều chỉnh của G01.
- **Đã triển khai:** Teacher/Admin đề nghị nhiều trẻ hoặc số lượng riêng bếp, có lý do/bản nguồn; Admin duyệt/từ chối; duyệt tạo snapshot mới theo lớp và liên kết bản trước. Nguồn lỗi thời không tự rebase; có nguồn Enrollment/báo Parent/ngoại lệ và nhãn thiếu nguồn legacy. Bếp xem bản gốc, hiện hành, tăng/giảm và lịch sử; tổng lấy bản mới nhất. Hành vi Admin được duyệt yêu cầu tự gửi đã có, v1.3 chưa yêu cầu thay đổi.
- **Còn thiếu:** xác nhận đã nhận đúng settlement/version, người/thời điểm nhận và trạng thái cần xác nhận lại khi đổi bản; nghiệm thu điều chỉnh sau automatic cutoff.
- **Công việc:** hồi quy luồng gửi/duyệt/từ chối với G1; thêm xác nhận nhận theo định danh snapshot/version cụ thể và hiển thị trạng thái/chênh lệch trên web/Android. Giữ audit và guard bản nguồn; không đồng nhất “đã mở màn hình” với “đã xác nhận nhận”, không bắt buộc thêm kênh nhắn tin.
- **Dependency:** G1 có bản chốt tự động và contract snapshot/version đã review; BE2 xác minh code acknowledgement trên C, trạng thái merge và test/migration trước khi sửa hoặc viết thêm. BE2 cung cấp membership settlement/version và metadata của lần nhận cho BE3 tích hợp audit; BE3 kiểm tra quyền/error contract xuyên endpoint Phase 1. Xác nhận nhận của bếp không được chặn Admin duyệt điều chỉnh hoặc làm thay đổi số suất.
- **Acceptance Criteria:** duyệt tạo đúng một version mới, từ chối không đổi suất; hai yêu cầu cùng nguồn không cùng được áp dụng, không giảm âm/trùng suất trẻ giữa lớp. Phiếu số lượng giữ roster/trạng thái trẻ. Sau duyệt, trưa v1=29 còn nguyên, v2=28 là bản áp dụng; bếp thấy chênh lệch và xác nhận v2 riêng, xác nhận v1 không được hiểu là đã nhận v2. Gửi lại xác nhận không sinh dữ liệu trùng. Báo muộn không tự đổi bản chốt, điểm danh hay hoàn phí.
- **Deliverables:** phần nhận bản suất trong BE/FE/Android, audit receipt/version, test scope/stale/duplicate/concurrency và demo điều chỉnh nối tiếp G1.
- **Ownership Backend:** **Assigned — BE2, Nguyễn Duy Nam:** Kitchen Acknowledgement, tương thích snapshot/version, audit metadata và regression settlement/amendment dưới cutoff mới. Giữ nghiệp vụ amendment đã có, không nhận là đã viết lại từ đầu. BE3 phối hợp audit-source adapter và ma trận RBAC; phần UI do phân công FE/Android riêng. Chi tiết theo tài liệu phân công §3.

Giữ kịch bản đối chiếu đã có: phiên đã chốt → Teacher gửi thêm/giảm → Admin xem nguồn/duyệt → Kitchen đối chiếu gốc/mới → yêu cầu nguồn cũ nhận conflict. Thêm xác nhận nhận bản mới và hồi quy legacy thiếu roster/nguồn; không suy diễn dữ liệu trẻ còn thiếu.

### G3 — Nguồn dinh dưỡng, công thức và nhóm tuổi (P1)

- **Mục tiêu/phạm vi:** hoàn thiện tính **kcal và protein/lipid/carbohydrate (P/L/C)** deterministic, có nguồn/đơn vị/hệ số và đối chiếu khoảng tham chiếu tuổi mầm non.
- **FR liên quan:** FR-11/12/23/24. Khép GAP G03.
- **Đã triển khai:** nguyên liệu và version có thời điểm hiệu lực/ghi nhận, edible fraction; recipe version và định lượng; calculator kcal/protein có rounding cố định, response đầu vào và lỗi thiếu dữ liệu. API đã dùng ADMIN/KITCHEN_STAFF.
- **Còn thiếu:** lipid/carbohydrate, nguồn tham chiếu/version nguồn, nhóm tuổi/khoảng đối chiếu; phân biệt gross/edible và chuyển đổi đơn vị; hiệu lực công thức và lưu rule/đầu vào của bản tính cố định. Calculator hiện resolve lại ingredient version theo `validAt/knownAt`; chưa thể dùng như bảo đảm bất biến bản công bố. Đầu vào `ml` chưa có mô hình chuyển sang gram tương ứng.
- **Công việc:** mở rộng model/API và giao diện nhập/xem nguồn/công thức theo nhu cầu MVP; chốt đơn vị, cách nhập khối lượng và hệ số có căn cứ, không ngầm coi ml=gram. Lưu nguồn/phiên bản và rule rounding; tách tính từ đầu vào cố định khỏi recompute theo nguồn mới. Bổ sung API đọc recipe/version phục vụ UI và bộ ca tính độc lập. Không thêm mô hình yield/hao hụt chế biến chi tiết khi chưa được xác nhận.
- **Dependency:** phần quyết định nguồn, nhóm tuổi, đơn vị và rounding ở G0; BE1 thống nhất metadata actor/time/reason/source/version với BE3 trước thay đổi schema, BE3 sở hữu adapter chuẩn hóa đọc nguồn này. BE1 cung cấp contract định lượng/version cho G4/G5/G7 nhưng không vì vậy nhận ownership các module đó. Có thể chuẩn bị G3 sau các quyết định liên quan mà không chờ hoàn tất G2.
- **Acceptance Criteria:** đúng ca gram/100 g phần ăn được, gross so với edible không chuyển đổi hai lần, đổi đơn vị có nguồn, rounding theo rule đã ghi; thiếu nguồn không giả số 0. Kết quả so sánh tuổi truy được khoảng và nguồn được trường/GVHD chấp thuận; không áp trực tiếp ngưỡng học sinh phổ thông cho mầm non. Cập nhật ingredient/recipe không đổi đầu vào/kết quả cố định cũ; recompute thể hiện version mới riêng và bộ ca độc lập khớp kết quả.
- **Deliverables:** model/API/luồng UI dinh dưỡng, bộ nguồn thử có tham chiếu, khoảng tuổi và rule được xác nhận, calculation/version tests cùng contract đầu vào cho menu/report.
- **Ownership Backend:** **Assigned — BE1, Hoàng Quốc Hùng:** Ingredient, Nutrient Data, Recipe Versioning, Nutrition Calculation kcal/P/L/C, nutritional sources, age-group references và audit metadata của module dinh dưỡng. BE3 cung cấp audit contract, tích hợp adapter và kiểm thử RBAC/error contract xuyên module; BE1 vẫn bảo vệ/kiểm thử API mình triển khai. Chi tiết theo tài liệu phân công §2.

### G4 — Soạn, duyệt/công bố thực đơn và Parent xem menu (P1)

- **Mục tiêu/phạm vi:** Kitchen lập menu ngày/tuần theo bữa, gắn recipe version/nhóm tuổi; Admin duyệt/công bố; Parent xem nội dung được công bố theo con liên kết.
- **FR liên quan:** FR-05 (thực đơn)/13/20 (menu và dinh dưỡng), FR-22/23/24.
- **Đã triển khai:** `MenuDish`, liên kết recipe version và `MealDay.PublishedAt`; API/web/Android đọc hồ sơ món nền cho staff. Đây chưa phải workflow duyệt/công bố hoặc quyền đọc menu của Parent.
- **Còn thiếu:** soạn/gửi duyệt, quyết định duyệt, phiên bản công bố bất biến và lý do sửa; UI menu/dinh dưỡng; Parent API/UI có kiểm tra guardian/publication.
- **Công việc:** triển khai luồng bản nháp → gửi duyệt → duyệt/công bố, lưu người/thời điểm/phiên bản; sửa sau công bố tạo bản mới có lý do. Gắn đầu vào dinh dưỡng cố định G3, trình bày đối chiếu tuổi và trạng thái chưa đủ dữ liệu. Tích hợp web và các màn Android liên quan, Parent xem đúng phạm vi. Hạn công bố 16:00 chỉ là ví dụ chưa chốt, không hardcode thành acceptance bắt buộc.
- **Dependency:** G3 cho nguồn/công thức/nhóm tuổi và contract phiên/ngày phục vụ G1; G0 về quyền, hạn công bố nếu áp dụng. Dữ liệu menu gắn phiên không đồng nghĩa phải đợi chốt suất mới được lập menu.
- **Acceptance Criteria:** Kitchen không tự duyệt thay Admin; bản nháp/chưa công bố không lộ cho Parent. Trẻ chưa xác minh/khác guardian không được đọc; thu hồi liên kết mất quyền. Menu ngày/tuần giữ đúng bữa/recipe version và dinh dưỡng; sửa nguồn hoặc menu sau công bố không ghi đè bản cũ, có audit/lý do/version. Không phát sinh thực đơn cá nhân hoặc AI tối ưu.
- **Deliverables:** workflow/API thực đơn và quyền đọc Parent, web và Android liên quan, snapshot/version công bố, integration/negative authorization tests.
- **Ownership Backend:** **Pending Team Assignment Review** — menu workflow/publication, nutrition snapshot tích hợp và Parent read scope.

### G5 — Dự tính và xuất nguyên liệu (P1)

- **Mục tiêu/phạm vi:** Kitchen tính/xuất nguyên liệu dự kiến theo menu, recipe và **số suất đã chốt đang áp dụng**, phục vụ kế hoạch chuẩn bị.
- **FR liên quan:** FR-16/23/24.
- **Đã triển khai:** có recipe/định lượng nền và truy vấn settlement hiện hành; chưa có service/API/UI hoặc luồng xuất dự tính nguyên liệu.
- **Còn thiếu:** tổng hợp khối lượng/đơn vị theo bữa/ngày, nguồn tính và bản xuất/bản tính lại sau điều chỉnh.
- **Công việc:** tính từ recipe version và định lượng G3/G4 nhân số suất hiện hành G2, tổng hợp nguyên liệu theo đơn vị tương thích. Cho phép truy lại món/recipe/version suất; xuất danh sách và giữ cơ sở bản đã lưu. Điều chỉnh suất/menu tạo bản dự tính mới; ghi rounding nếu áp dụng, không sửa bản xuất cũ đã lưu.
- **Dependency:** G2 + G4 với contract định lượng G3. Không dùng tổng cộng các settlement lịch sử; không dùng số suất dự kiến chưa khóa như bản đã chốt.
- **Acceptance Criteria:** bộ ca độc lập khớp khối lượng; mỗi dòng truy về menu/recipe/định lượng và settlement version. Menu hoặc suất đổi cho kết quả mới có cơ sở rõ ràng, bản cũ giữ nguyên; thiếu nguồn/đơn vị không được tính ngầm. Không có purchase order, nhà cung cấp, giá mua hoặc giao dịch kho.
- **Deliverables:** calculation/export API, UI bếp, tệp mẫu dự tính có nguồn tính và test thay đổi menu/suất/đơn vị.
- **Ownership Backend:** **Pending Team Assignment Review** — ingredient estimation, export và tích hợp menu/settlement.

### G6 — Món phục vụ, ảnh Android offline và Parent xem ảnh (P1)

- **Mục tiêu/phạm vi:** ghi món/số suất hoặc định lượng phục vụ, đổi món/lý do; chụp ảnh món/khay ăn offline Android, sync rồi Admin duyệt/công bố và Parent xem trong phạm vi được phép. **FR-17 là Must-have**, không để chung nhóm mở rộng “làm sau”.
- **FR liên quan:** FR-15/17, FR-05 (ảnh)/20 (ảnh), FR-22/23/24.
- **Đã triển khai:** `MealEvidence` có liên kết bữa, `CapturedAt`/`SyncedAt` và API đọc nền; web/Android đọc hồ sơ ngày ăn; Android có secure session và xử lý lỗi mạng. Chưa có nghiệp vụ capture/offline hoặc workflow phục vụ/duyệt ảnh.
- **Còn thiếu:** bản ghi món phục vụ/đổi món và recipe version thực tế; upload/storage có quyền, idempotency, hàng đợi bền vững, duyệt/công bố và Parent đọc ảnh.
- **Công việc:** thêm serving record liên kết menu/phiên/recipe, số suất/định lượng ở mức đã chốt thiết kế; ghi lý do đổi món và lịch sử. Thống nhất contract ảnh/metadata, định danh chống trùng và phản hồi server trước tích hợp Android. Lưu file/metadata/queue cục bộ qua restart, tách theo tài khoản; retry khi có mạng và xác minh lại quyền khi gửi. Lưu thời điểm chụp do thiết bị cung cấp tách khỏi thời điểm server nhận, xử lý lệch giờ. Lưu file/object storage và cấp đọc qua kiểm tra quyền; thêm Admin review/publication và UI Parent. Thay thuật ngữ “kiểm thực” đang dùng sai nghĩa trong các màn liên quan khi được triển khai.
- **Dependency:** G1 + G3 + G4 cho phiên/menu/công thức và G0 về privacy/lưu ảnh. Contract serving/evidence phải được review trước tích hợp client; G6 không cần đợi G5. Thử nghiệm capture/queue có thể chuẩn bị với contract đã thống nhất, nhưng chỉ nghiệm thu sau tích hợp BE thật.
- **Acceptance Criteria:** món thay thế có lý do/recipe nguồn và không sửa menu công bố cũ. Airplane mode → chụp → kill/relaunch vẫn còn ảnh/metadata → có mạng gửi hai lần chỉ có một bản ghi server; upload dở/timeout không báo đã đồng bộ trước server xác nhận. Hai timestamp giữ riêng, sai bữa/quyền hoặc phiên hết hạn không gây gán nhầm/mất queue. Parent không đọc ảnh chưa duyệt/ngoài guardian, kể cả truy cập URL trực tiếp hoặc sau thu hồi quyền. Ảnh mặc định là món ăn, không mặc định chụp trẻ.
- **Deliverables:** serving/evidence API và storage có kiểm tra quyền, Android capture/persistent queue/sync, web duyệt và Parent xem, unit/integration/device tests có minh chứng. Không có workflow sản xuất nội bộ, kiểm thực ba bước hay phiếu lưu mẫu.
- **Ownership Backend:** **Pending Team Assignment Review** — serving/substitution, evidence upload/storage/sync contract, review/publication và quyền đọc; không tự gán BE của phần Android cho bất kỳ thành viên nào.

### G7 — Báo cáo số suất/dinh dưỡng có truy vết và tái lập (P1)

- **Mục tiêu/phạm vi:** Admin tạo/xem/duyệt/xuất báo cáo theo kỳ; drill-down đến nguồn; giữ **As-Approved** và **Recomputed** riêng cùng nguyên nhân chênh lệch.
- **FR liên quan:** FR-06/23/24, FR-22 cho quyền đọc/duyệt/xuất.
- **Đã triển khai:** `ReportSnapshot`, một số trường giá trị và endpoint đọc `SourceJson`; snapshot suất/điều chỉnh đã có. Không coi scaffold này là báo cáo đã nghiệm thu hoặc đủ data lineage.
- **Còn thiếu:** tổng hợp kỳ, workflow duyệt/xuất, snapshot đầu vào/rule, drill-down và cơ chế tái lập/đối chiếu.
- **Công việc:** chốt mẫu/kỳ/định dạng xuất và người duyệt; liên kết số liệu với bản suất/điều chỉnh, món phục vụ, recipe, khối lượng, đơn vị/hệ số, nguồn và rule/version dinh dưỡng. Lưu đủ đầu vào/giá trị/rule của bản duyệt; dùng valid time và recorded time tại dữ liệu report-critical cần thiết. Thêm tính lại riêng, so sánh chênh lệch và UI drill-down; bảo vệ bản duyệt khỏi sửa/xóa. Không bắt toàn domain event sourcing/WORM.
- **Dependency:** G2 + G3 + G4 + G6 và contract lineage G0; mẫu báo cáo cần trường/nhóm xác nhận. G5 chỉ là dependency nếu báo cáo tích hợp nội dung dự tính nguyên liệu đã chốt, không mặc định chặn báo cáo suất/dinh dưỡng.
- **Acceptance Criteria:** tổng suất đúng bản đang xét, không cộng bản cũ; phân biệt dự kiến/chốt/điều chỉnh/phục vụ. Từ một chỉ số truy được chuỗi nguồn đủ tái lập. Sau sửa hợp lệ ingredient/recipe/suất/món, bản duyệt tái lập đúng giá trị cũ; recompute không ghi đè và giải thích được nguồn chênh lệch. Legacy thiếu nguồn được thể hiện trung thực, không tạo nguồn giả; chỉ quyền hợp lệ được duyệt/xuất.
- **Deliverables:** report/approval/export/lineage/recompute API và UI web, mẫu xuất đã xác nhận, reproducibility/integration tests và demo sửa nguồn sau duyệt. Không có báo cáo thu phí/doanh thu hoặc gửi trực tiếp cổng cơ quan quản lý.
- **Ownership Backend:** **Pending Team Assignment Review** — report aggregation, snapshot/approval, lineage, recompute/comparison và export.

BE3 phụ trách Audit Logs/Timeline/CSV Export của Phase 1 ở G0, **không** sở hữu Reporting G7. BE1/BE2 chỉ cung cấp version/source metadata của module mình; việc tái sử dụng các dữ liệu này không tự giao menu, estimation, serving/offline hoặc report cho bất kỳ BE nào.

### G8 — Nghiệm thu tích hợp và bàn giao MVP (P2)

- **Mục tiêu/phạm vi:** chứng minh các Must-have chạy cùng nhau trên BE/web/Android và hoàn thiện bàn giao có thể tái lập. Đây là cổng nghiệm thu, không phải bắt đầu viết toàn bộ test ở cuối.
- **FR liên quan:** FR-01–20, FR-22–24; NFR-01–06. FR-21 Optional không phải điều kiện mặc định.
- **Đã triển khai:** có xUnit/Vitest, Flutter unit/widget/contract tests, một E2E hồ sơ trẻ, harness baseline và scripts cấu hình local. Source có test PostgreSQL nhưng kết quả skip ở GAP chưa chứng minh chúng đã pass; build không thay thế nghiệm thu thiết bị.
- **Còn thiếu:** bằng chứng end-to-end v1.3, chạy PostgreSQL/migration, offline trên Android thật, benchmark mục tiêu, bộ dữ liệu demo và tài liệu triển khai/backup/restore đầy đủ; Android release cần cấu hình ký phù hợp.
- **Công việc:** tích hợp kịch bản proposal §8; chạy test trọng yếu và migration trên DB thử biệt lập, kiểm tra nâng cấp từ legacy, sao lưu/phục hồi. Nghiệm thu responsive web ở 320/390/768 px và desktop, Android theo role, lỗi mạng/quyền/phiên và OTP trên số thử được phép. Chốt dataset/môi trường/thiết bị và đo hiệu năng; bổ sung tài liệu bàn giao trong phạm vi được giao. Giữ secret ngoài Git, API HTTPS và keystore riêng khi phát hành; không tự gỡ sandbox tester hoặc mở kênh thông báo mới.
- **Dependency:** các deliverables Must-have G0–G7 đã tích hợp, môi trường PostgreSQL/Android và dữ liệu/tiêu chí trường chấp nhận. Trong Phase 1, BE1/BE2 bàn giao code/contract/metadata, kết quả test và migration module của mình; BE3 tích hợp adapters và chạy ma trận RBAC/redaction/error-contract xuyên các endpoint Phase 1 trên code đã tích hợp. Các ca skip/chưa chạy phải được ghi rõ và xử lý trước khi tuyên bố đạt tiêu chí tương ứng; không suy từ Phase 1 xong thành G8 tổng thể đã đạt.
- **Acceptance Criteria:** demo liền mạch import/liên kết → báo/ngoại lệ → tự chốt → điều chỉnh/bếp xác nhận → menu/P-L-C/tuổi → dự tính → đổi món/ảnh offline/duyệt/Parent → báo cáo tái lập. Negative tests đúng role/lớp/guardian/publication; dữ liệu legacy không đổi. Mục tiêu danh sách lớp **≤5 giây**, báo cáo tháng **≤10 giây** trên dữ liệu tương đương **3 năm học** có mô tả cấu hình/kịch bản/kết quả; nếu điều chỉnh tiêu chí phải có xác nhận. Restore từ backup và chạy theo hướng dẫn trên môi trường thử thành công, không coi số test/build là bằng chứng duy nhất.
- **Deliverables:** bộ test và kết quả nghiệm thu có ghi môi trường/giới hạn, demo dataset, Android build đã kiểm chứng và hướng dẫn cấu hình/triển khai/migration/backup/restore; continuity/roadmap cập nhật theo bằng chứng khi được giao cập nhật tài liệu.
- **Ownership Backend:** **Assigned trong phạm vi Phase 1:** BE1 — Hoàng Quốc Hùng kiểm thử/tích hợp module dinh dưỡng; BE2 — Nguyễn Duy Nam kiểm thử/tích hợp cutoff, settlement và acknowledgement; BE3 — Châu Quốc Bảo kiểm thử/tích hợp audit, redaction/CSV export, xác minh RBAC và error contract xuyên endpoint Phase 1. **Pending Team Assignment Review:** nghiệm thu tổng thể, demo/benchmark/thiết bị/tài liệu bàn giao liên nhóm và mọi module chưa giao. Không tự giao toàn bộ G8 cho một BE hoặc cho ba BE thay phân công liên nhóm.

## 5. Các điểm chờ xác nhận, ngoài scope và review ownership

### 5.1. Quyết định cần nhóm/trường/GVHD xác nhận

- Các bữa thực tế, lịch nghỉ/học bù và cách ánh xạ tên bữa legacy; thời điểm chuyển chính sách cho phiên chưa chốt. Giữ baseline 21:00 D−1/07:00 D, không dùng việc chưa xác nhận để quay lại 07:30 thủ công.
- Ghi danh/chuyển lớp/ngừng học khi sáng đã khóa nhưng trưa/xế chưa khóa; quyền lịch sử khi đổi phân công Teacher. Hủy/sửa lịch chuyển lớp tương lai chỉ triển khai theo chính sách được chốt, không tự thêm khả năng sửa lịch sử đã khóa.
- Nguồn thành phần định lượng, ai nhập/duyệt nguồn, đơn vị/gross-edible, hiệu lực recipe, rounding và khoảng tham chiếu tuổi mầm non. Tham chiếu proposal §4.5; tài liệu phân nhóm thực phẩm không thay bảng kcal, không áp trực tiếp ngưỡng phổ thông cho trẻ mầm non.
- Hạn công bố menu nếu cần áp dụng; mẫu/định dạng xuất báo cáo/dự tính, người duyệt/ký và cách trình bày món thay thế/ảnh. Không mặc định 16:00 là cam kết.
- Thiết bị/môi trường Android, định lượng phục vụ ở mức bữa/lớp, chính sách ảnh không chứa thông tin trẻ và thời hạn lưu ảnh; hành vi queue khi đổi tài khoản/mất quyền cần được review cùng contract.
- Cách giữ tương thích grant thanh tra hiện có và sửa thuật ngữ UI; không tạo role mới hoặc mở module thanh tra vật lý. FR-21 chỉ bổ sung khi nhóm chọn riêng và trường xác nhận quy trình xem xét lưu ý.

### 5.2. Phạm vi không đưa vào roadmap MVP

- Không quản lý nhà cung cấp/chứng chỉ, farm-to-fork/lô hàng, purchase order, giá mua, kho/nhập-xuất hay kiểm thực ba bước/lưu mẫu/quy trình chế biến nội bộ. Dự tính nguyên liệu và ghi nhận món phục vụ không mở rộng thành các nghiệp vụ này.
- Không fee ledger, hoàn phí tự động, thanh toán/đối soát kế toán, báo cáo thu phí/doanh thu. Không suy ra hoàn phí từ báo không ăn hoặc điều chỉnh.
- Không AI sinh/tối ưu menu, IoT, vi chất chi tiết, khẩu phần điều trị, iOS, full offline mọi vai trò, ERP/dashboard nâng cao hoặc tích hợp cổng báo cáo chính phủ.
- FR-21 là Optional; import PDF, mở rộng thông tin import và kênh SMS/email/WhatsApp sản xuất không phải yêu cầu MVP mới mặc định. Duy trì WhatsApp/OTP đã có và nghiệm thu luồng hiện hữu; không tự mở rộng kênh, outbox hoặc bỏ giới hạn thử nghiệm. Import XLSX trẻ vào lớp chọn sẵn đã có, không để lại trong danh sách “chờ triển khai”.

### 5.3. Backend Assignment Review — Phase 1 đã giao và phần còn Pending

[Backend Task Assignment v1.3](backend-task-assignment-v1.3.md) là nguồn chuẩn phân công chi tiết. **Assigned chỉ xác nhận trách nhiệm nghiệm thu đầu ra được giao, không phải trạng thái implementation/test/merge hoặc Completed, cũng không xác nhận tác giả toàn bộ code cũ.** Giữ lịch sử và GAP ở mục 1/4; không chép lại bảng endpoint/business rules của tài liệu phân công vào roadmap.

| Backend Developer | Module được phân công / phần chờ review | Giai đoạn liên quan | Trạng thái phân công | Dependency và phối hợp | Đặc tả chi tiết |
| --- | --- | --- | --- | --- | --- |
| **BE1 — Hoàng Quốc Hùng** | Ingredient, Nutrient Data, Recipe Versioning, Nutrition Calculation kcal/P/L/C, nutritional sources, age-group references; audit metadata module dinh dưỡng | G0 (contract), G3; G8 kiểm thử/tích hợp Phase 1 | **Assigned — Phase 1** | BE3 chốt normalized audit contract; BE1 ghi metadata/nguồn/version, bảo vệ API và bàn giao dữ liệu cho adapter BE3. Cung cấp contract cho G4/G5/G7, không sở hữu các giai đoạn này | [Phân công BE1](backend-task-assignment-v1.3.md), §2 và §5 |
| **BE2 — Nguyễn Duy Nam** | Meal-specific cutoffs 21:00 D−1/07:00 D, Automatic Settlement, Kitchen Acknowledgement, snapshot/version compatibility; audit metadata module suất ăn; regression amendment liên quan | G0 (contract), G1, G2; G8 kiểm thử/tích hợp Phase 1 | **Assigned — Phase 1** | Nhóm chốt chính sách Enrollment/loại bữa/chuyển đổi; BE2 xác minh C/merge/test/migration, cung cấp settlement/version membership và metadata cho adapter BE3, bảo vệ API mình triển khai | [Phân công BE2](backend-task-assignment-v1.3.md), §3 và §5 |
| **BE3 — Châu Quốc Bảo** | Centralized Audit Logs, `IAuditSource`/normalized contract, source adapters, Audit API/Timeline, CSV Export, sensitive-data redaction; RBAC Verification và kiểm tra API error contract | G0; tích hợp nguồn của G1/G2/G3; G8 kiểm thử/tích hợp Phase 1 | **Assigned — Phase 1** | BE1/BE2 cung cấp metadata của module mình; BE3 có thể làm adapters nguồn hiện hữu trước, rồi tích hợp nguồn mới khi code/contract sẵn sàng. Mỗi owner sửa/bảo vệ endpoint nghiệp vụ của mình; BE3 không sở hữu Reporting G7 | [Phân công BE3](backend-task-assignment-v1.3.md), §4 và §5 |
| Owner module gốc / người review cần nhóm xác nhận | Thay service tài khoản/liên kết/phân công hiện hữu ngoài đầu ra Phase 1; quyết định nghiệp vụ/shared contract do nhóm cùng xác nhận | Phần liên quan G0 và tác động G1 | **Pending Team Assignment Review** cho phần chưa giao; không thay owner hiện có bằng suy đoán | BE3 làm read adapter theo dữ liệu thực và báo GAP metadata/scope; sửa service nghiệp vụ phối hợp owner gốc, không mặc nhiên giao toàn bộ cho BE3 | [Ranh giới trách nhiệm](backend-task-assignment-v1.3.md), §5.1 |
| Chưa giao | Menu draft/submission/approval/publication và Parent đọc menu | G4 | **Pending Team Assignment Review** | Dùng contract dinh dưỡng BE1, phiên/snapshot BE2 và audit contract BE3 khi tích hợp | [Phần chưa giao](backend-task-assignment-v1.3.md), §7; roadmap G4 |
| Chưa giao | Dự tính/xuất nguyên liệu từ menu/recipe và suất hiện hành | G5 | **Pending Team Assignment Review** | G2 + G4, định lượng/version từ G3; cung cấp contract không đồng nghĩa nhận ownership | [Phần chưa giao](backend-task-assignment-v1.3.md), §7; roadmap G5 |
| Chưa giao | Serving/substitution, upload/phê duyệt ảnh, Android capture/queue/offline sync và Parent xem | G6 | **Pending Team Assignment Review** | G1 + G3 + G4 và contract evidence riêng; không giao BE2 hoặc BE3 chỉ vì có settlement/audit | [Phần chưa giao](backend-task-assignment-v1.3.md), §7; roadmap G6 |
| Chưa giao | Reporting approval/export, lineage/drill-down, As-Approved/Recomputed và đối chiếu | G7 | **Pending Team Assignment Review** | Tái sử dụng version/source metadata BE1/BE2 và normalized audit contract BE3; Audit CSV không phải report export | [Phần chưa giao](backend-task-assignment-v1.3.md), §7; roadmap G7 |
| Chưa giao owner nghiệm thu tổng thể | Demo/benchmark/migration/thiết bị/tài liệu bàn giao liên nhóm và tích hợp các module chưa phân công | G8 tổng thể | **Pending Team Assignment Review** | Ba BE chịu trách nhiệm test/tích hợp Phase 1 của module mình; nghiệm thu toàn MVP cần thêm các owner G4–G7, FE/Android và môi trường thử đã thống nhất | [Phần chưa giao](backend-task-assignment-v1.3.md), §7; roadmap G8 |

WP trong proposal chỉ dùng đối chiếu phạm vi; không tự đồng nhất WP với người/nhánh/PR. Phân công Phase 1 không tự mở rộng thành ownership toàn dự án, không tự giao công việc FE/Android cho BE và không đặt deadline. Những mục Pending vẫn thuộc MVP nếu proposal quy định, không chuyển thành Out of Scope.

## 6. Quy tắc thực hiện mỗi chặng

- Đọc proposal, GAP Analysis, continuity và source trước khi sửa; ghi rõ thay đổi model/migration/contract, hành vi cũ/mới và phạm vi dữ liệu legacy. Phân biệt “đã có code”, “đã test” và “đã nghiệm thu”.
- BE kiểm tra quyền và dữ liệu; FE tái dùng clay style, Modal và Sonner. Modal giữ dữ liệu khi lỗi, chặn đóng khi đang lưu.
- Test hành vi và các rủi ro cụ thể: sai scope, cut-off, phiên bản, transaction, nguồn báo cáo; dùng PostgreSQL biệt lập cho concurrency.
- Giữ Clean Architecture: endpoint → service Application → repository interface; không đưa EF/HTTP/storage vào domain. Android giữ feature/presentation-controller/domain/data và composition root hiện có; chỉ capture/metadata ảnh cần offline, không mở rộng toàn bộ ứng dụng.
- Giữ UTC khi lưu thời điểm; hiển thị/đối chiếu cutoff theo UTC+7. Dùng clock có thể điều khiển trong test, không đổi giờ hệ thống hoặc DB người dùng; phiên bản đã khóa/duyệt không bị sửa ngầm.
- Thay DbContext/entities/Program, migrations hoặc API dùng chung phải có review và kiểm tra tương thích FE/Android. Không coi “giữ contract” là lý do bỏ yêu cầu v1.3; thay đổi cần có contract và lộ trình chuyển đổi rõ ràng, không tự bắt buộc công cụ triển khai cụ thể.
- **Audit contract Phase 1:** BE3 thống nhất `IAuditSource`/normalized entry, identity, actor/time, version, filter/sort/pagination và redaction với BE1/BE2 trước tích hợp. BE1/BE2 ghi đầy đủ metadata/audit source nghiệp vụ của module mình theo contract; BE3 sở hữu adapter đọc/chuẩn hóa, không tạo lại workflow của hai module hoặc dựng lịch sử giả. Tái sử dụng nguồn hiện hữu trong khi chờ nguồn mới đủ metadata.
- **Quyền và lỗi API Phase 1:** mỗi BE bảo vệ/kiểm thử quyền API mình triển khai; BE3 kiểm thử RBAC xuyên các endpoint Phase 1, sensitive-data redaction và API error contract. GAP ở service nghiệp vụ do owner tương ứng phối hợp sửa; không mặc định BE3 sửa toàn bộ auth/error handling ngoài phạm vi đã duyệt.
- **Tránh conflict tích hợp:** phối hợp trước khi sửa DbContext/model snapshot, migrations, Dependency Injection/Program, shared DTO, filters và error handling. BE1/BE2 báo schema/metadata thay đổi, nhóm xác nhận reviewer và thứ tự merge/migration trên model nền đồng bộ; BE3 mặc định dùng read adapters, không tự sửa schema của module khác. Xác minh code/PR/merge/test mới nhất trước khi triển khai; không viết lại endpoint đã có chỉ để trùng route của đặc tả cũ, không dùng test H để xác nhận C đạt.
- Sau khi kiểm chứng, ghi tiến độ thật vào continuity/roadmap khi được giao cập nhật; không sửa proposal chỉ để khớp phần đã code. Proposal chỉ đổi khi scope/business rule được nhóm xác nhận. Các mâu thuẫn còn trong continuity/mobile docs là backlog đồng bộ tài liệu, không phải lý do sửa chúng trong lần chỉ cập nhật implementation plan này. Checklist thủ công giữ local theo quyết định hiện có.
- Giữ thông tin chạy local còn giá trị: BE qua `dotnet run --project be/src/MealTrace.Api --launch-profile http` (5184), FE `npm run dev` trong `fe/` (5173); Android theo `mobile/README.md`. DB/JWT/WhatsApp cấu hình ngoài Git qua scripts hiện có; không chạy migration chỉ để cập nhật kế hoạch. Mỗi trường dùng DB riêng theo hướng hiện tại; không thiết kế lại multi-tenant trong đợt này.
- Commit/push khi người dùng yêu cầu. Không đưa config local, secrets hoặc checklist vào Git.

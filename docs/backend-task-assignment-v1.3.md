# MealTrace — Backend Task Assignment, Phase 1 (Baseline v1.3)

> **Trạng thái:** Đề xuất phân công đã được Leader chốt về nguyên tắc; trạng thái code là **ảnh chụp audit**, cần xác minh lại trên Git/source mới nhất trước khi triển khai hoặc đánh dấu hoàn tất.  
> **Ngày lập:** 23/10/2026  
> **Nơi đặt trong repository:** `docs/backend-task-assignment-v1.3.md`  
> **Nguồn chuẩn phạm vi:** `PROPOSAL.md` v1.3 (theo Phiếu đăng ký MealTrace v1.3).  
> **Nguồn kế hoạch:** `docs/implementation-plan.md`, `docs/gap-analysis-v1.3.md`.  
> **Nguồn đối chiếu lịch sử:** `MealTrace_Pha1_API_Spec.docx` (04/10/2026), báo cáo audit BE1/BE2/BE3 ngày 23/10/2026, `continuity.md`.  
> **Mục đích:** Giao việc có thể triển khai/kiểm thử theo từng API mà **không viết lại phần đã có**, không nhầm code trên nhánh với code đã merge, và không mở rộng phạm vi ngoài Phase 1.

---

## 1. Quy tắc sử dụng tài liệu

### 1.1. Quyết định đã chốt

| Gói việc | Người phụ trách | Ownership Phase 1 | Trọng tâm |
| --- | --- | --- | --- |
| **BE1** | Hoàng Quốc Hùng | Ingredient, nutrient versions, recipe versions, nutrition calculation | Hoàn thiện kcal/P/L/C, nguồn, temporal/versioning, nhóm tuổi và kiểm thử |
| **BE2** | Nguyễn Duy Nam | Meal-specific cutoff, automatic settlement, Kitchen acknowledgement | Chuyển 07:30 thủ công sang 21:00/07:00 tự động, xác nhận bản suất hiện hành |
| **BE3** | Châu Quốc Bảo | Centralized audit APIs, audit-source adapters, RBAC/security verification | Tổng hợp audit hiện có, kiểm tra quyền và chuẩn lỗi xuyên Phase 1 |

**Ownership là trách nhiệm nghiệm thu các đầu ra được giao, không phải tuyên bố cá nhân đã viết toàn bộ code của module.** Các module G4–G7 chưa giao thêm cho ba người trong tài liệu này. FE và Android có trách nhiệm tích hợp theo kế hoạch nhóm; giao BE không tự động có nghĩa người đó phải hoàn thành toàn bộ giao diện.

### 1.2. Cách đọc trạng thái

| Nhãn | Ý nghĩa | Hành động |
| --- | --- | --- |
| **EXISTING — VERIFY** | Đã thấy hành vi/code nền; test có thể chưa đủ | Xác minh source mới nhất, chạy test bổ sung; **không viết lại nếu đạt** |
| **PARTIAL — UPDATE** | Có code nhưng thiếu trường, rule, contract hoặc test | Giữ phần đang chạy; bổ sung/sửa đúng GAP |
| **MISSING — IMPLEMENT** | Không tìm thấy implementation trong các ref đã audit | Thiết kế rồi triển khai sau khi chốt dependency |
| **BRANCH — VERIFY/INTEGRATE** | Đã thấy code trên nhánh chưa ở baseline H | Fetch/xác minh nhánh, chạy test/migration, review và tích hợp; **không code lại từ đầu** |
| **OBSOLETE — DO NOT IMPLEMENT** | Yêu cầu cũ xung đột phạm vi v1.3 | Không triển khai; chỉ giữ hỗ trợ đọc dữ liệu lịch sử khi cần |
| **PENDING DECISION** | Chưa đủ căn cứ để chốt business rule hoặc contract | Ghi quyết định, owner phê duyệt; **không tự hardcode** |

**Lưu ý về test:** IT = implemented and tested trong phạm vi test Codex đã chạy; IU = implemented but unverified. `167 passed / 29 skipped / 0 failed` chỉ thuộc baseline **H**, chủ yếu test in-memory SQLite; không chứng minh PostgreSQL concurrency hoặc code ở nhánh **C** đã chạy thành công.

### 1.3. Git/source baseline và giới hạn xác minh

- **H (baseline audit):** `bao@4005331`, trùng `origin/main` trong các remote-tracking ref **có sẵn** ở máy khi audit.
- **C (chưa nằm trong H):** `origin/cuong` và `origin/nam` cùng trỏ `b9b05ac`, có thêm code acknowledgement, snapshot token, test và sửa role boundary; Codex **không fetch/checkout**, **không chạy test trên C**.
- `origin/hung@a7e671b` và các commit do Hoàng Quốc Hùng ghi tác giả cho thấy phần Kitchen đã được đóng góp; code nền dinh dưỡng tồn tại trước Phase 1 và có refactor/tích hợp bởi thành viên khác.
- Các nhánh trên **không chắc là trạng thái GitHub mới nhất**. Trước khi code, từng người phải fetch/pull theo quy trình nhóm, kiểm tra commit/PR thực tế và ghi lại kết quả. Không đánh giá tiến độ con người chỉ bằng tên nhánh hoặc số commit.
- Không tính endpoint đổi tên là “Missing” nếu cùng chức năng đã có qua route khác. Ưu tiên Swagger và API contract hiện hành, không đổi route chỉ để trùng tài liệu 04/10.

### 1.4. Phạm vi v1.3 cần tuân thủ

- **Cutoff:** bữa sáng **21:00 ngày D−1**; trưa và xế **07:00 ngày D**, theo `Asia/Ho_Chi_Minh` (UTC+7), server quyết định thời điểm. Phải **tự động khóa/chốt**, phục hồi job trễ/restart/retry.
- Trẻ Enrollment hợp lệ **mặc định dự kiến có suất**. Báo không ăn/ngoại lệ tác động **theo từng phiên chưa qua cutoff**. “Dự kiến có suất” **không phải điểm danh thực tế**. Sau cutoff không tự sửa bản chốt; điều chỉnh phải qua workflow Admin duyệt, tạo version mới.
- **Nutrition:** kcal và protein/lipid/carbohydrate (P/L/C), dựa trên định lượng/đơn vị/nguồn; có phiên bản, khả năng tái lập bản đã công bố. Quyết định 3958/QĐ-BYT (2025), Phụ lục 7 được dùng làm **nguồn tham khảo dữ liệu thực phẩm**, không áp trực tiếp ngưỡng học sinh phổ thông cho trẻ mầm non. Khoảng tuổi phải theo căn cứ mầm non được xác nhận.
- **Android offline ảnh món ăn** là Must-have của toàn dự án nhưng không thuộc gói BE1/BE2/BE3 Phase 1 này; được thực hiện/được phân công riêng ở G6. BE3 không tự nhận sở hữu upload/queue Android.
- **Ngoài scope:** nguồn gốc thực phẩm, nhà cung cấp, purchase orders, kho, quy trình nấu nội bộ, kiểm thực ba bước, lưu mẫu thức ăn, payment/fee ledger/refund, AI sinh thực đơn, iOS. `FR-21` lưu ý dị ứng là **Optional**.
- `FR-22/23/24` là yêu cầu xuyên module. Audit log tập trung **khác** cơ chế lineage/tái lập báo cáo dinh dưỡng ở G7.

---

## 2. BE1 — Nguyên liệu, dữ liệu dinh dưỡng và công thức có phiên bản

**Owner:** Hoàng Quốc Hùng  
**FR chính:** FR-11, FR-12; phối hợp FR-23, FR-24.  
**Roadmap liên quan:** G0 (contract), G3 (dinh dưỡng); cung cấp contract cho G4/G5/G7.  
**Đánh giá tại H:** **PARTIAL**. API và schema nền đã có; chưa đạt full v1.3.  
**Nguyên tắc:** Chỉ bổ sung phần thiếu, bảo toàn API và dữ liệu/version cũ; không tự mở rộng sang workflow menu approval hoặc lập đơn mua hàng trong Phase 1.

### 2.1. Bảng API giao BE1

| ID | Method + API đích/thực tế | Trạng thái audit | Input/Output cần bảo đảm | Việc phải làm / nghiệm thu |
| --- | --- | --- | --- | --- |
| **BE1-API-01** | `POST /api/kitchen/ingredients` | **PARTIAL — UPDATE** trên H | In: `name`, `unit` và `category` nếu catalog xác nhận dùng; edible ratio phải có vị trí lưu rõ ràng. Out: `201` + `ingredientId`/thông tin mới | Giữ `IngredientService.CreateAsync`; kiểm tra `category`, quy tắc `ediblePercent` thuộc ingredient hay version; không lưu giá trị ở hai nơi gây mâu thuẫn. Test validation/trùng tên theo rule hiện hữu và Kitchen/Admin vs Teacher/Parent |
| **BE1-API-02** | `GET /api/kitchen/ingredients?search=&page=&pageSize=` | **EXISTING — VERIFY** trên H | In: search, page, pageSize (quy ước 25, tối đa 100); Out: danh sách có thứ tự ổn định, dữ liệu phiên bản dinh dưỡng hiệu lực được ghi rõ thời điểm | Test tìm kiếm/phân trang/sort, empty page, scope; xác minh dữ liệu hiệu lực/nguồn. Không viết endpoint list mới nếu API cũ đáp ứng |
| **BE1-API-03** | `POST /api/kitchen/ingredients/{id}/ingredient-versions` (**route thực tế**, không ép đổi sang `nutrient-versions`) | **PARTIAL — UPDATE** trên H | In: `validFrom`, `validTo?`, per-100g `energyKcal`, `proteinG`, `fatG`, `carbG`, `ediblePercent` theo model đã thống nhất, `source`, `sourceRef?`, `supersedesId?`; actor/time lấy backend. Out: `201`, `versionId`, valid/recorded metadata | Bổ sung fat/carb/source, recordedBy và khoảng hiệu lực; validation và migration an toàn; lịch sử append-only, không sửa version cũ. Test thiếu source, nguồn sai định dạng, hiệu lực chồng nhau theo rule đã chốt, thiếu dữ liệu thì lỗi đúng |
| **BE1-API-04** | `POST /api/kitchen/recipes` | **PARTIAL — UPDATE** trên H | In: `name`, `servingBasis`, `lines[{ingredientId,quantity,unit}]`, `roundingRule` theo rule đã chốt. Out: `201`, recipeId, version 1 | Giữ API; bảo toàn định lượng/đơn vị gốc, servingBasis và roundingRule đã áp dụng. Test quantity >0, duplicate ingredient, missing ingredient, sai unit, đầy đủ phiên bản |
| **BE1-API-05** | `POST /api/kitchen/recipes/{id}/versions` | **PARTIAL — UPDATE** trên H | In: `expectedVersion`, `servingBasis`/lines nếu có đổi, `reason`, roundingRule theo rule đã chốt. Out: `201`, version mới, tham chiếu version trước | Giữ optimistic conflict `409`; bổ sung reason và metadata; không UPDATE/DELETE version cũ. Test stale version, retry/concurrency, recipe read ở knownAt nếu có temporal rule |
| **BE1-API-06** | `GET /api/kitchen/recipes/{id}/versions/{v}/nutrition?validAt=&knownAt=&ageGroup=` | **PARTIAL — UPDATE** trên H | In: recipe/version, validAt, knownAt, ageGroup. Out: perServing kcal/P/L/C; từng line có input/unit/conversion, nutrientVersionId/pinnedId, contributions, roundingRule; kết quả so sánh tuổi kèm nguồn/range | Bổ sung lipid, carbohydrate, nguồn/phiên bản, logic gross/edible, nhóm tuổi; kết quả fixed-input vs recomputed phải phân biệt rõ. Test kết quả độc lập và dữ liệu temporal; không giả định `ml = g` |

**Quy ước HTTP:** 401 chưa xác thực, 403 sai vai trò, 404 không tìm thấy, 409 conflict phiên bản, 422 dữ liệu tính toán không hợp lệ/thiếu (`NUTRIENT_DATA_MISSING` nếu trường hợp tương ứng). Endpoint mới hoặc đã chỉnh phải trả cấu trúc lỗi có business `code`; phối hợp BE3 chốt `application/problem+json` mà không gây breaking change âm thầm với client cũ.

**API không giao Phase 1:** `POST .../price-versions` (Optional cũ, không Must-have v1.3); menu publish (G4), dự tính/xuất nguyên liệu (G5), fee ledger/ERP. Không tự thêm “hệ số Ak” vì chưa có định nghĩa được xác nhận.

### 2.2. Business rules và acceptance BE1

| ID | Quy tắc phải đạt | Kiểm thử/tiêu chí nghiệm thu |
| --- | --- | --- |
| **BE1-BR01** | Version của ingredient/recipe **append-only** | Service/API không cho ghi đè; có guard thích hợp ở persistence (DbContext) cho `IngredientVersion`, `RecipeVersion`, `RecipeIngredient`; test attempted Update/Delete không làm đổi bản đã lưu |
| **BE1-BR02** | Valid time / recorded time | Truy vấn `validAt`, `knownAt` chọn chính xác version khả dụng tại hai thời điểm; trường hợp `validTo`, version trùng hiệu lực và thứ tự ưu tiên được ghi thành **quy tắc chính thức trước khi implement**; không chỉ có `EffectiveFrom <= validAt` |
| **BE1-BR03** | Recipe as-known-at | Recipe version không được xuất hiện trong kết quả của `knownAt` trước thời điểm hệ thống ghi nhận recipe đó; cần test riêng |
| **BE1-BR04** | Công thức kcal/P/L/C | Có bộ ca tính độc lập với thực phẩm/định lượng xác định; chuẩn dữ liệu per 100 g được giải thích; rounding ở giai đoạn cuối theo rule đã lưu, tránh làm tròn trung gian làm lệch tổng |
| **BE1-BR05** | Gross/edible và đơn vị | Không nhân edible ratio hai lần; phải nêu rõ định lượng đầu vào là gross hay edible; `ml` chỉ đổi sang gram khi có conversion/density được xác nhận, nếu thiếu thì trả lỗi/đòi input phù hợp |
| **BE1-BR06** | Nguồn dinh dưỡng có tham chiếu | Mỗi version chứa `source` và tham chiếu nếu có; nguồn bản địa/bảng tham khảo ghi được rõ ràng, không nhầm `IngredientVersionId` với citation/source của thành phần thực phẩm |
| **BE1-BR07** | Không âm thầm tính 0 khi thiếu dữ liệu | Trả lỗi có `code` và danh sách ingredient thiếu; test cả khi ingredient có nhưng không có nutrient version hiệu lực |
| **BE1-BR08** | Cố định đầu vào bản công bố | API tính lại theo `validAt/knownAt` không tự thay thế các input/result đã ghim ở version công bố. BE1 cung cấp pinned input + rounding/source/version contract; workflow lưu approved snapshot được nghiệm thu ở G4/G7, **không ép BE1 tự xây toàn bộ G7** |
| **BE1-BR09** | Quyền | ADMIN/KITCHEN_STAFF thao tác đúng phạm vi; TEACHER/PARENT bị 403; chưa đăng nhập 401; có positive/negative tests cho mọi endpoint được giao |
| **BE1-BR10** | Audit metadata cho BE3 | Khi tạo nutrient/recipe version mới có ID, actor, time, reason nếu áp dụng, tham chiếu version trước và mô tả thay đổi có thể truy xuất. Thống nhất contract với BE3 **trước migration** |

### 2.3. Việc kiểm tra source/migration/test của BE1

- **Tái sử dụng:** `IngredientEndpoints`, `RecipeEndpoints`, `IngredientService`, `RecipeService`, `NutritionService`, `IngredientRepository`, các DTO Kitchen và schema nền `20260930031430_InitialCreate`.
- **Không coi đã kiểm thử:** `ArchitectureTests.MovedKitchenServicesResolveAndKeepIngredientRecipeNutritionFlow` là PostgresFact đã **skip** trong audit H; kiểm tra quyền `POST ingredient` đã pass nhưng chưa phải ma trận toàn API.
- **Migration có thể cần:** fatG/carbG, source/sourceRef, validTo/recordedBy, recipe reason/rounding/servingBasis, age-group reference (thiết kế tối thiểu được duyệt). Không tự khẳng định cấu trúc cuối cùng khi chưa review schema.
- **Test tối thiểu:** create/list/version; validAt/knownAt ở hai lần sửa nguồn; recipe v1/v2 + stale 409; kcal/P/L/C; gross/edible; ml thiếu density; rounding; missing data 422; append-only guard; 4 roles/anonymous; migration từ DB legacy.
- **Bàn giao:** Swagger contract thực tế, migrations/rollback hoặc hướng restore thử, test log và bảng kết quả mẫu (kcal/P/L/C, nguồn, version IDs), audit-source metadata/adapter input cho BE3.

---

## 3. BE2 — Cutoff tự động, settlement và Kitchen acknowledgement

**Owner:** Nguyễn Duy Nam  
**FR chính:** FR-03/04/07/08/09/14/19; phối hợp FR-01/22/23/24.  
**Roadmap liên quan:** G0/G1/G2.  
**Đánh giá:** H có nền settlement và amendment; **C có acknowledgement đáng kể nhưng chưa chạy test/merge tại thời điểm audit**. Automatic cutoff v1.3 **MISSING trên H/C**.  
**Ưu tiên trước khi code:** Xác minh nhánh C mới nhất, chạy migration và test riêng; không tạo lại acknowledgement nếu hợp lệ.

### 3.1. Bảng API / thành phần giao BE2

| ID | Method + API hoặc thành phần | Trạng thái audit | Input/Output cần bảo đảm | Việc phải làm / nghiệm thu |
| --- | --- | --- | --- | --- |
| **BE2-API-01** | `GET /api/kitchen/settlements` (và `/search` **nếu đang có trên nhánh**) | **BRANCH — VERIFY/INTEGRATE** | In: ngày/meal session, filter/page nếu đã công bố. Out: từng MealDay và các lớp, version/snapshot hiện hành, tổng suất, ackStatus | Giữ route đang dùng; xác minh `mealType` filtering nếu FR/UI cần, ID MealDay khác SettlementId; không đổi payload hiện hữu vô cớ. Test thiếu/chưa chốt/hủy/ngày nghỉ, sort, phân trang |
| **BE2-API-02** | `GET /api/kitchen/settlements/{mealDayId}/diff` (**route C**) | **BRANCH — VERIFY/INTEGRATE** | In: MealDayId (kiểm tra Swagger). Out: base/current count, added/removed theo lớp, adjustment/bản nguồn và `snapshotToken` nếu contract đã dùng | Đối chiếu API cũ `/meal-days/{dayId}/amendments` và roster để tránh trùng logic. Bổ sung nguồn/version/reason cần thiết cho UI và audit; test amendment và bản cũ thiếu roster |
| **BE2-API-03** | `GET .../meal-days/{mealDayId}/portions` và API amendment đang có (đường dẫn đầy đủ **xác minh Swagger**) | **EXISTING — VERIFY** | Out: danh sách trẻ, nguồn quyết định và bản suất theo lớp/phiên, trước/sau điều chỉnh | Regression theo cutoff mới, quyền Teacher/Kitchen/Admin và ghi danh hiệu lực; không tạo thêm route `GET /settlements/{id}` nếu cùng nghiệp vụ đã đủ |
| **BE2-API-04** | `POST /api/kitchen/settlements/{mealDayId}/acknowledge` (**route C**) | **BRANCH — VERIFY/INTEGRATE** | In: `snapshotToken`, `note?`; actor/time lấy backend. Out: acknowledgement ID, actor, time, token/count/status | Stale token trả 409; retry cùng user/token không tạo trùng; các nhân viên bếp có thể ack độc lập; không thay số suất. Xác nhận response 201/200 retry theo contract đã review, không đổi chỉ vì đặc tả 04/10 |
| **BE2-API-05** | `GET /api/kitchen/settlements/{mealDayId}/acknowledgements` (**route C**) | **BRANCH — VERIFY/INTEGRATE** | Out: lịch sử ID/user/name/time/note/token, trạng thái `current`/`superseded` và **tham chiếu các settlement/version đã xác nhận** | Bổ sung khả năng truy ra chính xác tập version của từng acknowledgement; token dạng hash **không thể giải ngược** thành items. Cần chốt cách lưu immutable membership hoặc dữ liệu nguồn tương đương; test v1→v2 và history không đổi |
| **BE2-CORE-01** | **Automatic Meal Settlement Worker / scheduled background process** (**không bắt buộc có public endpoint mới**) | **MISSING — IMPLEMENT** | In: session cần chốt, CutoffAt và dữ liệu được ghi nhận trước cutoff. Out: snapshot gốc theo từng lớp/phiên cùng nguồn, log xử lý và trạng thái job | Worker tự tìm session đến hạn, xử lý delayed/restart/retry/multi-instance mà không duplicate; dùng chung nghiệp vụ snapshot đã có, không build settlement engine thứ hai |
| **BE2-CORE-02** | **Cutoff policy dùng chung**: `SchoolTime`, calendar/session, absence/exception/enrolment | **PARTIAL — UPDATE** (legacy 07:30) | Input: serving date, normalized meal type, server time. Output: `CutoffAt` đúng bữa và lock/eligibility tại thời điểm đó | Chuyển toàn bộ nhánh dùng 07:30 sang meal-specific; rà validation, UI contract, chuyển lớp/ngừng học, lịch đóng/mở phiên. Không thay hằng số đơn lẻ rồi coi hoàn thành |

**Không giao BE2 viết lại:** phần nền `PortionAmendmentService` đã hoạt động, roster lịch sử, approval versioning hoặc bảng settlement cũ nếu đã đáp ứng. Chỉ sửa chỗ không còn đúng theo cutoff mới, acknowledgement hoặc race conditions có bằng chứng.

### 3.2. Contract acknowledgement cần chốt trước khi merge

Đặc tả cũ cho `POST /api/kitchen/meal-days/{mealDayId}/acknowledgements` dùng `items[{settlementId,version}]`. Nhánh C dùng `POST /api/kitchen/settlements/{mealDayId}/acknowledge` với **`snapshotToken` đại diện tập các settlement mới nhất theo lớp của một MealDay**.

**Quyết định đề xuất:** **Ưu tiên giữ `snapshotToken`/route C** nếu kiểm thử chứng minh an toàn. Không coi khác tên route là thiếu API. Nhưng phải đạt đồng thời:

1. Token xác nhận tập bản suất **cụ thể**, không chỉ xác nhận “đã mở màn hình”; sau bất kỳ amendment làm đổi bản đang áp dụng, token/status mới có hiệu lực và token cũ không còn đủ xác nhận.
2. Audit/history **truy lại được chính xác** settlementId + version thuộc lần ack, hoặc snapshot bất biến tương đương. **Không cố giải ngược token hash**; nếu C chưa lưu đủ membership, bổ sung cấu trúc tối thiểu sau review.
3. Chỉ KITCHEN_STAFF được ghi ack; Admin có quyền đọc theo phạm vi; Teacher/Parent 403, anonymous 401. Các role kết hợp cần theo policy thống nhất, không mở quyền ghi cho Admin ngoài đặc tả mà không có quyết định mới.
4. Retry cùng actor + token idempotent; actor khác có thể xác nhận riêng. Append-only; không sửa/xóa log nhận cũ.
5. Nếu chọn đổi 201 thành 200 khi retry, phải xem tương thích client/test và ghi vào API contract; yêu cầu cốt lõi là **không tạo trùng**, không phải thay status code tùy tiện.

### 3.3. Business rules và acceptance BE2

| ID | Quy tắc phải đạt | Kiểm thử/tiêu chí nghiệm thu |
| --- | --- | --- |
| **BE2-BR01** | Cutoff theo meal session | Breakfast D: 21:00 D−1; Lunch/Snack D: 07:00 D. Timezone server dùng UTC khi lưu, `Asia/Ho_Chi_Minh` khi diễn giải. Đúng thời điểm cutoff đã là muộn |
| **BE2-BR02** | Loại bữa ổn định | Không dựa chuỗi tự nhập tùy ý; ánh xạ MealType legacy phải được review trước migration, không tự đoán sáng/trưa/xế từ tên dữ liệu cũ |
| **BE2-BR03** | Tự khóa/chốt | Hệ thống tự chốt đúng/sau hạn; Admin không cần chờ để bấm. Nếu giữ manual endpoint thì chỉ là cơ chế phục hồi được phân quyền |
| **BE2-BR04** | Không ảnh hưởng phiên còn mở | Báo cả ngày lúc 22:00 D−1 không đổi breakfast D đã khóa; có thể đổi lunch/snack D chưa khóa. Khóa bữa sáng không khóa toàn bộ ngày |
| **BE2-BR05** | Chốt dùng dữ liệu tại cutoff | Worker trễ vẫn đọc được tập thông tin hệ thống đã biết **trước cutoff**, không dùng thay đổi sau đó để viết lại bản gốc |
| **BE2-BR06** | Race/idempotency | Job chạy lại, retry hoặc nhiều instance không sinh settlement gốc trùng; test transaction, uniqueness và lock wait trên PostgreSQL thử |
| **BE2-BR07** | Preservation | Không di trú lại các bản chốt lịch sử theo giờ mới, không ghi đè roster/version/audit; không xóa snapshot legacy thiếu nguồn |
| **BE2-BR08** | Post-cutoff adjustment | Teacher/Admin gửi yêu cầu; Admin duyệt mới tạo bản v2; từ chối giữ bản v1; stale request 409; phiên/roster không âm/trùng suất |
| **BE2-BR09** | Kitchen acknowledgement | Đã ack v1 không đồng nghĩa đã nhận v2; history diễn giải `current/superseded` đúng; retry không duplicate; ack không làm đổi total/roster |
| **BE2-BR10** | Audit metadata BE3 | Settlement/ack có ID/version, actor/time, cut-off, reason/adjustment, supersedes/snapshot membership đủ truy vết; thống nhất contract trước khi BE3 tích hợp adapter |
| **BE2-BR11** | Role security | API Kitchen read/write đúng quyền; Admin chỉ đọc ack; Teacher/Parent 403; nguồn trả roster không lộ lớp/trẻ ngoài quyền |

### 3.4. Những điểm PENDING DECISION trước khi implement BE2-CORE-02

- Chính sách **chuyển lớp/ngừng học** giữa lúc bữa sáng đã khóa và bữa trưa/xế còn mở: khi nào ghi danh mới có hiệu lực với từng bữa, xử lý hồ sơ đã lên lịch; không sửa lại breakfast cũ.
- Mapping thực tế của `MealType` hiện có sang Breakfast/Lunch/Afternoon snack và cách xử lý tên bữa lạ.
- Ngày áp dụng cutoff mới cho **session chưa chốt**; chính sách xử lý phiên legacy đã tạo trước thời điểm chuyển đổi.
- Cách worker xử lý phiên không có trẻ, ngày nghỉ, ngày học bù, phiên bị hủy, job phục hồi sau downtime; acceptance phải bao phủ nhưng không tự thêm hành vi trái lịch được duyệt.

**Ghi lại quyết định vào tài liệu nghiệp vụ/team decision log trước khi code; nếu chưa chốt, BE2 có thể chuẩn bị worker/contract và test kỹ thuật không phụ thuộc quyết định đó, nhưng không tự hardcode chính sách nghiệp vụ đang mở.**

### 3.5. Việc kiểm tra nhánh/migration/test của BE2

- **Trước hết:** kiểm tra `origin/cuong` / `origin/nam` (hoặc ref mới nhất sau fetch), review commit `76ad8bc`, `51673fc`, `f57f616`, `b9b05ac`; xác định code nào đã merge/PR, không cherry-pick ngẫu nhiên.
- **Migrations C cần xác minh trên PostgreSQL thử:** `20261012162538_KitchenSettlementAcknowledgement`, `20261018172929_KitchenAcknowledgedSnapshot`, `20261018173502_KitchenAcknowledgementUniqueness`. Chưa có bằng chứng đủ để coi đã áp dụng an toàn trên DB nhóm.
- **Test acknowledgement:** stale token 409; new settlement requires re-ack; hai actor độc lập; retry cùng actor; concurrent insert; cancelled/unsettled MealDay; history v1/v2 + superseded; append-only; quyền từng endpoint.
- **Test cutoff:** `20:59:59/21:00:00`, `06:59:59/07:00:00`, ngày D−1→D; không có bữa sáng; Parent báo/cancel; Teacher exception; chuyển lớp; job trễ/restart/nhiều instance; không chốt hai lần; PostgreSQL concurrency.
- **Demo:** lớp có breakfast=30 chốt 21:00 Chủ nhật → Parent báo 22:00, lunch/snack thứ Hai còn 29 → 07:00 tự chốt lunch=29 → Teacher xin giảm 1 và Admin duyệt lunch v2=28 → bếp xác nhận v2; lịch sử v1 không bị xóa.
- **Bàn giao:** code/PR/migrations test; Swagger snapshot/ack contract; log worker/recovery; kiểm thử PostgreSQL và acceptance checklist với giới hạn đã biết; dữ liệu settlement/ack cho BE3.

---

## 4. BE3 — Audit log tập trung và RBAC/security verification

**Owner:** Châu Quốc Bảo  
**FR chính:** FR-22, FR-23; phối hợp các nhánh dữ liệu phục vụ FR-24, không sở hữu toàn bộ reporting G7.  
**Roadmap liên quan:** G0 (audit nền/phạm vi), tích hợp nguồn G1/G2/G3; G7 chỉ dùng lại contract khi triển khai sau.  
**Đánh giá:** Audit APIs/`IAuditSource` **MISSING** tại H/C được kiểm tra; RBAC có nền **PARTIAL**, đã có sửa scope trên C chưa chạy test.  
**Khởi đầu khuyến nghị:** Viết contract + adapter Calendar/Enrollment/PasswordReset rồi API list; không đợi BE1/BE2 xong toàn bộ.

### 4.1. Bảng API giao BE3

| ID | Method + API | Trạng thái audit | Input/Output cần bảo đảm | Việc phải làm / nghiệm thu |
| --- | --- | --- | --- | --- |
| **BE3-API-01** | `GET /api/admin/audit-logs` | **MISSING — IMPLEMENT** | Query: `from?`, `to?`, `actorId?`, `entityType?`, `entityId?`, `action?`, `page=1`, `pageSize=25` (max 100). Out: phân trang `AuditEntry` đã chuẩn hóa, stable sort | Tạo read service tổng hợp `IAuditSource`; lọc/paging/sort thống nhất trên nhiều nguồn, không trả thiếu do phân trang từng nguồn sai. Chỉ Admin; test filter, tie-break, legacy/nullable fields, 401/403 |
| **BE3-API-02** | `GET /api/admin/audit-logs/{auditId}` | **MISSING — IMPLEMENT** | Path: stable `{source}:{id}`. Out: một event với actor/time/action/entity/reason/before/after/correlationId **nếu nguồn có** | Parser/dispatcher source → adapter; validate ID, không phát lộ record ngoài phạm vi; missing 404; chỉ Admin. Không tự dựng `before/after` khi không có dữ liệu |
| **BE3-API-03** | `GET /api/admin/audit-logs/entities/{entityType}/{entityId}` | **MISSING — IMPLEMENT** | Path entityType/entityId, query page/pageSize/from/to nếu cần. Out: timeline nhiều nguồn cùng một đối tượng, ổn định theo thời gian | Chuẩn hóa entity identity, diễn giải rõ “theo trẻ” và event nào là lịch sử của trẻ; không suy diễn liên kết từ các timestamp gần nhau. Test enrollment → exception → amendment |
| **BE3-API-04** | `GET /api/admin/audit-logs/export?from=&to=&format=csv` | **MISSING — IMPLEMENT** | In: khoảng ngày tối đa **31 ngày**, `format=csv`, filter tương thích list nếu được xác nhận. Out: CSV có cột chuẩn hóa/sort/encoding hợp lệ, **redacted** | Giới hạn **10.000 dòng**, vượt hạn trả 422 có business code; chống lộ password/token/SecurityStamp/SĐT; chống CSV formula injection. Test ngày biên/10.000–10.001 dòng; Admin-only |

**Đặc tả response đề xuất cho `AuditEntry`** (contract nội bộ, không tuyên bố đã có trong code):

```json
{
  "id": "calendar:123",
  "source": "calendar",
  "occurredAt": "2026-10-23T08:00:00Z",
  "actor": { "id": "...", "name": "..." },
  "action": "calendar.update",
  "entityType": "MealDay",
  "entityId": "...",
  "reason": null,
  "before": null,
  "after": null,
  "correlationId": null
}
```

Ví dụ trên chỉ minh họa **shape**; `null` có nghĩa **nguồn không có dữ liệu xác nhận được**, không phải lời mời tự suy đoán. Tên `source`, `action`, `entityType` phải có danh mục/enum mapping ổn định; trước/sau có thể là JSON đã lọc dữ liệu nhạy cảm, không phải entity EF serialize nguyên xi.

### 4.2. Thành phần kỹ thuật BE3 phải tạo

| Thành phần | Trạng thái | Trách nhiệm / lưu ý |
| --- | --- | --- |
| `IAuditSource` và `AuditEntry` model/DTO | **MISSING — IMPLEMENT** | Contract đọc lịch sử theo nguồn, lọc/thời gian/identity và chuyển sang event chuẩn hóa; không đặt logic EF ở Domain. Chốt contract với BE1/BE2 sớm |
| Audit aggregation service | **MISSING — IMPLEMENT** | Tổng hợp có phân trang đúng trên nhiều nguồn, lọc theo trường được hỗ trợ, sort `occurredAt DESC`, rồi `source`, rồi `id` (thứ tự tie-break được đóng băng trong contract) |
| Audit endpoint mapping / authorization | **MISSING — IMPLEMENT** | `/api/admin/audit-logs...`, chính sách ADMIN, read-only; 401 anonymous/403 other roles |
| Source adapters — existing | **MISSING — IMPLEMENT** | Calendar, Password Reset, Meal Exception, Absence, Enrollment, Amendment, Settlement, Parent Link/Import **theo metadata thực có** |
| Source adapters — BE1/BE2 | **BLOCKED BY CONTRACT** | BE1: ingredient/nutrient/recipe versions; BE2: settlement acknowledgement; adapter được tích hợp sau khi nguồn/metadata và nhánh đã sẵn sàng |
| RBAC test matrix Phase 1 | **PARTIAL — EXTEND** | Tái sử dụng tests hiện hữu và test trên nhánh C; 4 role + anonymous trên tất cả endpoint Phase 1 đã thống nhất; positive + negative, record-level scope |
| Error contract review | **PARTIAL — UPDATE SELECTIVELY** | Endpoint mới dùng `application/problem+json` có `code`; phối hợp sửa conflict/error chung khi có test hoặc owner approval, không refactor toàn hệ thống trong một lượt |
| Security/redaction/export tests | **MISSING — IMPLEMENT** | Không lộ password/hash/temp password/JWT/SecurityStamp/SĐT trong API/CSV; kiểm soát dữ liệu cá nhân của trẻ; không tạo endpoint audit ghi/xóa |

**Lưu ý kỹ thuật:** Các nguồn khác nhau lưu ID và timestamp khác nhau. Không thể bảo đảm pagination đúng bằng cách lấy đúng `pageSize` sự kiện ở mỗi nguồn rồi concat/sort mà không xét offset/filter. Chốt thuật toán/query có giới hạn dữ liệu và kiểm chứng bằng test nhiều nguồn trước khi đưa vào production. Không bắt buộc xây mới một bảng audit chung nếu source adapters đáp ứng và hiệu năng chấp nhận được.

### 4.3. Nguồn audit cần tích hợp (BE3 sở hữu adapter, không viết lại workflow của người khác)

| Nguồn | Bằng chứng trong code/audit | BE3 thực hiện | Phụ thuộc |
| --- | --- | --- | --- |
| **Calendar** | `MealCalendarAudit`, có actor/reason/BeforeJson/AfterJson | Adapter + mapping `calendar.*` + redaction/tests | Có thể làm ngay |
| **Password reset** | `AccountPasswordResetAudit`, actor/target/time/reason, không lưu mật khẩu | Adapter `account.password_reset`, bảo vệ sensitive; không tạo before/after giả | Có thể làm ngay |
| **Meal exception** | `MealRegistration`, actor/sequence/supersedes, có dữ liệu legacy thiếu actor | Adapter set/restore; trước/sau chỉ khi có thể xác minh từ chuỗi | Có thể làm ngay |
| **Meal absence** | `MealAbsence`, ReportedBy/CancelledAt; replacement link không lưu đủ | Adapter create/cancel; hiển thị `null/unknown` nơi thiếu actor/correlation; báo thiếu metadata cho owner workflow | Có thể làm ngay phần xác thực được |
| **Enrollment** | `Enrollment`, ngày hiệu lực + RecordedAt/EndRecordedAt/actor | Adapter transfer/stop/reenroll; identity theo trẻ, xử lý end-exclusive đúng | Có thể làm ngay |
| **Portion amendment** | `PortionAmendment`, `PortionAmendmentResolution`, audit/version nguồn | Adapter request/resolve; không dựng lại phiên bản nguồn không tồn tại | Có thể làm ngay |
| **Portion settlement** | `PortionSettlement`, `SettlementDecision`, cutoff/version/supersedes | Adapter `portion.settle`; người/time/số suất theo nguồn có thật | Có thể làm ngay, mở rộng khi BE2 cập nhật cutoff |
| **Parent link / import** | `ParentLinkRequest`, `StudentImportBatch` đã có; link trực tiếp/đổi assignment còn GAP | Adapter theo dữ liệu có thật, lập danh sách điểm ghi lịch sử còn thiếu gửi owner module review | Có thể làm một phần; không tự sửa AccountService của người khác |
| **Ingredient / nutrient / recipe version** | Model đã có ID/version/time, thiếu recordedBy/source/reason | BE3 công bố shape audit, xây adapter sau khi BE1 thêm/chuẩn hóa metadata | **BE1** |
| **Kitchen acknowledgement** | Có trên nhánh C: actor/time/note/token, chưa chắc reconstruct được items/version | BE3 xây adapter sau khi BE2 xác minh token/history và merge | **BE2** |

### 4.4. Business rules và acceptance BE3

| ID | Quy tắc phải đạt | Test/đầu ra |
| --- | --- | --- |
| **BE3-BR01** | Admin-only/read-only | Chỉ GET; Admin 200, Teacher/Kitchen/Parent 403, anonymous 401; không tạo PUT/POST/PATCH/DELETE audit |
| **BE3-BR02** | Không làm giả thông tin lịch sử | Event thiếu actor, reason, before/after, correlationId phải thể hiện `null`/unknown; không suy luận từ tên file, ID hoặc sự kiện gần thời điểm |
| **BE3-BR03** | Merge/sort/pagination ổn định | Sort `occurredAt DESC`, rồi `source`, `id`; query liên nhiều nguồn, trang 1/2 không trùng/thiếu khi dữ liệu không đổi; test filter entity/actor/time/action |
| **BE3-BR04** | ID audit ổn định | `source:id` duy nhất, parser reject nguồn không hợp lệ; GET detail trả đúng event hoặc 404 |
| **BE3-BR05** | Bảo vệ dữ liệu nhạy cảm | API list/detail/timeline/CSV không lộ password/hash/temp password/token/SecurityStamp; mask SĐT trong CSV chỉ hiển thị 3 số cuối; không serialize nguyên entity; an toàn công thức CSV |
| **BE3-BR06** | Export đúng giới hạn | >31 ngày hoặc >10.000 dòng bị 422 với business `code`; file CSV đúng nội dung/encoding/sort; không làm treo hệ thống vì query vô hạn |
| **BE3-BR07** | Phân quyền xuyên module | Ma trận tất cả endpoint Phase 1: 4 role + anonymous, cả positive/negative; Parent theo verified link, Teacher theo Enrollment hiệu lực; kết quả dựa trên source merged mới nhất |
| **BE3-BR08** | Chuẩn error handling | New endpoints trả ProblemDetails có `code` cho validation/conflict; không nuốt lỗi concurrent thành 500. Các lỗi legacy ngoài phạm vi sửa được đưa vào issue list |
| **BE3-BR09** | Không mất audit cũ | Không xóa/ghi đè bảng lịch sử nghiệp vụ hiện có; adapter đọc đúng bản legacy với metadata thiếu; thay đổi DbContext chỉ khi thực sự cần và đã review migration |
| **BE3-BR10** | Không nhầm audit với reporting lineage | Audit timeline ghi ai/lúc nào/làm gì; lineage và tái lập phép tính As-Approved/Recomputed ở G7 **không** là deliverable độc lập của BE3 Phase 1 |

### 4.5. Kế hoạch implement nhỏ cho BE3 (để bắt đầu được ngay)

1. **BE3-A — Contract:** Đọc source hiện tại và source nhánh đã merge; chốt `AuditEntry`, nguồn/ID, mapping actor/time, `IAuditSource`, khả năng filter/pagination, redaction; gửi BE1/BE2 contract cần cung cấp metadata. **Không sửa DbContext của BE1/BE2.**
2. **BE3-B — Adapters lõi:** Calendar → Enrollment → PasswordReset (hoặc thứ tự theo dễ test), thêm MealException, Absence, Amendment, Settlement. Có unit tests mapping/legacy/redaction.
3. **BE3-C — API list/detail/timeline:** Admin-only, filter/sort/paging chính xác, test đa nguồn và record scope.
4. **BE3-D — CSV export:** 31 ngày/10.000 dòng, che dữ liệu, tránh CSV injection, test security/performance trên data sample.
5. **BE3-E — RBAC/error tests:** Chạy ma trận 4 role + anonymous, test trên code tích hợp BE1/BE2; phối hợp sửa code ở đúng owner thay vì tự sửa toàn bộ service.
6. **BE3-F — Integrate audit BE1/BE2:** Kết nối adapter nutrient/recipe và acknowledgement khi merge và contract đã kiểm chứng; test demo timeline tổng hợp.

**Bàn giao BE3:** 4 audit GET endpoints; source adapters; contract/documented mappings; RBAC security matrix + automated tests; test redaction/export; demo audit từ Enrollment/Meal Exception/Amendment, từ Nutrient/Recipe và Kitchen Ack khi các nguồn sẵn sàng.

---

## 5. Hợp đồng dùng chung và quy tắc tránh conflict

### 5.1. Ranh giới trách nhiệm

| Chủ đề | Chủ sở hữu phần nghiệp vụ | BE3/nhóm phối hợp như thế nào |
| --- | --- | --- |
| Metadata source/recipe/rounding/bitemporal | **BE1** | BE3 cung cấp `AuditEntry`/`IAuditSource` contract; BE1 bổ sung actor/source/reason cần thiết; BE3 viết adapter |
| Cutoff/worker/settlement/ack token/membership | **BE2** | BE3 cần dữ liệu acknowledgement bất biến và IDs; BE2 bổ sung nguồn theo contract; BE3 viết adapter |
| `IAuditSource`, audit list/detail/timeline/export | **BE3** | BE1/BE2 review contract; không tự tạo cơ chế audit chuẩn hóa thứ hai |
| Phân quyền của endpoint nghiệp vụ | **Owner endpoint: BE1/BE2** | BE3 viết ma trận test chung, nêu lỗ hổng để owner sửa, review mã và error contract |
| Audit của tài khoản/liên kết hiện có | **Owner module gốc, cần nhóm xác nhận người review** | BE3 chỉ làm read adapter và ghi issue metadata thiếu; thay đổi service không giao mặc nhiên cho BE3 |
| FE/Android integration | **Theo phân công FE/Mobile riêng** | BE owner cung cấp Swagger/contract/test data, phối hợp sửa client nếu contract buộc thay |
| Reporting lineage/As-Approved/Recomputed | **Pending Team Assignment Review (G7)** | Phase 1 chỉ bảo đảm version/source metadata có thể tái sử dụng, chưa giao BE3 implement báo cáo |

### 5.2. Quy tắc migration, branch và API

1. **Trước khi sửa:** fetch refs mới nhất, so thực tế `origin/main` và branch/PR, xác minh trạng thái commit. H/C trong tài liệu chỉ là audit snapshot.
2. **Không tạo migrations song song trên model snapshot không đồng bộ.** BE1/BE2 báo schema thay đổi; reviewer/tích hợp chủ trì chốt thứ tự, kiểm tra DbContext model snapshot và PostgreSQL DB thử. BE3 mặc định read-only adapters, hạn chế migration nếu không thật sự cần.
3. Thay `MealTraceDbContext`, entities dùng chung, `Program`, dependency injection, error filters hoặc route dùng chung phải thông báo/review trước. Không tự đổi các endpoint đang dùng trên Web/Android chỉ để khớp đặc tả cũ.
4. Source code/backend bảo vệ dữ liệu và quyền; FE/Mobile chỉ ẩn thao tác theo quyền, không thay thế backend authorization.
5. Dùng `TimeProvider` cho thời gian nghiệp vụ phát sinh mới; lưu UTC, trình bày cutoff theo UTC+7; test fake time, **không chỉnh giờ máy/DB**. Các nguồn legacy có thể đang dùng UtcNow hoặc thiếu actor: báo thiếu, không dựng giả.
6. API lỗi mới: chuẩn `application/problem+json` với business `code`; 401 unauthenticated, 403 forbidden, 409 stale/concurrency, 422 rule/data invalid. Kiểm tra client compatibility trước khi thay shared `ResultExtensions`/filters. Quy tắc page/pageSize mặc định 25, max 100 khi áp dụng cho endpoint mới, trừ contract cũ cần giữ và đã được ghi nhận.
7. Giữ append-only/immutable với data report-critical. Không xóa hoặc tính lại snapshots lịch sử theo cutoff mới; không phát tán JWT, password hoặc config secrets vào source/tests/logs.
8. Trước khi PR/merge: mô tả endpoints thay đổi, schema/migration, test đã chạy (pass/fail/skip), mức độ ảnh hưởng Web/Mobile, known limitations và người đã review. Không tự coi đã hoàn thành vì build pass hoặc unit tests SQLite pass.

### 5.3. Contract cần chốt trước khi triển khai đồng thời

| Contract | Ai chuẩn bị | Ai review | Điều kiện xong |
| --- | --- | --- | --- |
| `AuditEntry`, `IAuditSource`, action/source/entity naming | **BE3** | BE1/BE2 | Actor/time/identity/source/version/reason rõ; nullable legacy; privacy/redaction được mô tả |
| Ingredient/Recipe metadata/version và pinned input | **BE1** | BE3 + người phụ trách menu/report về sau | `source`, `recordedBy`, recipe reason/rounding, temporal selection có test độc lập |
| MealType/cutoff policy và chuyển đổi legacy | **BE2** | Nhóm/GVHD khi có business ambiguity | 21:00/07:00 theo bữa; mapping tên bữa và Enrollment policy được ghi rõ |
| Acknowledgement snapshot membership/identity/status | **BE2** | BE3 và FE/Mobile owner | Có thể truy phiên bản đã ack, stale/duplicate/superseded có tests |
| Phân quyền/ProblemDetails contract | **BE3 điều phối**, owner từng API implement | BE1/BE2 | 4 role + anonymous, record scope và error `code` rõ, giữ tương thích client |

---

## 6. Acceptance checklist, tích hợp và demo Phase 1

### 6.1. Điều kiện nghiệm thu từng người

- [ ] **BE1:** các API nguyên liệu/recipe/nutrition được cập nhật hoặc xác minh đúng trạng thái; đủ kcal/P/L/C, temporal valid/known, nguồn và nhóm tuổi có căn cứ; có test độc lập và migrations PostgreSQL thử; contract audit cho BE3.
- [ ] **BE2:** xác minh/merge phần ack trên branch sau test; auto settlement 21:00/07:00 đúng từng bữa và hồi quy điều chỉnh; ack version/token/truy source đầy đủ; worker restart/idempotent/concurrency được thử trên PostgreSQL; contract audit cho BE3.
- [ ] **BE3:** 4 endpoint audit đọc được nguồn hiện hữu và nguồn BE1/BE2 đã tích hợp; filter/sort/pagination/CSV/redaction đúng; ma trận quyền và legacy null có test; không thay đổi âm thầm service người khác.
- [ ] **Integration:** một dữ liệu/nhiều versions có thể tra theo source và role; negative paths trả lỗi đúng; source cũ vẫn đọc được, migrations không phá dữ liệu.
- [ ] **Evidence:** mỗi owner nộp PR/commit, Swagger sample requests/responses, test names/kết quả, migration test log, ảnh/chứng cứ demo nếu có, ghi rõ skipped/unverified.

### 6.2. Demo tích hợp đề xuất

1. **BE1:** Kitchen/Admin tạo ingredient, nhập nutrient version có source, tạo recipe v1; API trả kcal/P/L/C với nutrientVersionIds; thay đổi nguồn/recipe version và `validAt`/`knownAt` cho ra kết quả được giải thích, bản input đã cố định không bị ghi đè.
2. **BE2:** Dùng fake clock/script test để demo D−1 21:00 breakfast chốt 30 → Parent báo D lúc 22:00 → lunch/snack còn 29 → D 07:00 tự chốt lunch 29 → Admin duyệt giảm 1 thành v2=28 → Kitchen ack đúng snapshot mới, ack cũ thành superseded.
3. **BE3:** Admin lọc timeline của trẻ theo Enrollment/Meal Exception/Portion Amendment; xem password reset không lộ secret; xem recipe version và acknowledgement khi nguồn đã merge; Teacher gọi audit API nhận 403, anonymous 401; xuất CSV có masking.
4. **Tích hợp/test:** PostgreSQL concurrency, API scope, migration từ dữ liệu cũ, dashboard/test evidence; ghi rõ phần Android/Web chưa nghiệm thu nếu không có thiết bị/môi trường.

### 6.3. Kết quả không được suy diễn từ Phase 1

- Không khẳng định đã hoàn thành **MVP v1.3** chỉ vì BE1/BE2/BE3 Phase 1 xong: G4 menu approval, G5 ingredient estimates, G6 serving/offline images, G7 reporting/reproducibility và G8 acceptance vẫn phải được phân công/triển khai theo roadmap.
- Không đánh dấu code ở **C** là đã merge vào H nếu chưa kiểm tra; không dùng test H để tuyên bố C đạt.
- Không tự động bổ sung chức năng thanh tra, giá nguyên liệu/chi phí, mua hàng, lưu mẫu, kiểm thực, AI hay full offline.

---

## 7. Các việc chưa giao trong tài liệu này

| Roadmap | Nội dung | Trạng thái phân công |
| --- | --- | --- |
| **G4** | Menu draft/submission/approval/publication và Parent đọc menu có quyền | **Pending Team Assignment Review** |
| **G5** | Dự tính/xuất nguyên liệu từ menu/recipe + bản suất hiện hành | **Pending Team Assignment Review** |
| **G6** | Serving/substitution, upload/phê duyệt ảnh, Android capture/queue/offline sync, Parent xem | **Pending Team Assignment Review** |
| **G7** | Report approval/export, As-Approved/Recomputed, lineage và drill-down | **Pending Team Assignment Review** |
| **G8** | Demo/benchmark/migration/thiết bị/tài liệu bàn giao liên nhóm | **Pending Team Assignment Review** |

Các công việc trên **vẫn thuộc mục tiêu MVP** nếu được quy định trong proposal; chỉ là **chưa được giao owner trong bản phân công Backend Phase 1**. Không được coi là Out of Scope.

---

## 8. Quick-start cho Codex và thành viên

Khi giao Codex hỗ trợ một BE cụ thể, yêu cầu đọc `PROPOSAL.md`, `docs/implementation-plan.md`, `docs/gap-analysis-v1.3.md`, file này, `continuity.md` và **source code/branch hiện tại**. Bắt đầu bằng đối chiếu trạng thái mục việc trên HEAD mới nhất; thực hiện **đúng API + BR + test của người được giao**, không triển khai phần của BE khác hoặc các chặng chưa phân công. Nếu gặp business rule `PENDING DECISION`, báo rõ câu hỏi/quyết định cần chốt trước khi code phần bị chặn. Không cập nhật lịch sử tiến độ thành “đã hoàn thành” khi chưa có chứng cứ test/merge.

**Thứ tự phối hợp:** (1) xác minh branch + thống nhất ownership → (2) BE3 công bố audit contract, BE1/BE2 chốt metadata/ack contract → (3) triển khai và test song song theo module → (4) merge tuần tự migrations/shared changes → (5) PostgreSQL/role/demo end-to-end → (6) cập nhật `continuity.md` theo bằng chứng, khi được giao.

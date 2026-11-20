# MealTrace — Phân công Backend Phase 2 theo v1.4

> **LEAD REVIEW — 15/11/2026, awaiting final team sign-off.** This document is a consolidated *execution plan*, not a commit, a passing acceptance report or an approval to change BM04. Technical baseline: `origin/main@a6b187e6eae9553f7966d5444b8745f9896d2a11`; Codex ran backend tests on this SHA (278 passed / 0 failed / 34 skipped). Re-fetch and reconfirm before coding. See [Lead decisions](phase2-phase3-lead-decisions-v1.4.md).


**16–22/11/2026**; core days thứ Hai–thứ Sáu **16–20/11**, 21–22/11 chỉ contingency. Baseline đã fetch: `origin/main` **`a6b187e6eae9553f7966d5444b8745f9896d2a11`**, working branch `Bao`, audit 15/11 UTC+7. Nguồn chuẩn: [registration v1.4](review2/Phieu_FA26SE270_V1.4.docx) và [BM04 §3,6,8](review2/SEP490_FA26SE270_BM04_Review2_Commitment_1.docx); [audit chi tiết](phase2-phase3-rebaseline-audit.md). Đây là task plan, **chưa có production implementation của các task NEW**.

Kết quả phải có cuối tuần: **một vertical slice thật trên PostgreSQL** từ menu draft → submit → approve → publish → latest settlement → actual serving → cùng ingredient dùng hai source-entry → report draft và source trace. Không nghiệm thu bằng JSON mock, chỉ entity hay API đọc seed có sẵn.

## 1. Ownership và quy tắc

| Người | Trách nhiệm Phase 2 | Trách nhiệm BM04 giữ nguyên |
| --- | --- | --- |
| **BE1 Hoàng Quốc Hùng** | Supplier/source catalogue, menu lifecycle, frozen publication nutrition | WF-02 / M3 |
| **BE2 Nguyễn Duy Nam** | Bảo vệ cutoff/worker/amendment/ack; actual serving và split allocation; photo API contract cho Android | WF-03 + WF-04 / M2; collaborator API của WF-05 |
| **BE3 Châu Quốc Bảo** | Report draft/trace, audit adapters/security, **migration integrator duy nhất** | WF-06 / M5; hỗ trợ auth M1, điều phối CI |
| **Nguyễn Văn Cường** | Android source selection, durable offline photo queue, retry/restart tests song song | WF-05 / M4; sở hữu Flutter/device cases |
| **Lê Phát Đạt** | FE menu/source/serving/report screens và integration, bảo vệ enrollment/link flows | WF-01 / M1; sở hữu FE cases |

- Giữ implementation đã merge: worker, 21:00 D−1 / 07:00 D, amendments và immutable settlements, token ack, full macros/density, centralized audit. Không xây lại Phase 1.
- Baseline test thực chạy ngày 15/11: **278 passed, 0 failed, 34 skipped**. Skips gồm 33 PostgreSQL + 1 baseline measurement; FE/Flutter/device chưa chạy. Tất cả acceptance bên dưới là **PLANNED**, không phải test vừa pass.
- Mỗi issue copy nguyên task card và §2–§3/checklist chung. Task owner là PR owner; reviewer nêu trong card. Merge chỉ sau review và quyền người quản lý repo; không dùng branch-only code làm bằng chứng đã xong.
- Bảo generate migrations/designer/`MealTraceDbContextModelSnapshot.cs` theo hàng đợi; Hùng/Nam chỉ giao model/config/DTO và migration notes. Không ba người cùng regenerate snapshot. Không sửa migration đã áp dụng; bổ sung migration mới có rehearsal.
- Chỉ một trường. Không procurement, tồn kho, fee ledger, chứng nhận ATTP; SourceRef dinh dưỡng không đại diện lần nhận hàng. FR-21 optional, không chiếm critical path.

## 2. Exit gate Phase 2

Mục tiêu tích hợp **20/11 17:00**, dự phòng sửa blocker đến **22/11 17:00**. Đây là **gate theo bằng chứng**, không phải lời hứa rằng tính năng sẽ tự hoàn thành. Nếu phải dùng weekend, ghi rõ owner, thời gian và rủi ro. Hùng/Nam/Bảo và hai client owners cùng xem replay:

1. Trên PG biệt lập: ingredient trứng + recipe version thực; 2 supplier khác nhau, 2 source-entry có ngày/lượng/ref; Kitchen lập menu ngày, Admin approve/publish, Parent có linked child chỉ thấy bản published.
2. Worker dùng dữ liệu trước cutoff; 2 lớp có settlement; amendment được duyệt tạo version mới. Bếp đọc latest, stale token trả 409, ack mới giữ ack cũ. Report không cộng tất cả settlement history.
3. Kitchen ghi dish thực phục vụ theo session/class/recipe version, actor server; 1.000g trứng thực dùng phân bổ **600g nguồn A + 400g nguồn B**, có quantity và source-entry IDs. Thay món có lý do; retry không sinh bản trùng.
4. Admin tạo report **draft** từ dữ liệu thật, trace tới đúng serving/use/source-entry/supplier revisions. Case thiếu 400g source trả `hasSourceGap=true`, không tự lấy supplier mặc định; draft không được gọi approved.
5. Mutation sai role/scope bị 401/403; một case PG concurrency có một winner và rollback loser. Logs có SHA/command/pass-fail-skip, migration từ bản cũ và seed/replay instructions.
6. Đạt nối FE menu tới API thật; Cường trình diễn queue lưu file/metadata qua kill/restart với contract đã freeze. Full approved/private photo và report approval/export/recompute hoàn tất P3, không đánh dấu M4/M5 xong ở P2.

Không qua gate thì ghi rõ task/chủ thể/blocker, dùng weekend để sửa tích hợp. Nếu vẫn thiếu vertical slice ngày 22/11, **đỏ deadline 06/12**, không dùng thời gian dự phòng của Phase 3 để viết lại nền và âm thầm bỏ FR bắt buộc.

## 3. Shared contract freeze — 16/11 trước 12:00

Các path **NEW** trong tài liệu đều mang nghĩa **PROPOSED ROUTE — CONFIRM WITH OPENAPI**, không phải endpoint tại HEAD. Path **EXISTING** đã kiểm từ endpoint source. GUID trong JSON ví dụ dùng ký hiệu `mealId/sourceA/...`; khi seed/test phải thay GUID thật. Freeze DTO/error code vào PR contract và OpenAPI sau implementation; clients không parse tiếng Việt để phân loại lỗi.

| Contract | Quyết định triển khai đề xuất | Owner/giao nhận |
| --- | --- | --- |
| Session và latest | `MealDayId` hiện là session (ngày + loại bữa), không thêm MealSession song song. Freeze `settlementIds[]` theo lớp và `snapshotToken`; giữ CutoffAt cũ | Nam → Hùng/Bảo/Cường/Đạt |
| Supplier/source | `Supplier` + immutable `SupplierRevision`; `IngredientSourceEntry` versioned, ingredientId, supplierRevisionId, referenceDate, quantity/unit, optional delivery/lotRef. Không dùng Ingredient.SourceRef làm supplier FK | Hùng → Nam/Bảo |
| Recipe/menu | Chọn **recipeVersionId**, không lấy latest mỗi lần đọc; menu version có trạng thái draft/submitted/approved/published/rejected, transitions append events. Week view ghép 7 ngày từ cùng API range | Hùng → clients/Nam |
| Actual use | ServingVersion → ServedDish → IngredientUse → SourceAllocation. IngredientUse lưu actual quantity/unit/basis và nutrient version riêng; allocations nhiều entry cho cùng ingredient. Sum converted allocation + missingQuantity = actual quantity, tolerance thống nhất 0.001g | Nam → Bảo/Cường |
| Thiếu nguồn | `sourceGap:{missingQuantity,unit,reason}`; đủ nguồn trả null. Không chọn ngầm “supplier hiện tại”; không cấm lưu chứng cứ incomplete. Draft/trace giữ gap, approval policy **PENDING REVIEWER DECISION**: BM04 yêu cầu hiển thị source gap, không tự suy ra bắt buộc 422; trong mọi trường hợp draft/trace phải ghi gap và không bao giờ báo complete giả | Nam/Bảo, reviewer chốt 16/11 |
| Temporal/actor | UTC persisted, offset timestamp bắt buộc; business date ISO. Server lấy actor/recordedAt; mỗi correction có reason + priorVersionId. Không PUT lên approved content. Supplier name/reference cũng snapshot/versioned | Ba BE |
| Nutrition | `mode=pinned` cho published inputs, lưu cả response + nutrient/recipe/range IDs + rule version. `mode=recompute` chỉ tạo view mới, không đổi published. Reference tuổi thiếu → 422, không dùng fake range | Hùng/Bảo |
| Concurrency | `expectedVersion` hoặc existing snapshotToken; stale 409. Idempotency UUID client + scope actor/device/serving, payload hash; cùng key/body trả cùng ID, khác body 409. Commit business rows + audit atomic | Nam/Bảo |
| Photo handoff | stable clientPhotoId, servingVersionId, capturedAt, checksum, mediaType, file bytes; receivedAt server. DB metadata/object private; không public PhotoUrl. Retry hỗ trợ response mất sau commit | Nam ↔ Cường, Bảo review |
| Role/scope | Kitchen ghi serving/source/menu, Admin review/publish/report, Parent chỉ published và verified linked child trong lớp/ngày; Teacher theo lớp. Inspector chỉ read grant có hạn cho reports, **chưa có policy hiện hành**; Bảo chốt reviewer | Bảo |
| Audit | Tái dùng IAuditSource/EfAuditSource; event với entity ID, actor,time,reason,priorVersionId, before/after theo dữ liệu đã lưu; không dựng lịch sử từ current master | Bảo, mỗi BE giữ metadata |
| Scope-history legacy | Direct parent link và account role/class/student scope changes cần immutable events mới; ghi actor/time/reason và old/new scope. Policy quyền lịch sử khi đổi TeacherAssignment chốt16/11; không sửa lại direct-link current-enrollment scope đã có test pass | Bảo → Đạt; P3-B03 khép implementation |
| Error | 400 shape/unit, 401 anonymous, 403 role/scope, 404 absent ID, 409 stale/state/idempotency, 422 thiếu dữ liệu tính toán/nguồn bắt buộc; `application/problem+json` có `code` ổn định cho route mới | Bảo + clients |

**Migration/rollback chung:** additive tables/nullable columns và FK Restrict cho historical references; unique keys ở DB, không chỉ check trong service. Bảo rehearsal từ schema hiện tại và fresh DB; xuất SQL để review. Backup/restore thử trước staging, không chạy Down làm mất source/approved records; rollback app trước hoặc forward-fix. Không sửa CutoffAt/legacy provenance hàng loạt; record cũ thiếu dữ liệu được đánh dấu rõ. Migration failure không được để một nửa serving/allocation/report tồn tại.

**Capacity tối đa để lập kế hoạch:** BE1 36h task +4h integration, BE2 36h+4h (N01 10h/N02 22h/N03 4h), BE3 36h+4h (B03 gồm 3h CI bootstrap). Các giờ chỉ là ước tính, không coi thứ Bảy/Chủ nhật là giờ code mặc định. Hùng H01 14h + H02 14h không thể đồng thời hoàn tất 17–18/11 nếu bắt đầu từ freeze trưa16/11; dùng handoff model/IDs trước, API đủ sau. Cường và Đạt mỗi người 5 core days P2, tiếp 10 core days P3; không tính thời gian hai người này vào 120h BE. Trường/reviewer phải dành buổi chốt reference/policy 16–17/11. Đây là timebox rủi ro cao; daily gate dùng để phát hiện thiếu người, không hạ scope. **Tổng kế hoạch sau điều chỉnh: Phase 2 (16–22/11) + Phase 3 (23/11–06/12), 15 ngày làm việc; task IDs và acceptance giữ nguyên.**

## 4. BE1 — Hoàng Quốc Hùng

| ID / loại / giờ | Deliverable | Due / PR owner / reviewer | Dependencies |
| --- | --- | --- | --- |
| P2-H01 NEW, 14h | Supplier revisions và dated ingredient-source entries | **18/11 12:00** (17/11 bàn giao model/IDs) / Hùng / Nam+Bảo | Contract freeze, P2-B01 migration slot |
| P2-H02 NEW, 14h | Menu draft/submit/review/publish, daily + weekly read | **19/11 17:00** (18/11 state model + endpoints skeleton) / Hùng / Bảo+Đạt | H01 IDs không buộc menu phải giả nguồn thực dùng; existing recipe |
| P2-H03 EXTEND, 8h | Frozen publication nutrition và catalogue/guardian menu read | **20/11 12:00** / Hùng / Bảo+Đạt | H02, reference policy; P3-H02 hoàn thiện reference dataset |

### P2-H01 — Dated physical-source catalogue

**FR-11/23, M3/M5, WF-02/06.** EXISTING: `GET/POST /api/kitchen/ingredients`, `POST /api/kitchen/ingredients/{id}/ingredient-versions`; `be/src/MealTrace.Application/Features/Kitchen/IngredientService.cs` `CreateAsync/AddVersionAsync/ResolveEffectiveAsync`; `be/src/MealTrace.Domain/Entities/Ingredient.cs`, `IngredientVersion.cs`. Full nutrient source/version đã có; không sửa thành supplier receipt.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `GET/POST /api/kitchen/suppliers`; `POST /api/kitchen/suppliers/{id}/versions`; `GET /api/kitchen/suppliers/{id}/history`; `GET/POST /api/kitchen/ingredient-source-entries`; `POST /api/kitchen/ingredient-source-entries/{id}/versions`; `GET /api/kitchen/ingredient-source-entries/{id}/history`.

```json
{"name":"Nguồn A","contact":"đầu mối trường","reference":"NCC-A"}
```

→ `201 {"id":"supplierA","versionId":"supplierA-v1","version":1,"recordedAt":"...","recordedBy":"serverActor"}`. Source input/output:

```json
{"ingredientId":"egg","supplierVersionId":"supplierA-v1","referenceDate":"2026-11-19","quantity":2000,"unit":"g","deliveryRef":"GN-A-19","lotRef":null}
```

→ `201 {"id":"sourceA","versionId":"sourceA-v1","version":1,"ingredientId":"egg","quantity":2000,"unit":"g"}`. Correction thêm `expectedVersion:1`, `reason`, trả version2/priorVersionId; GET list dùng `ingredientId/from/to/page/pageSize` → `{items,total,page,pageSize}` và history trả các versions.

**Rules/security:** A/K write/read; T/P 403. Quantity >0, unit rõ, ngày có nghĩa vụ truy vết, supplier/version/ingredient tồn tại; không tự tạo tồn kho hoặc chặn theo “remaining stock”. Không hard-delete record đã tham chiếu. Name/contact changes có revision để report lịch sử không drift. Bảo thêm SourceAudit adapter, Hùng lưu metadata transactional. Migration mới supplier/source tables và reference FK; rollback theo §3, không gộp receipts khác ngày vào Ingredient.

**Acceptance (planned):** (1) A/K tạo sourceA ngày 19/11 2.000g và sourceB ngày 18/11 3.000g cho egg → 201, list trả 2 entries khác ID, nutrient SourceRef không đổi [FR-11/M3/WF-02]. (2) Correction sourceA quantity 2.100, reason, expectedVersion1 → 201 v2; history vẫn 2.000 v1/actor/time; gửi stale v1 lại →409 [FR-23/M5/WF-06]. (3) Parent tạo source hoặc quantity−1 → lần lượt 403/400, DB không thêm rows [FR-11/22/M3/WF-02].

### P2-H02 — Menu approval lifecycle

**FR-05/13/23, M3, WF-02.** EXISTING: `GET /api/meal-days/{id}` qua `be/src/MealTrace.Application/Features/Meals/MealService.cs.GetMealDayAsync`; `be/src/MealTrace.Domain/Entities/MenuDish.cs` chỉ chứa MealDayId/RecipeVersionId, `MealDay.PublishedAt` không phải approval.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `GET/POST /api/kitchen/menus`; `GET /api/kitchen/menus/{id}`; `POST /api/kitchen/menus/{id}/versions`; `POST /api/kitchen/menus/{id}/submit`; `POST /api/admin/menus/{id}/review`; `POST /api/admin/menus/{id}/publish`; `GET /api/kitchen/menus/{id}/history`.

```json
{"mealDayId":"lunch19","dishes":[{"recipeVersionId":"friedEgg-v1","plannedPortions":20}],"reason":"Thực đơn ngày"}
```

→ `201 {"id":"menu19","versionId":"menu19-v1","version":1,"state":"draft"}`. Submit `{expectedVersion:1}` →200 submitted; review `{expectedVersion:1,decision:"approve",reason:"Đủ thông tin"}` →200 approved (reject →rejected); publish `{expectedVersion:1}` →200 published + publishedAt + nutritionSnapshotId. Read date range `?from=2026-11-16&to=2026-11-22` → `{items:[...],total}`. Version append gửi full dishes + expectedVersion/reason, không mutate v1.

**Rules/security:** K/A soạn; chỉ A review/publish; phải submitted trước approve, approved trước publish; reject có reason và revision/resubmit. Session cancelled/missing recipe version bị chặn; menu chưa đủ nutrient bắt buộc không publish. Menu kế hoạch chưa được coi actual serving hay supplier thực dùng. Lưu menu versions + transition events; giữ published versions cũ, không đổi recipe reference theo latest. Không bắt mọi client dùng lại scaffold MenuDish làm state machine.

**Migration/risk:** additive Menu/MenuVersion/MenuItem/transition + link MealDay; legacy MenuDish chưa có approval → đánh dấu legacy/unverified, không backfill approved. Transaction state/version uniqueness do Bảo tích hợp.

**Acceptance:** (1) K tạo/submit, A approve/publish →201/200/200/200, GET week thấy đúng published version; GET trước publish không lộ Parent [FR-05/13/M3/WF-02]. (2) K gọi publish →403; A publish draft/stale expectedVersion →409, không ghi publication [FR-22/13/M3/WF-02]. (3) Menu dùng legacy macro null →422 `NUTRIENT_DATA_INCOMPLETE`; A reject kèm reason →200 rejected, tạo v2 sửa rồi submit được, v1 không đổi [FR-12/13/23/M3/WF-02].

### P2-H03 — Pin calculation ở publication và API chọn dữ liệu

**FR-12/20/24, M3/M4/M5, WF-02/05/06.** EXISTING `GET /api/kitchen/recipes/{id}/versions/{v}/nutrition?mode=pinned|recompute&ageGroup=...`; `be/src/MealTrace.Application/Features/Kitchen/NutritionService.cs.CalculateAsync/CompareAsync`, `RecipeService.RoundingOf`; không viết calculator mới.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `GET /api/kitchen/recipes?search=&page=&pageSize=`; `GET /api/kitchen/recipes/{id}/versions`; `GET /api/parent/students/{studentId}/menus?from=&to=`. Publication H02 gọi calculator nội bộ và lưu snapshot; không dùng GET recompute mặc định làm As-Published.

Input chọn recipe `{recipeVersionId,ageGroup}`; output catalogue `{items:[{recipeId,name,version,versionId}],total,page,pageSize}`; parent menu response:

```json
{"items":[{"mealDayId":"lunch19","menuVersionId":"menu19-v1","state":"published","nutrition":{"kcal":100,"proteinG":6,"fatG":7,"carbG":1},"calculationSnapshotId":"calc1","referenceVersionIds":[],"ageComparisonStatus":"reference_missing"}]}
```

Số trên chỉ fixture format, không nutritional recommendation. Capture exact recipe/nutrient IDs, quantities, gross/edible, density, rounding/algorithm version và reference ranges khi có. P2 có thể hiển thị reference_missing rõ ràng; **P3 phải nạp reference được trường xác nhận và freeze**, không coi nhãn này là hoàn thành FR-12. Nếu consumer yêu cầu age comparison khi thiếu range thì trả 422 như existing service.

**Role/migration:** catalogue A/K; Parent menu yêu cầu approved active link và enrollment đúng meal/class, unlinked 403. `ParentLinkService`/repository dùng lại cho quyền, không chỉ query param studentId. Snapshot stored JSON+FK/version IDs do Bảo integrate, dùng decimal không double; correction không overwrite snapshot.

**Acceptance:** (1) Publish calc1, thêm nutrient correction, GET Parent menu vẫn cùng snapshot/totals; GET explicit recompute có thể khác, không ghi đè calc1 [FR-24/M5/WF-06]. (2) P linked đúng child GET200 published; P khác hoặc link revoked GET403; draft không xuất hiện [FR-20/22/M4/WF-05]. (3) 2 recipe versions, paginated catalogue trả đúng version IDs và chọn v1 vẫn lấy v1 sau v2 [FR-12/13/M3/WF-02].

## 5. BE2 — Nguyễn Duy Nam

| ID / loại / giờ | Deliverable | Due / PR owner / reviewer | Dependencies |
| --- | --- | --- | --- |
| P2-N01 VERIFY/FIX, 10h | Test-first cutoff, alias bữa và ghi danh trễ; chỉ sửa lỗi tái hiện được | **17/11 12:00 repro/tests; 20/11 12:00 minimal fix** / Nam / Bảo | Contract meal types 16/11 |
| P2-N02 NEW, 22h | Actual servings, substitution, ingredient-use split sources | **19/11 graph/API skeleton; 20/11 12:00 integrated write/read** / Nam / Hùng+Bảo | H01, H02/H03, B01 |
| P2-N03 CONTRACT, 4h | Photo retry/approval/private-view handoff cho Cường | **16/11 12:00 initial; 17/11 12:00 signed; review 20/11** / Nam / Cường+Bảo | Có thể fixture trước N02, IDs thật sau N02 |

### P2-N01 — Khép lỗ legacy cutoff, giữ worker/ack

**FR-01/03/04/08/09/14/19, M1/M2, WF-01/03/04.** EXISTING routes giữ nguyên: `PUT /api/admin/meal-calendar/{code}/schedule`, `.../days/{date}`, POST `.../preview|generate`; `POST /api/admin/students/{id}/enrollments`; `POST /api/meal-days/{id}/settle`; amendment review; `GET /api/kitchen/settlements/{id}/diff`, POST `.../acknowledge`. Worker không cần public route mới.

Files cần sửa tối thiểu: `be/src/MealTrace.Domain/Time/SchoolTime.cs` `Cutoff/EarliestEnrollmentDate`; `be/src/MealTrace.Application/Features/Calendar/MealCalendarService.cs` `NormalizeTypes/EditDayAsync`; `be/src/MealTrace.Application/Features/Students/StudentAdministrationService.cs.ChangeEnrollmentAsync`; `be/src/MealTrace.Infrastructure/Persistence/Repositories/EnrollmentQueries.cs.OnDate` và `MealDecisionRepository.cs.Eligible`; giữ `be/src/MealTrace.Api/Workers/MealSettlementWorker.cs.ProcessPendingSettlementsAsync`, `WorkflowService.SettlePortionsAsync`, `KitchenSettlementService.AcknowledgeSettlementAsync`.

**Contract:** existing meal creation `{date:"2026-11-19",mealType:"Breakfast",schoolYear:"2026-2027"}` →201 `{id,date,mealType,schoolYear,cutoffAt:"2026-11-18T14:00:00Z"}`. Diff/ack giữ `{snapshotToken:"64hex",note:null}` →201 cùng ack ID khi retry. Chuẩn hóa type mới thành Breakfast/Lunch/Snack; aliases đã dùng trong DB/clients phải có mapping review, unknown 400. Không rewrite CutoffAt locked session. Trường hợp enrollment thay đổi sau cutoff phải giữ quyết định của session đã khóa cả khi worker chưa chạy, xét recorded-time của việc kết thúc ghi danh; không dùng current EndDate làm lịch sử cutoff.

**Migration/risk:** ưu tiên sửa logic và explicit alias mapping; nếu thêm meal type code, nullable/backfill chỉ từ alias chắc chắn; ambiguous giữ original và flag. Reconcile calendar từng session, không cấm bữa trưa chỉ vì sáng đã khóa. Giữ serialized locks/unique constraints; chỉ A sửa calendar/enrollment, Teacher exception assigned class, Parent owned child; ack không cấp Admin.

**Acceptance:** (1) Fake clock tại D−1 20:59:59/21:00:00 và D 06:59:59/07:00:00; trước cutoff exception200, đúng cutoff409; worker retry tạo đúng một settlement/version; Breakfast/Lunch/Snack đúng cutoffs [FR-03/08/M2/WF-03]. (2) Lunch cutoff07:00, worker chạy08:00, absence07:15 và transfer07:15; snapshot phải dùng membership/decision trước07:00, không mất trẻ; correction đi amendment tạo version mới [FR-01/04/19/M2/WF-03/04]. (3) Approve amendment→latest count đổi, stale ack409, fresh ack201, old settlement/ack còn nguyên; Admin ack403 [FR-09/14/22/M2/WF-04].

### P2-N02 — Actual serving và split supplier allocations

**FR-15/23, M4/M5, WF-05/06.** Source hiện có: `be/src/MealTrace.Domain/Entities/MenuDish.cs`, `MealEvidence.cs` chỉ read scaffold; `be/src/MealTrace.Application/Features/Meals/MealService.cs.GetMealDayAsync`, `be/src/MealTrace.Application/Features/Kitchen/KitchenSettlementService.cs.GetSettlementDiffAsync` để reuse latest semantics. Không dùng MenuDish làm món đã phục vụ.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/kitchen/servings`; `GET /api/kitchen/servings/{id}`; `POST /api/kitchen/servings/{id}/versions`; `GET /api/kitchen/servings/{id}/history`.

```json
{"clientRequestId":"uuid","mealDayId":"lunch19","classId":"classA","menuVersionId":"menu19-v1","settlementId":"classA-settlement-v2","snapshotToken":"64hex","dishes":[{"plannedRecipeVersionId":"friedEgg-v1","actualRecipeVersionId":"friedEgg-v1","servedPortions":20,"substitutionReason":null,"ingredients":[{"ingredientId":"egg","nutrientVersionId":"egg-nutrient-v1","quantity":1000,"unit":"g","weightBasis":"gross","allocations":[{"sourceEntryVersionId":"sourceA-v1","quantity":600,"unit":"g"},{"sourceEntryVersionId":"sourceB-v1","quantity":400,"unit":"g"}],"sourceGap":null}]}]}
```

→201 `{id,versionId,version:1,responsibleStaffId,recordedAt,hasSourceGap:false}`; retry cùng payload →200 cùng IDs. Correction gửi expectedVersion/reason + full payload, →201 version2/priorVersionId, không update v1.

**Rules/security:** K ghi, A/K đọc; không tin responsibleStaffId do client gửi (actor server). Validate class thuộc settlement/session, menu published, source ingredient trùng actual-use ingredient, source date không sau serving date, unit conversion được lưu. Actual portions khác settlement phải có lý do, **không sửa settlement để khớp serving**. Substitution phải có actual recipe version + reason, lưu planned version riêng. Actual quantity và allocations là lượng cả dish trong class/session, không lượng mỗi suất; source receipt quantity chỉ truy vết, không trừ kho. Missing allocation được lưu với missingQuantity/reason và flag; tổng quá actual hoặc source sai ingredient trả400/422. Sau correction phục vụ, các report dùng version trước vẫn giữ nguyên.

**Migration/risk:** Serving + ServingVersion + ServedDish + IngredientUse + SourceAllocation tables, restrictive FK, unique request key/hash, version uniqueness. Commit cả graph và audit một transaction; loser conflict không để orphan. Bảo generate snapshot theo thứ tự supplier/menu trước serving. Cường phải nhận full response/lookup errors để UI chọn source đúng.

**Acceptance:** (1) Input trên →201; GET trace có 1 egg use/2 allocations 600+400, staff=token user, class/meal đúng, v1 frozen [FR-15/23/M4/WF-05]. (2) Chỉ sourceA600 + gap400 reason “thiếu phiếu” →201 hasSourceGap=true; sai ingredient hoặc sum1100 với actual1000 →422 và không có partial graph [FR-15/06/M4/M5/WF-05/06]. (3) Retry cùng key/body→200 cùng serving ID; cùng key khác body hoặc correction stale→409; Teacher/Parent create→403; v1 vẫn nguyên sau v2 [FR-15/22/23/M4/WF-05].

### P2-N03 — Photo API contract handoff, không giao Flutter cho BE2

**FR-17/20/22, M4, WF-05 owner Cường.** EXISTING `be/src/MealTrace.Domain/Entities/MealEvidence.cs` có CapturedAt/SyncedAt/PhotoUrl, không có upload lifecycle. Files liên quan `be/src/MealTrace.Application/Dtos/Meals/MealEvidenceSummary.cs`, `be/src/MealTrace.Application/Features/Meals/MealService.cs`; client receiver `mobile/lib/features/school/data/school_repository_impl.dart` (Cường sửa ở implementation run).

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/kitchen/servings/{id}/photos` multipart metadata+file; `GET /api/kitchen/photos/{id}`; `POST /api/admin/photos/{id}/review`; `GET /api/parent/students/{studentId}/photos`; `GET /api/parent/students/{studentId}/photos/{photoId}/content`.

```json
{"clientPhotoId":"uuid","servingVersionId":"serving-v1","capturedAt":"2026-11-19T11:05:00+07:00","sha256":"64hex","contentType":"image/jpeg"}
```

Upload lần đầu201 `{id,clientPhotoId,state:"synced",capturedAt,receivedAt,servingVersionId}`; retry cùng key/checksum200 cùng ID; khác hash409. Review `{decision:"approve",expectedVersion:1,reason:"Ảnh đúng phiên"}` →200 approved; reject phải reason. List Parent200 chỉ approved/scoped metadata; content trả bytes private có no-store, 403 unrelated/revoked/unapproved. Giới hạn đề xuất JPEG/PNG ≤10 MiB, decode/signature validation và safe filename, không tải URL do client đưa. CapturedAt giữ nguyên, ReceivedAt server; clock skew đánh dấu review thay vì tự sửa capture time.

**Deliverable P2:** contract/examples/error matrix + Cường xác nhận queue key lưu trước upload và giữ file sau restart. **Implementation upload/approval/private bytes thuộc P3-N01, nhưng P3-N01 phải cung cấp upload staging tối thiểu từ 23/11 (để Android không chờ đến 25/11)**, không đánh dấu endpoint tồn tại khi mới có contract. Schema storage objectKey/checksum/state history do Bảo review, Nam giao design; không migration giả trong P2.

**Acceptance contract (planned):** (1) Cường replay mất response sau commit, metadata/file queue giữ nguyên key, expected same ID200 và 1 object [FR-17/M4/WF-05]. (2) Hai payload cùng key khác hash →409, UI giữ conflict để xử lý, không auto overwrite [FR-17/23/M4/WF-05]. (3) Decision matrix approved+linked→200 bytes, pending/rejected/unlinked→403, anonymous401; fixture và route names được hai bên ký nhận, chưa tính là backend/device test pass [FR-20/22/M4/WF-05].

## 6. BE3 — Châu Quốc Bảo

| ID / loại / giờ | Deliverable | Due / PR owner / reviewer | Dependencies |
| --- | --- | --- | --- |
| P2-B01 INTEGRATE, 8h | Schema integration và role/error contract | Slots 17–19/11 / Bảo / Hùng+Nam | Models H01/H02/N02, freeze16/11 |
| P2-B02 NEW, 20h | Report draft + actual-source trace | **18/11 draft schema/queries by contract; 20/11 17:00 live PG integration** / Bảo / Hùng+Nam | H03/N02, PG schema |
| P2-B03 EXTEND, 8h | Audit adapters **(5h)** + CI bootstrap **(3h)**, regression/evidence gate | **17/11 minimal CI pipeline; 20/11 integration evidence** / Bảo / Nam+Cường+Đạt | Business metadata của H/N |

### P2-B01 — Single migration integrator và access boundary

**FR-22/23/24, M1–M5, WF-01–06.** Existing files: `be/src/MealTrace.Infrastructure/Persistence/MealTraceDbContext.cs.OnModelCreating/PrepareEnrollments`, `be/src/MealTrace.Infrastructure/Persistence/Migrations/MealTraceDbContextModelSnapshot.cs`, `be/src/MealTrace.Infrastructure/Identity/AuthenticationRegistration.cs`, `be/src/MealTrace.Domain/Security/RoleNames.cs`, `be/src/MealTrace.Api/Program.cs`.

Không endpoint mới riêng cho migration. Existing role groups E1–E9 giữ nguyên. Contract artifact (không HTTP):

```json
{"baseSha":"a6b187e6eae9553f7966d5444b8745f9896d2a11","migrationOrder":["supplier-source-menu","serving-allocation","report-draft"],"snapshotOwner":"BE3","database":"isolated-test-only","rollback":"restore-tested-backup-or-forward-fix"}
```

Input model changes từ Hùng/Nam; output migration/SQL review + FK/unique map + actual PG logs và contract routes. Tách guard history mới khỏi editable current pointers; approval metadata không phá append-only. Xác nhận policy Inspector đọc report bằng grant có hạn, không cho viết/duyệt và không mở toàn audit Admin. Hạn reviewer policy16/11; nếu chưa rõ, đóng deny-default và ghi blocker, không tuyên bố M5 complete.

**Acceptance:** (1) Fresh DB và DB schema hiện tại nâng cùng migrations → tables/FK/index giống model, retained settlements và counts không đổi [FR-23/24/M5/WF-06]. (2) Race 2 correction cùng priorVersion → một201, một409, không partial children; DB unique được chứng minh PG [FR-23/M4/M5/WF-05/06]. (3) Matrix new write endpoints anonymous401/P403/T403, Admin/K theo contract, Inspector không ghi; existing ack Admin403 còn nguyên [FR-22/M1–M5/WF-01–06]. Migration không có HTTP nên test1 đối chiếu schema/data, không bịa status200.

### P2-B02 — Report draft dùng actual lineage

**FR-06/23/24, M5, WF-06.** EXISTING `GET /api/reports/{id}/lineage` ở `be/src/MealTrace.Api/Features/MealEndpoints.cs`; `be/src/MealTrace.Application/Features/Meals/MealService.cs.GetReportLineageAsync`; `be/src/MealTrace.Infrastructure/Persistence/Repositories/MealRepository.cs.FindReportLineageAsync`; `be/src/MealTrace.Domain/Entities/ReportSnapshot.cs` lưu SourceJson, kcal, legacy cost; không có report generation.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/admin/reports/drafts`; `GET /api/admin/reports/{id}`; `GET /api/admin/meal-days/{mealDayId}/source-trace`. Existing `/api/reports/{id}/lineage` giữ compatibility, mở rộng typed references bằng additive fields hoặc adapter, không phá clients lookup.

```json
{"periodStart":"2026-11-19","periodEnd":"2026-11-19","classIds":["classA"],"mealTypes":["Lunch"],"mode":"actual"}
```

→201 `{id,versionId,state:"draft",settlementIds:["v2"],servingVersionIds:["serving-v1"],hasSourceGap:false,totals:{...},lineage:{...}}`. Trace GET200 `{mealDayId,classes:[{classId,servings:[{versionId,dishes:[{recipeVersionId,ingredientUses:[{quantity:1000,unit:"g",allocations:[{sourceEntryVersionId:"sourceA-v1",supplierVersionId:"supplierA-v1",quantity:600},{sourceEntryVersionId:"sourceB-v1",supplierVersionId:"supplierB-v1",quantity:400}],sourceGap:null}]}]}]}]}`. Unknown ID404; invalid period400; no actual serving422 rõ code.

**Rules:** A tạo draft; A/Inspector grant scoped đọc khi policy được chốt. Snapshot các version IDs/values/names/quantities/rules/time, không chỉ JSON chứa live FK không đóng băng. Tổng latest settlement theo từng class; actual served portions là metric riêng, không thay denominator bằng history sum. Tính report nutrition theo actual recipe/actual-use và basis đã chốt; planned numbers chỉ label riêng. Draft thiếu source vẫn trace gap; không có approver/export-as-approved ở P2. Không đưa cost/fee ledger vào scope vì ReportSnapshot có CostPerPortion cũ.

**Migration:** draft/report period + source-reference model mới hoặc mở rộng có adapter legacy; một ReportSnapshot.SettlementId không đại diện nhiều lớp/cả tháng. Không backfill legacy SourceJson thành “complete”. Atomic read snapshot/selection consistency; lưu input version identities trước khi calculate, chống correction giữa chừng.

**Acceptance:** (1) Class A settlements10→12 và B5, actual A12/B5 → draft settled17, **không27**; trace actual egg600/400 tới đúng hai suppliers [FR-06/M5/WF-06]. (2) Egg sourceA600/gap400 →200 trace/201 draft, hasSourceGap=true và reason; không dùng SourceRef nutrient làm nguồn thiếu [FR-06/23/M5/WF-06]. (3) Sau draft sửa supplier/current recipe/serving → GET draft giữ IDs/values đã chọn; unauthorized Parent403, absent report404 [FR-22/24/M5/WF-06].

### P2-B03 — Mở rộng audit và chứng minh integrated slice

**FR-22/23, M5 và xuyên module, WF-06.** EXISTING `GET /api/admin/audit-logs`, `/{auditId}`, `/entities/{entityType}/{entityId}`, `/export`. Reuse `be/src/MealTrace.Application/Features/Audit/AuditService.cs.ListAsync/ExportAsync`, `AuditPrivacy.cs`, `be/src/MealTrace.Infrastructure/Persistence/Audit/EfAuditSource.cs` và `CoreAuditSources.cs/PortionAuditSources.cs/StudentAuditSources.cs`; đăng ký qua `be/src/MealTrace.Infrastructure/DependencyInjection.cs`.

Không dựng persisted generic AuditLogs engine mới. Thêm adapter nutrient/recipe/ack và H01/H02/N02 source events, với input append-history rows; output JSON giữ contract hiện tại `{id:"source:guid",source,occurredAt,actor,action,entityType,entityId,reason,before,after,correlationId}`. Actor/time không tồn tại trên legacy thì null và flag provenance, không gán current user. Photo/report lifecycle audit bổ sung tiếp P3.

**Role/migration/risk:** audit vẫn A-only; không mở raw audit cho Parent/Inspector để phục vụ report. Adapter thường không migration; nếu business row thiếu prior values phải yêu cầu owner thêm immutable history qua B01. AuditPrivacy allowlist phải thêm source refs cần thiết nhưng không lộ contact/phone/token/secret. Giữ CSV escaping/range31days/limit10.000 và paging ổn định.

**Acceptance:** (1) SourceA v1→v2 và serving v1→v2 → GET timeline200 có cả prior/current quantities, actor/reason/time đúng DB, report trace còn chọn v1 khi đã snapshot [FR-23/24/M5/WF-06]. (2) Reason chứa dấu phẩy, quote, newline và `=SUM(1,1)` → CSV200 parse đủ cells, formula vô hiệu, không lộ phone/credentials; P/T/K export403 [FR-22/23/M5/WF-06]. (3) Replay exit gate §2 trên PG với API thật, nộp logs SHA/test results và FE/Android handoff receipts; skipped PG không đạt gate [FR-06/15/23/M3/M4/M5/WF-02/05/06].

## 6A. Lead-controlled integration và CI bootstrap (không thêm scope)

- **16/11 12:00:** chốt schema/DTO/IDs tối thiểu cho SupplierVersion, SourceEntryVersion, MenuVersion, ServingVersion, source gap, ReportDraft và Photo; route NEW chỉ có tính dự kiến đến khi OpenAPI triển khai. Cường/Đạt ký nhận error contract. Chốt initial plan cho PDF dependency và grant Inspector, tránh đợi P3.
- **17/11 17:00:** Bảo đưa pipeline tối thiểu lên working integration branch: restore/build/backend tests + PostgreSQL service **biệt lập** và artifact TRX; Đạt/Cường chuẩn bị lệnh FE/Flutter test riêng. Đây là **CI skeleton**, chưa tính đủ 24 case. Nếu runner chưa chạy, nêu blocker ngay.
- **18/11:** Hùng cung cấp source IDs/version APIs, Nam commit read/write graph theo fixture; Bảo tích hợp migration #1 rồi #2 theo thứ tự (không song song snapshot). Bảo phát triển query/report model sớm từ contract, không chờ Nam xong rồi mới bắt đầu 20h.
- **19–20/11:** Hùng publish menu, Nam ghi serving/use/allocations, Bảo đọc actual graph vào report draft; cùng chạy một case PostgreSQL, giữ nguyên source references và log SQL/migration.
- **Source gap approval:** 16–17/11 leader/reviewer ghi một trong hai policy: (A) chặn approve khi incomplete với 422 hoặc (B) cho approve with-disclosure và actor/reason rõ. **BM04 chỉ trực tiếp yêu cầu explicit gap**, không tự biến (A) thành quy định nếu chưa thống nhất.
- **Phân tải:** CI/30-case mapping và 13 BM02 High không dồn cho Bảo ngày27: Đạt phụ trách SRS/FE/diagram WF01, Hùng ERD+nutrition WF02, Nam state/workflow WF03–04, Cường offline E2E WF05, Bảo tích hợp/trace WF06 và evidence index.

## 7. Daily gates và test ledger

| Ngày | Hùng | Nam | Bảo | Client/human gate |
| --- | --- | --- | --- | --- |
| Mon 16/11 | Source/menu DTO + schema; reference request | Canonical meal types + photo contract; cutoff repro | Freeze contracts/role map/migration queue | Đạt bắt đầu FE menus; Cường bắt đầu durable queue. Reviewer chốt discrepancies BM04, không chờ endpoint xong mới làm UI |
| Tue 17/11 | H01 source schema + IDs frozen (full APIs 18/11) | N01 reproducible tests/risk triage | Migration1 + CI skeleton running | PostgreSQL private DB; Cường queue and Đạt UI contract started; reference request escalated |
| Wed 18/11 | H01 APIs + H02 state machine/API skeleton | N02 serving graph + allocations scaffold | Report draft reader/code against contract | Đạt connects available menu operations; Cường local restart queue |
| Thu 19/11 | H02 menu publish; H03 calculation frozen in progress | N02 live graph/write with split sources | Audit adapters + report draft integration | Trace600+400 bằng API thật; Android selection có thể qua staging fixture trước khi API ổn |
| Fri 20/11 | H03 pinned publication + estimate handoff | N02 commit/read/idempotency và minimal M2 fixes | PG vertical slice + CI artifact/evidence pack | 17:00 exit gate; BM04 CI due20/11 đã cam kết, nếu chưa đạt ghi overdue, không tự đổi signed date |
| Sat 21/11 | Chỉ sửa lỗi gate trong scope | Chỉ sửa race/rollback/API mismatch | Migration/replay fixes | Không thêm feature tùy chọn; ưu tiên blocker đã phân công |
| Sun 22/11 | Retest phần sửa | Retest phần sửa | Freeze P2 integration SHA | 17:00 pass hoặc báo đỏ deadline06/12; không tự nhận P2 xong |

BM04 ledger giữ nguyên: Đạt WF-01 **5/4 CI**, Hùng WF-02 **5/4**, Nam WF-03 **4/3** và WF-04 **4/4**, Cường WF-05 **6/4**, Bảo WF-06 **6/5** = **30 unique /24 CI**. Detailed IDs ở [P3 §7](backend-task-assignment-phase3-v1.4.md). Acceptance task có thể là subassertions/regression bổ sung; không đếm lặp vào 30. Nam viết API tests cho photo nhưng Cường vẫn accountable toàn bộ 6 WF-05 cases; Bảo điều phối CI không lấy test ownership FE.

### Verification checklist bắt buộc cho mỗi PR

- [ ] Base SHA và upstream compare mới; task ghi rõ phần reuse vs NEW; generated OpenAPI đúng route/DTO/error codes, clients nhận thay đổi.
- [ ] Chạy 1–3 acceptance trong card với input/expected output/status/history; ghi command, timestamp UTC+7, SHA, DB/provider, pass/fail/skip. Tests cùng tên nhưng skip không là bằng chứng.
- [ ] Negative role/scope và stale/retry đúng; report-critical rows/version IDs/actor/reason không bị overwrite; không thêm scope purchasing/stock.
- [ ] Bảo review migration/unique/FK/rollback; actual PG transaction/race khi task có concurrency; không chỉnh snapshot song song.
- [ ] Baseline regression liên quan không mất; code ở branch chỉ “ready for review”, chỉ sau merge mới `VERIFIED_IN_MAIN` cùng execution evidence.
- [ ] Handoff fixture/API thật cho Đạt/Cường; screenshot/demo logs tự sinh trong implementation run, không tự nhận có trong audit này.

## AUDIT LIMITATIONS

Baseline chỉ gồm 278 non-PG pass; 34 skip và chưa FE/Flutter/device/staging. P2 endpoints mới, schema tên đề xuất và JSON là contract để review, **chưa tồn tại tại SHA audit**. Các legacy cutoff/recorded-time hazards cần repro trước fix. Supplier/age policy, Inspector và thiếu nguồn khi approve cần reviewer xác nhận; không lấy tài liệu v1.3 để phủ định v1.4. BM04 §6/§7/§8 có mâu thuẫn số lượng/ownership/due dates đã ghi trong audit.

## NEXT HUMAN ACTIONS

1. Bảo phân issues P2-H01…H03, P2-N01…N03, P2-B01…B03 đúng PR owners; đặt 16/11 12:00 contract freeze và 20/11 17:00 integrated gate.
2. Đặt lịch Đạt/Cường đủ 5 ngày P2 và 10 ngày P3; cung cấp PG biệt lập/private storage/device, không gửi secret trong issue.
3. Reviewer/trường chốt source/date/unit/reference-age, Inspector policy và BM04 ledger; ký nhận việc recovery dates không tự thay deadline đã cam kết.
4. Mọi thiếu hụt gate20/11 phải có người sửa/hạn weekend; không bỏ phần mandatory ở [Phase 3](backend-task-assignment-phase3-v1.4.md) để làm đẹp báo cáo tiến độ.

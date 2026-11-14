# MealTrace — Phân công Backend Phase 3 theo v1.4

> **LEAD REVIEW — 15/11/2026, awaiting final team sign-off.** This document is a consolidated *execution plan*, not a commit, a passing acceptance report or an approval to change BM04. Technical baseline: `origin/main@a6b187e6eae9553f7966d5444b8745f9896d2a11`; Codex ran backend tests on this SHA (278 passed / 0 failed / 34 skipped). Re-fetch and reconfirm before coding. See [Lead decisions](phase2-phase3-lead-decisions-v1.4.md).


**23/11–06/12/2026**; core days **23–27/11 và 30/11–04/12**, cuối tuần 28–29/11 là buffer tự nguyện, 05–06/12 chỉ contingency/release gate. Baseline audit: `origin/main` **`a6b187e6eae9553f7966d5444b8745f9896d2a11`**, 15/11 UTC+7; **278 pass, 0 fail, 34 skip**, chưa PG/device/staging. Các task dưới đây **chưa được triển khai trong audit**, chỉ được mở sau khi đối chiếu SHA tích hợp P2 thực tế.

Nguồn chuẩn: [registration v1.4](review2/Phieu_FA26SE270_V1.4.docx), [BM04](review2/SEP490_FA26SE270_BM04_Review2_Commitment_1.docx) §3/6/8/9. Đọc cùng [audit](phase2-phase3-rebaseline-audit.md) và [P2 contracts](backend-task-assignment-phase2-v1.4.md). Mục tiêu: hoàn thành FR mandatory còn thiếu, CI và staging/device regression; **FR-21 optional**. Không thay photo offline bằng online demo, PDF bằng CSV, hoặc actual sources bằng ingredient master.

## 1. Ownership, capacity và điều kiện bắt đầu

Hùng BE1 chịu WF-02/M3; Nam BE2 chịu WF-03+04/M2 và serving/photo APIs cộng tác; Bảo BE3 chịu WF-06/M5 và điều phối CI, **migration integrator duy nhất**. Cường giữ WF-05/M4, Flutter queue/tests/device; Đạt giữ WF-01/M1, FE workflows/tests. Tất cả test được đếm một owner theo §7; API helper test không chuyển quyền sở hữu workflow.

P2 phải có menu published + two source entries + latest settlement + actual split serving + report draft trace trên PG. Nếu còn thiếu, ghi cụ thể vào risk/owner/capacity; không giả định P2 code đã merge vì task plan tồn tại. Ngày 23/11 09:00 kiểm tra `origin/main`, migrations và actual test logs mới, không tiếp tục dùng SHA15/11 làm release SHA.

Capacity đề xuất vẫn **40h task/review đã ước tính trên mỗi BE** ở P3: Hùng12+12+12 và4h review, Nam20+8+8 và4h review, Bảo20+8+8 và4h review. Lịch Phase 3 kéo thành **10 ngày làm việc/người (tối đa 80h)**; phần tăng thêm dành cho integration, PostgreSQL, CI, FE/Android handoff, sửa defect và tài liệu đã cam kết, **không thêm scope/task mới**. Cường cần **10 ngày P3** cho queue/device/serving-source UI, Đạt **10 ngày P3** cho menus/estimates/approvals/report diff/export UI và WF-01 regression. Cần trường/reviewer xác nhận reference trước26/11 và staging nghiệm thu04/12. Đây là điều kiện nhân lực, không được tính phần client vào ba BE hoặc coi weekend là hai ngày code mặc định.

## 2. Business gates phải đóng

- Ingredient estimate = published menu version + recipe quantities/basis/conversion đã chọn + **latest settlement từng class**. Là kế hoạch, không mua hàng/trừ kho; xuất list rõ unit/source inputs.
- Actual report dùng recorded ingredient-use và source allocations của **served version**, tách planned vs actual. Tổng năng lượng/macros thực dùng tính từ actual quantity × conversion × edible fraction × nutrient/100g; per-serving chia actual served portions theo dish/session rõ ràng. Nếu một session có nhiều dish, không cộng số portions của từng dish rồi gọi đó là số trẻ; report giữ `settledPortions`, `servedPortions`, `dishPortions` riêng.
- Approved menu/report/serving/source versions không overwrite; source master đổi không làm lịch sử drift. Actual corrections tạo successor version; report Recomputed mới giữ old/new input IDs, exact deltas và reasons.
- Ảnh private, linked-child scope tại meal/class và verified link còn hiệu lực; approval và quyền phải kiểm tra cả **bytes retrieval**, không chỉ list. Không tái dùng public PhotoUrl làm bảo mật.
- ≥95% overall, **100% mandatory main paths**, 6/6 main +6/6 exception demos, **0 open Critical/High**, 30 unique cases/24 CI theo BM04 §8. 278 baseline pass không thay các gates này.

## 3. BE1 — Hoàng Quốc Hùng

| ID / loại | Đầu ra | Due / PR owner / reviewer | Phụ thuộc |
| --- | --- | --- | --- |
| P3-H01 NEW, 12h | Ingredient estimate calculation và CSV | 25/11 17:00 / Hùng / Nam+Bảo | P2-H02/H03, N01 latest settlements |
| P3-H02 EXTEND, 12h | Reference mầm non có nguồn và frozen nutrition | 26/11 17:00 / Hùng / Bảo+trường | Reference được duyệt, P2 publication snapshot |
| P3-H03 HARDEN, 12h | Menu/source histories, legacy nutrition correction, accuracy/PG | 30/11 17:00 / Hùng / Nam+Bảo | H01/H02, P2-H01/H02 |

### P3-H01 — Estimate từ published menu và latest portions

**FR-16/12/14, M3/M2, WF-02/04.** EXISTING: `GET /api/kitchen/settlements/{id}/diff`, `GET /api/kitchen/recipes/{id}/versions/{v}/nutrition`. Reuse exact files `be/src/MealTrace.Application/Features/Kitchen/RecipeService.cs` (`RoundingOf`, stored quantities), `NutritionService.cs` (`Compute` semantics), `KitchenSettlementService.cs` (`GetSettlementDiffAsync`), `be/src/MealTrace.Infrastructure/Persistence/Repositories/RecipeRepository.cs`, `be/src/MealTrace.Domain/Entities/RecipeIngredient.cs`, `RecipeVersion.cs`. Không tính từ mọi dòng PortionSettlements.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/kitchen/ingredient-estimates`; `GET /api/kitchen/ingredient-estimates/{id}`; `GET /api/kitchen/ingredient-estimates/{id}/export?format=csv`. Module mới dự kiến `be/src/MealTrace.Application/Features/Kitchen/IngredientEstimateService.cs` — **chưa có tại SHA audit**.

```json
{"menuVersionIds":["menu19-v1"],"mealDayIds":["lunch19"],"classIds":["classA"],"snapshotTokens":{"lunch19":"64hex"}}
```

→201 `{id,createdAt,menuVersionIds,settlementIds,lines:[{ingredientId,quantity,unit,grossGrams,edibleGrams,recipeVersionIds,nutrientVersionIds,conversion}],isPlanningEstimate:true}`. GET/CSV200 cùng stored inputs; stale token409; missing published menu/settlement hoặc conversion422; anonymous401, T/P403; A/K allowed.

**Rules/minimal change:** quantity line của recipe là lượng cho servingBasis; scale bằng latest class counts, chuyển `ml` bằng density pinned, áp edible một lần. Khi recipe dùng edible quantity và cần gross requirement, chia edible fraction; fraction0/unknown →422, không divide-by-zero hoặc suy đoán. Round sau aggregate, trả unit/basis rõ; thiếu legacy unit/density →explicit gap/422. Version estimate bất biến; amendment mới tạo estimate mới, không sửa file cũ. Không cộng receipts thành tồn kho và không gọi output purchase order.

**Migration/risk:** additive estimate snapshot + input reference rows hoặc canonical JSON/version IDs; Bảo generate. Không dùng Down xóa estimate đã xuất, rollback restore rehearsal/forward-fix. Giữ existing recipe response compatibility.

**Acceptance:** (1) Recipe100g/serving, class A settlement18→20, classB10: estimate3.000g, không4.800g; CSV cùng số/IDs [FR-16/M3/WF-02]. (2) Recipe100ml/serving density0.9, 20 portions →1.800g gross; thiếu density422 `UNIT_CONVERSION_MISSING`; old estimate hash giữ nguyên sau density correction [FR-12/16/24/M3/WF-02]. (3) Teacher export403, CSV ingredient name có quote/comma/newline/`=1+1` parse đúng và formula vô hiệu; stale token409 không tạo estimate [FR-16/22/M3/WF-02].

### P3-H02 — Nguồn nhóm tuổi và calculation freeze

**FR-12/20/24, M3/M5, WF-02/06.** EXISTING `GET /api/kitchen/recipes/{id}/versions/{v}/nutrition?mode=&ageGroup=&validAt=&knownAt=`. Exact files `be/src/MealTrace.Application/Features/Kitchen/NutritionService.cs.CalculateAsync/CompareAsync`, `be/src/MealTrace.Domain/Entities/NutrientReferenceRange.cs`, `be/src/MealTrace.Infrastructure/Persistence/Repositories/NutritionRepository.cs.ListReferenceRangesAsync`, `be/src/MealTrace.Infrastructure/Persistence/Configurations/NutrientReferenceRangeConfiguration.cs`. Range comparator đã có; **không giao viết lại**.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `GET /api/kitchen/nutrition-references?ageGroup=&validAt=&knownAt=`; `POST /api/admin/nutrition-references` (append version, hoặc import seed quản trị có version được review). Chốt một cơ chế tại freeze, không để UI ghi trực tiếp DB.

```json
{"ageGroup":"school-approved-age-code","nutrient":"kcal","minValue":null,"maxValue":null,"basis":"perMeal","mealType":"Lunch","source":"Tên tài liệu đã được trường xác nhận","sourceRef":"section/page/version","effectiveFrom":"2026-11-01T00:00:00+07:00","supersedesId":null,"reason":"Nạp dữ liệu được duyệt"}
```

Đây là **schema minh họa**; cả min/max null không hợp lệ. Fixture thực phải có ngưỡng xác nhận, không lấy ví dụ “Toddler/Council table 2026” trong test làm chuẩn mầm non. Input hợp lệ →201 `{id,version,recordedAt,recordedBy}`; GET200 trả source/basis/range IDs; thiếu group/basis phù hợp →422. A quản lý range; A/K đọc; Parent chỉ thấy comparison đã freeze trong menu publication, không raw quản trị.

**Rules:** căn cứ Preschool Education Programme/nguồn được trường xác nhận theo registration; Decision3958 Appendix7 là food composition, không tự thành preschool age threshold. Phải phân biệt daily vs per-meal và unit kcal/g; không đem một dish so với daily range rồi báo “đạt”. Lưu reference version IDs + values, basis, recipe/nutrient inputs và algorithm/rounding vào menu/report snapshot. `mode=pinned` hiện vẫn chọn ranges theo now: sửa bằng snapshot path, không đổi semantics hiện hữu thiếu migration plan. Missing macro/range trả unknown/422 rõ, không điền0.

**Migration/risk:** bổ sung version/basis/mealType/source metadata nếu cần, giữ rows cũ như legacy unspecified; không mutate existing append-only rows. Cần Bảo migrate trước report freeze. Nếu trường chưa cấp source26/11, FR-12 gate đỏ; fallback trả missing rõ chỉ tránh số giả, **không được tính hoàn thành**.

**Acceptance:** (1) Reference R1 được duyệt và calculation C1 đã publish; append R2 với ngưỡng khác →GET As-Published vẫn R1/result cũ; explicit Recomputed chọn R2 và liệt kê delta/changed reference [FR-12/24/M3/M5/WF-02/06]. (2) Query group không có hoặc lấy daily range cho per-meal không có conversion rule →422; không claim within [FR-12/M3/WF-02]. (3) P/T POST reference403, correction stale409, independent calculation fixture round AwayFromZero đúng các chữ số lưu; old input values không đổi [FR-12/22/24/M3/WF-02].

### P3-H03 — Complete menu/source histories và legacy provenance

**FR-05/11/13/23/24, M3/M5, WF-02/06.** Existing source `be/src/MealTrace.Application/Features/Kitchen/IngredientTimeline.cs`, `IngredientService.cs.AddVersionAsync`, `RecipeService.cs.AddVersionAsync`, `be/src/MealTrace.Infrastructure/Persistence/Migrations/20261028090000_KitchenV13Nutrition.cs`, `MealTraceDbContext.cs.PrepareEnrollments`. Existing `/ingredients/{id}/ingredient-versions`, `/recipes/{id}/versions` giữ nguyên; planned P2 menu/source history routes cần hoàn thiện, không báo là đã có từ HEAD.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** P2 `/api/kitchen/menus/{id}/history`, `/suppliers/{id}/history`, `/ingredient-source-entries/{id}/history`; bổ sung `GET /api/kitchen/recipes/{id}/history` nếu version listing P2 không đủ reason/actor. Input correction `{expectedVersion:1,reason:"Sửa phiếu nguồn",...fullNewValues}` →201 `{version:2,priorVersionId,recordedAt,actor}`; history200 `{items:[{versionId,priorVersionId,actor,recordedAt,reason,before,after}]}`.

**Minimal changes:** chỉ thêm history metadata/event đã thiếu ở P2; publication correction tạo version mới rồi review, không direct edit published. Đối chiếu legacy recipe lines có ingredient Unit=ml nhưng Quantity/Unit null; **không backfill1ml=1g**. Đánh dấu `legacyConversionUnverified`, ngăn xuất bản mới khi chưa có correction data; bản cũ vẫn replay đúng recorded values và ghi limitations. Source supplier/name/contact corrections append revisions và bảo toàn actual allocation/report references; không sửa source v1 khi v2 được tạo.

**Security/migration:** A/K đúng routes; Parent chỉ published read đã scope. Cung cấp Bảo schema histories và allowlist fields, không exposed contact qua parent response. Nullable metadata cho legacy, không fabricating actor/date/ref; migration rehearsal lưu before/after checksum.

**Acceptance:** (1) Publish menu v1, reject v2 rồi sửa v3/publish →history đầy đủ transitions/reasons; v1 nutrition/source values giữ nguyên [FR-05/13/23/M3/WF-02]. (2) Legacy ml line thiếu conversion bị flag/422 lúc new publication; correction có density bằng actual source →new version, old row/approved report hash không đổi [FR-11/12/24/M3/M5/WF-02/06]. (3) PG simultaneous ingredient/source corrections cùng prior →một201, một409; history không branch trái phép, unauthorized Parent403 [FR-23/22/M3/WF-02].

## 4. BE2 — Nguyễn Duy Nam

| ID / loại | Đầu ra | Due / PR owner / reviewer | Phụ thuộc |
| --- | --- | --- | --- |
| P3-N01 NEW, 20h | Photo upload/retry/review/private guardian bytes | **23/11 17:00 staging upload minimum; 27/11 17:00 all photo functions** / Nam / Cường+Bảo | P2-N02/N03, storage riêng, Cường queue song song |
| P3-N02 EXTEND, 8h | Serving/substitution/source histories và correction consistency | 30/11 17:00 / Nam / Hùng+Bảo | P2-N02, H03, B01 report snapshot |
| P3-N03 VERIFY/FIX, 8h | PostgreSQL cutoff/amendment/ack/race regressions | 02/12 12:00 / Nam / Bảo | PG riêng, P2-N01, N01/N02 |

### P3-N01 — Offline evidence API và private retrieval

**FR-05/17/20/22/23, M4, WF-05 owner Cường.** Existing files `be/src/MealTrace.Domain/Entities/MealEvidence.cs`, `be/src/MealTrace.Application/Dtos/Meals/MealEvidenceSummary.cs`, `be/src/MealTrace.Application/Features/Meals/MealService.cs.GetMealDayAsync`; no upload/review/private bytes route tại audit SHA. Client integration contract ở `mobile/lib/features/school/data/school_repository_impl.dart`; **Cường** implement Flutter và tests, Nam không nhận cả app.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/kitchen/servings/{id}/photos` multipart; `GET /api/kitchen/photos/{id}`; `POST /api/admin/photos/{id}/review`; `GET /api/parent/students/{studentId}/photos`; `GET /api/parent/students/{studentId}/photos/{photoId}/content`. New file dự kiến `be/src/MealTrace.Api/Features/EvidenceEndpoints.cs`, service `be/src/MealTrace.Application/Features/Meals/EvidenceService.cs` — proposed, không phải existing evidence.

```json
{"clientPhotoId":"uuid","servingVersionId":"serving-v1","capturedAt":"2026-11-25T11:05:00+07:00","sha256":"64hex","contentType":"image/jpeg"}
```

File part chứa bytes, metadata ở JSON part. First201 `{id,state:"synced",clientPhotoId,capturedAt,receivedAt,servingVersionId}`; same payload retry200 same id; hash mismatch409. Review `{decision:"approve|reject",expectedVersion:1,reason:"..."}` →200 `{id,state,reviewedBy,reviewedAt,version}`. Parent list200 chỉ approved; private content200 image bytes, no-store, safe headers; anonymous401, unlinked/revoked/unapproved403, missing ID404 theo policy không leak metadata.

**Rules/security:** K upload/own serving permission; A approve/reject; parent child link còn active và enrollment meal/class phù hợp. Kiểm tra authorization lại khi GET bytes, expiry/revocation có hiệu lực; không public object URL/cache công cộng. Inspector report grant không tự cho quyền ảnh trẻ. JPEG/PNG≤10MiB theo P2 contract, sniff/decode content, reject spoof type/path traversal, bỏ EXIF location khỏi bản hiển thị theo policy và checksum phân biệt original/served object. Không nhận URL để server tải hộ. CapturedAt client giữ nguyên với skew flag; receivedAt server, không tin capture time để vượt cutoff/quyền.

DB unique idempotency scoped clientPhotoId + actor/serving, stored hash. File+DB không có distributed transaction: dùng trạng thái staging/finalized, retry recover orphan và cleanup riêng có log; chỉ trả synced khi object durable/checksum đúng. Cường giữ local file và metadata/key qua kill/restart; retry backoff, 401 reauth, 409 conflict cần xử lý, không bỏ queue khi mất response. Approval rejection giữ record/reason, upload correction thành evidence version mới; không overwrite ảnh approved.

**Migration/rollback:** additive objectKey/hash/state/review/actor/version tables hoặc mở rộng entity có adapter legacy PhotoUrl. Link legacy URL không được mặc nhiên approved/public. Bảo integrate migration, storage bucket/private directory permissions. Rollback không delete approved evidence; backup cả metadata+objects, restore test. Handoff Nam cung cấp auth/limits/examples và mock-free staging từ23/11, đủ photo review/private bytes trước27/11; Cường smoke-test device 26/11 và chạy nghiệm thu thiết bị 03/12.

**Acceptance:** (1) Cường airplane mode chụp, kill app/relaunch, reconnect; Nam simulate commit thành công nhưng response mất, retry3 lần →1 DB evidence/1 final object, capturedAt không đổi, receivedAt server, local queue done sau receipt [FR-17/M4/WF-05]. (2) A approve, P đúng child GET200 bytes; unrelated/revoked/pending/rejected403, anonymous401; đo trực tiếp object URL không đọc được public [FR-05/20/22/M4/WF-05]. (3) Same key khác hash409; spoof media/oversize400/413 theo OpenAPI; không orphan published object, reject giữ history/reason và ảnh đã approved trước không đổi [FR-17/23/M4/WF-05].

### P3-N02 — Serving corrections không làm đổi report lịch sử

**FR-15/23/24, M4/M5, WF-05/06.** Reuse planned P2 serving routes; actual existing files là `be/src/MealTrace.Application/Features/Kitchen/KitchenSettlementService.cs` `GetSettlementDiffAsync/Token`, `be/src/MealTrace.Application/Features/Portions/PortionAmendmentService.cs.ReviewAmendmentAsync`, `be/src/MealTrace.Domain/Entities/MenuDish.cs`, `MealEvidence.cs`, `be/src/MealTrace.Infrastructure/Persistence/MealTraceDbContext.cs`. Các files ServingService/entities mới của P2 phải lấy từ merge SHA thật, không suy tồn tại ở audit SHA.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/kitchen/servings/{id}/versions`, `GET /api/kitchen/servings/{id}/history` và read P2. Correction input full graph + `{expectedVersion:1,reason:"Thay món/đính chính nguồn",priorVersionId:"serving-v1"}` →201 `{versionId:"serving-v2",version:2,priorVersionId:"serving-v1",hasSourceGap:false}`; history200 includes old/new actual recipe/use/source metadata; stale409.

**Minimal changes:** đảm bảo mọi correction actual quantity/source/supplier revision/dish substitution giữ prior values và responsible actor/session/class, không update allocations v1; valid-time và recorded-time riêng nếu correction hồi tố. Recipe/nutrient/sourceEntry identities không thay lẫn nhau. Session serving count/actual dish portions validate rõ để report không đếm trẻ hai lần khi có hai món. Chênh lệch settlement/served giữ reason, không sửa settlement. Báo cáo đã approve tham chiếu v1, report recompute mới chọn v2 theo requested knownAt.

**Migration/security:** DB uniqueness priorVersion/version và transaction graph; Bảo tích hợp guards/adapters; không dùng cascading delete lịch sử. K write/A read, teacher/parent denied write. Bảo review trace chỉ trả supplier evidence phù hợp, không raw contacts cho Parent.

**Acceptance:** (1) Serving v1 egg1000 split600/400; v2 egg1100 split600/500 + reason →201, v1 vẫn1000; As-Approved trỏv1, Recomputed trỏv2 [FR-15/23/24/M4/M5/WF-05/06]. (2) Thay egg dish bằng actual recipe khác, thiếu reason400, có reason201 lưu planned+actual recipe IDs; hai dishes20 portions mỗi dish vẫn session20, không40 [FR-15/06/M4/M5/WF-05/06]. (3) PG correction race →1success/1conflict, không partial allocation; Parent403; GET history actor/time/reason/prior ID đầy đủ [FR-22/23/M4/WF-05].

### P3-N03 — Chứng minh PostgreSQL và bảo vệ vận hành

**FR-03/04/08/09/14/19/22, M2, WF-03/04.** EXISTING routes calendar/generate, exceptions, `/api/meal-days/{id}/settle`, amendments review, kitchen diff/ack giữ contract. Files exact: `be/src/MealTrace.Api/Workers/MealSettlementWorker.cs.ProcessPendingSettlementsAsync`; `be/src/MealTrace.Application/Features/Workflow/WorkflowService.cs.SettlePortionsAsync`; `be/src/MealTrace.Infrastructure/Persistence/Repositories/MealDecisionRepository.cs`; `be/src/MealTrace.Infrastructure/Persistence/Repositories/EnrollmentQueries.cs`; `be/src/MealTrace.Application/Features/Kitchen/KitchenSettlementService.cs.AcknowledgeSettlementAsync`.

Test hiện có cần chạy: `be/tests/MealTrace.Api.Tests/PostgresConcurrencyTests.cs`, `SchoolTimeConcurrencyTests.cs`, `KitchenSettlementRegressionTests.cs`, `PortionAmendmentTests.cs`, `MealSettlementWorkerTests.cs`. Bổ sung case còn thiếu từ P2-N01, **không viết lại tests đã đủ rồi gọi là feature mới**. Input/output như P2, đặc biệt ack `{snapshotToken,note}` →201 same ack on retry, stale409. Existing amendment request `{"classId":"classA","studentId":"childA","baseSettlementId":"settlement-v1","willEat":true,"reason":"Điều chỉnh","studentIds":null,"quantity":null}` →200 amendment response; existing review `{"approve":true,"reason":"Đã kiểm tra"}` →200 reviewed response. DTO thực là `be/src/MealTrace.Application/Dtos/Portions/RequestInput.cs` và `ReviewInput.cs`; không thêm expectedVersion giả vào route hiện hữu khi baseSettlementId đã là concurrency source.

**Implementation chỉ khi test chứng minh defect:** chỉnh query recorded-time/locks/conflict mapping tối thiểu; dùng PostgreSQL test connection chỉ DB biệt lập, test factory random schema, không production. Kiểm tra same-source concurrent amendment, worker nhiều instance/restart, parent/teacher chờ lock vượt cutoff, latest counts và no invented roster. Giữ400/403/409 đúng existing contracts, không catch-all thành success. Bảo là migration owner nếu phải thêm DB constraint.

**Acceptance:** (1) PG worker2 instances + retry/restart tại21:00/07:00 →one initial settlement mỗi class/session; late absence/transfer không ảnh hưởng cutoff snapshot, cancelled session không chốt [FR-03/19/M2/WF-03]. (2) Concurrent approval cùng source version →1applied/1conflict, latest count đúng, old snapshots intact; stale ack409/fresh201/retry same ID [FR-04/09/14/M2/WF-04]. (3) Teacher ngoài lớp403, Admin ack403, Parent sửa sau lock chỉ tác động session còn mở; test không skip, logs provider PostgreSQL và SHA [FR-08/19/22/M2/WF-03/04].

## 5. BE3 — Châu Quốc Bảo

| ID / loại | Đầu ra | Due / PR owner / reviewer | Phụ thuộc |
| --- | --- | --- | --- |
| P3-B01 NEW/EXTEND, 20h | Monthly approve + immutable As-Approved/Recomputed | 01/12 17:00 / Bảo / Hùng+Nam | P2-B02, H02, N02 |
| P3-B02 NEW, 8h | CSV/PDF report, Inspector scoped read, trace completeness | 02/12 17:00 / Bảo / Đạt+Cường | B01, source histories, private policy |
| P3-B03 RELEASE, 8h | **CI completion (skeleton created P2)**, security regression, RC evidence consolidation; BM02 owners supply their own fixes | 04/12 17:00 / Bảo / cả5 owners | Các task, staging/device/PG resources |

### P3-B01 — Monthly report approval và immutable comparison

**FR-06/23/24, M5, WF-06.** Existing source `be/src/MealTrace.Domain/Entities/ReportSnapshot.cs` chỉ one meal/settlement + SourceJson; `be/src/MealTrace.Infrastructure/Persistence/Configurations/ReportSnapshotConfiguration.cs`; `be/src/MealTrace.Application/Features/Meals/MealService.cs.GetReportLineageAsync`; `be/src/MealTrace.Infrastructure/Persistence/Repositories/MealRepository.cs.FindReportLineageAsync`; `be/src/MealTrace.Infrastructure/Persistence/MealTraceDbContext.cs.PrepareEnrollments` thiếu report guard. Extend P2 draft service, không nhận một GET lineage làm report engine.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `POST /api/admin/reports/drafts` mở rộng monthly; `POST /api/admin/reports/{id}/approve`; `POST /api/admin/reports/{id}/reject`; `GET /api/admin/reports/{id}?view=as-approved`; `POST /api/admin/reports/{id}/recomputations`; `GET /api/admin/reports/{id}/comparisons/{recomputationId}`. Existing `GET /api/reports/{id}/lineage` backward-compatible adapter.

```json
{"periodStart":"2026-11-01","periodEnd":"2026-11-30","classIds":["classA"],"mealTypes":["Lunch"],"mode":"actual"}
```

Draft201 `{id,version,state:"draft",inputsHash,totals,completeness}`. Approval `{expectedVersion:1,inputsHash:"sha256",reason:"Đối chiếu đủ"}` →200 `{id,state:"approved",approvedAt,approvedBy,approvedHash}`. Reject reason bắt buộc →200 rejected, sửa tạo draft version mới. Recompute `{validAt:"2026-11-19T12:00:00+07:00",knownAt:"2026-11-26T10:00:00+07:00",reason:"Đính chính"}` →201 `{recomputationId,originalReportId,asApprovedHash,recomputedHash}`; compare200 `{asApproved:{...},recomputed:{...},delta:{kcal:100,...},changedInputs:[{oldId,newId,reason}],sourceChanges:[...],hasSourceGap:false}`.

**Rules:** monthly selection giữ IDs toàn bộ latest settlements từng meal/class ở lúc draft, actual serving/source revisions/nutrient/recipe/reference/rule versions, rounded totals và unrounded inputs đủ replay. Approve atomic recheck draft version/input consistency; correction trong lúc approve phải409 hoặc snapshot nhất quán, không mixed versions. Report totals tách settled/served/nutrition per-serving, không sum settlement history, không lấy planned source thay actual. As-Approved đọc frozen result, không gọi live calculator; replay kiểm tra snapshot inputs với algorithm version. Recomputed là artifact mới, không sửa approved JSON/hash/export.

**Missing-source approval — pending reviewer decision:** draft/trace luôn hiển thị chính xác gap. BM04 yêu cầu "actual supplier or explicit gap", **không tự suy ra chỉ có rule chặn approve**. Trước 17/11, reviewer/lead chốt **A: reject incomplete (422 `REPORT_SOURCE_INCOMPLETE`)** hoặc **B: approve with explicit disclosure + reason + incomplete flag**. Test phải theo đúng một policy đã ký, không bao giờ trả complete giả. Không tự sửa BM04 để hợp thức hóa policy. Retrospective supplier-name-only correction phải hiển thị source diff dù nutrient delta0.

**Role/migration:** A generate/review; Inspector grant read-only đúng scope; K/T/P denied admin reports. Add monthly report/versions/recomputation/reference tables hoặc model tương đương; old ReportSnapshot adapter giữ legacy flag. Approved rows+children immutable guards/FK Restrict; separate lifecycle events tránh mutate frozen values. Bảo duy nhất generate snapshot; migration/backup restore và checksum approval evidence bắt buộc.

**Acceptance:** (1) S3 seed ở §8, approve report R1 →200 hashH1; nutrient correction100→110kcal/100g + same input1000g, recompute →201 delta+100kcal, R1 vẫn1000/hashH1; source IDs và rule replay đủ [FR-06/24/M5/WF-06]. (2) SourceA supplier correction không đổi macros →comparison sourceChanges có old/new/reason, delta0; R1 giữ old supplier name/ref [FR-23/24/M5/WF-06]. (3) Missing allocation400g →draft201 hasSourceGap=true; approve theo **policy A/422 hoặc policy B/disclosed incomplete** đã chốt, không được im lặng chấp thuận; stale expectedVersion409; wrong role403; reject/revise/resubmit giữ lịch sử [FR-06/22/23/M5/WF-06].

### P3-B02 — Report CSV/PDF và complete source lineage

**FR-06/22/24, M5, WF-06.** EXISTING audit export `GET /api/admin/audit-logs/export` và `be/src/MealTrace.Application/Features/Audit/AuditService.cs.ExportAsync/Cell`, `AuditPrivacy.cs`: reuse patterns/escaping, **không gọi audit CSV là nutrition report**. Existing lineage files ở B01 và RoleNames.ReportReaders chỉ Admin; grant entity `be/src/MealTrace.Domain/Entities/InspectorGrant.cs`, token validation `be/src/MealTrace.Infrastructure/Identity/AuthenticationRegistration.cs` chưa đủ scoped report authorization.

**PROPOSED ROUTE — CONFIRM WITH OPENAPI:** `GET /api/reports/{id}/export?format=csv|pdf&view=as-approved`; `GET /api/reports/{id}/source-trace`; `GET /api/reports/{id}/comparisons/{recomputationId}` read-only policy. Admin workflow endpoints vẫn A-only; scoped read grant không mở audit/account writes. Response input query, output200 `text/csv` hoặc `application/pdf`, Content-Disposition filename an toàn, reportId/version/approvedHash/As-Approved label. Invalid format400, unapproved export-as-approved409, scope403/expired auth401 hoặc403 theo auth policy đã ghi OpenAPI.

**Rules:** template PDF tối giản nhưng đủ tháng/lớp/bữa, totals/units/rules, approval time/user, sources/gaps, ID/hash; font tiếng Việt và phân trang không cắt bảng. CSV stable order/invariant decimal/UTF-8, escape comma/quote/newline và neutralize formula. Xuất lại cùng report version cho cùng content nghiệp vụ; timestamp sinh file không thay approved calculation data. Source trace tới actual dish/use/source-entry/date/supplier revision/staff/class/session, trả explicit gap cho legacy/missing evidence. Requesting recomputed view phải label rõ; không ghi lại As-Approved.

**Security:** Inspector granted resource/date/class scope, expiry server-time, bị revoke mất access; không mượn Admin role để đọc. Parent không có raw report/source contacts; chỉ published nutrition/ảnh theo FR-20. Không truy object bằng source-provided URL; SourceRef chỉ metadata. Report export cache private/no-store. Bảo/Đạt phối hợp UI hai view, không đổi ownership FE.

**Migration/risk:** không cần bảng export nếu snapshot đủ; nếu thêm export metadata thì append-only và reference approvalHash. Không đưa cost/financial module vào output. PDF generation dependency chọn trong implementation review/license, không cài trong planning run.

**Acceptance:** (1) ApprovedR1 →CSV/PDF200 cùng totals/H1/source versions; sửa ingredient/supplier rồi xuất lại vẫn H1/old values; PDF tiếng Việt đủ trang, CSV roundtrip không mất dòng [FR-06/24/M5/WF-06]. (2) Supplier name `=SUM(1,1),"A"\nB` →CSV literal safe field, PDF render đúng; contacts/token/password không xuất [FR-22/M5/WF-06]. (3) Active Inspector grant classA đọc classA200 nhưng classB403, expired/revoked denied; Parent/T/K report403; incomplete trace trả gap rõ, không “complete” [FR-06/22/M5/WF-06].

### P3-B03 — CI evidence, release candidate và BM02 closure

**FR-01–20/22–24, M1–M5, WF-01–06.** Existing files/config: `be/MealTrace.sln`, `be/tests/MealTrace.Api.Tests/MealTrace.Api.Tests.csproj`, `be/tests/MealTrace.Api.Tests/PhaseOneRbacTests.cs`, `AuditTests.cs`, `AuditReviewTests.cs`, `BackendBaselineTests.cs`, `be/scripts/run-backend-baseline.ps1`, `fe/package.json`, `mobile/pubspec.yaml`, `mobile/test/`, `mobile/integration_test/student_profile_e2e_test.dart`. `.github/workflows` chưa có ở SHA audit; CI workflow là **NEW task ở implementation run**, không được sửa trong planning này.

Không endpoint mới cho CI. Artifact contract:

```json
{"releaseSha":"full-sha","runAt":"timestamp+07:00","environment":{"postgres":"version","android":"device/os","dataset":"3-academic-years"},"ledger":{"unique":30,"ci":24,"passed":30,"failed":0,"skipped":0},"demo":{"main":6,"exception":6},"openCritical":0,"openHigh":0,"artifactLinks":[]}
```

Số trong JSON là **expected gate**, không kết quả đã đo. Pipeline backend + PostgreSQL service/DB riêng; FE test/build; Flutter analyze/unit/widget/integration subset phù hợp runner. Cường cấu hình tests của mình và device evidence; Đạt FE tests; Bảo tích hợp CI/reporters. Phải có mapping §7 IDs→test method/CI job/run artifact; lý thuyết nhiều parameters không đếm lại functional case.

Commands dự kiến dùng project config: `dotnet test be/MealTrace.sln --logger trx`, `npm ci` rồi `npm test`/`npm run build` trong fe, `flutter pub get`, `flutter analyze`, `flutter test` trong mobile; device integration theo project config và thiết bị được cấp. Set `MEALTRACE_TEST_CONNECTION` chỉ DB biệt lập; baseline opt-in chỉ khi kết nối an toàn đã xác nhận. Không dùng local config production fallback của script. Capture versions/seeds/latency: class register≤5s, monthly report≤10s trên dataset3 năm với record counts/hardware/concurrency/statistic được trường chốt; test harness tồn tại không là latency pass.

**Minimal fixes/security:** thêm audit adapters/photo/report events còn thiếu vào `DependencyInjection.cs`, maintain privacy allowlist; full negative role/class/guardian test, object URL private, stale correction/approval replay. Chỉ fix regressions có evidence; không broad refactor. **Closure 13 BM02 High là trách nhiệm chia theo nguồn gốc:** Hùng Fix-01/ERD/nutrition, Nam Fix-03/workflow/state, Cường SR-07/offline, Đạt Fix-02/overview và SRS+FE acceptance mapping, Bảo Fix-04/report/CI/evidence index. Mỗi owner có commit/test/diagram link; Bảo tổng hợp, **không tự làm thay 4 người**. BM02 findings chưa xác minh đóng cần reviewer disposition; planning Markdown không tự đóng finding. Reviewer xử lý BM04 counts/NFR/Inspector/overdue dates và Fix-01..04, SR-07.

**Phần legacy cần khép, không giao lại scope fix đã pass:** existing `PUT /api/admin/users/{id}` và `POST /api/admin/students/{studentId}/parents`; sửa tối thiểu `be/src/MealTrace.Application/Features/Accounts/AccountService.cs.UpdateAccountAsync/ReplaceScopesAsync`, `be/src/MealTrace.Application/Features/Workflow/WorkflowService.cs.LinkParentAsync`, repositories tương ứng và audit adapters. Input account hiện có `{fullName,email,roles,status,classIds,studentIds,inspectorAccessUntil,phoneNumber}`; direct link `{email,fullName,phoneNumber,sendRegistrationNotification:false}`; output hiện hữu200 AccountView/ParentLinkedResponse giữ tương thích. Chốt additive `changeReason` cho thay đổi scope với Đạt từ P2; lưu immutable old/new role/class/child links, actor/time/reason trong cùng transaction, không suy before từ danh sách hiện tại đã xóa. Current direct-link authorization theo enrollment đã pass, không rebuild. Policy quyền Teacher sau đổi phân công phải rõ ở historic reads; không cấp lại quyền chỉ vì từng được phân công. Bảo thêm migration events nếu cần và giữ append-only; P/T/K gọi update account403. Legacy event thiếu thông tin để null/provenance gap, không tạo audit giả.

**Capacity cảnh báo:** B03 8h chỉ khả thi nếu CI skeleton đã chạy trong P2 và từng WF owner tự nộp test/document evidence. Audit legacy nếu chưa hoàn tất P2 phải có estimate giờ riêng và cân lại BE capacity; Cần mỗi WF owner dành phần integration/review đã dự trù để map test/run/fix của chính mình; Đạt cập nhật FE reason input, Cường cấu hình Flutter CI/device. Nếu audit legacy hoặc CI chưa có nền làm vượt8h, Bảo báo ngay23/11 và xin bổ sung một BE-day/cộng tác CI tương đương; không dùng nhận định “phase1 đã xong” để bỏ FR-23. Không trừ ngày Android của Cường để giao Bảo thêm implementation không được cam kết.

**Acceptance:** (1) Clean CI từ release candidate SHA, 24 designated CI cases chạy/pass không skip, còn6 case manual/device có actual logs; ledger unique30, overall≥95% và mandatory paths100% [FR-01–20/22–24/M1–M5/WF-01–06]. (2) Migration upgrade+restore trên PG riêng, replay12 stories §8 cùng source SHA, ảnh không mất/trùng, As-Approved unchanged; tất cả6 main/6 exception pass [FR-06/17/24/M4/M5/WF-05/06]. (3) Admin đổi Teacher scopeA→B/direct parent link200 tạo before/after events đúng actor/reason; previous event bất biến, quyền cũ bị chặn theo policy; unauthorized account write403/guardian bytes denied, CSV safe, 0 open Critical/High kèm reviewer sign-off và performance measured results [FR-02/22/23/M1–M5/WF-01–06].

## 6. Daily gates và PR verification

| Ngày | Gate Hùng | Gate Nam | Gate Bảo | Client/reviewer outcome |
| --- | --- | --- | --- | --- |
| Mon 23/11 | Estimate scale fixture; xác nhận reference thật | **Staging upload API tối thiểu 17:00**, durable retry foundation | Monthly draft/approval skeleton trên P2 data; verify CI skeleton still green | Đạt estimate/report UI; Cường nối queue vào staging; 09:00 kiểm P2 SHA |
| Tue 24/11 | Estimate+CSV và frozen snapshot đang triển khai | Upload/retry stable + review/private stream in progress | Approval transaction + immutable input selection; CI cases accumulating | Tiếp tục xin nguồn reference được trường duyệt; Cường reconnect test trên server thật |
| Wed 25/11 | H01 estimate+CSV bàn giao; H02 range freeze còn triển khai | Photo API review/private bytes integration | B01 approve/recompute/diff integration | **Checkpoint nội bộ**; chưa feature freeze; Đạt tích hợp estimate/report UI |
| Thu 26/11 | H02 reference + frozen nutrition chốt | N01 private read + Android smoke-test | B01 immutable report snapshot tests | Cường smoke-test kill/restart/retry; trường xác nhận reference thật |
| Fri 27/11 | H03 history và accuracy regression đang triển khai | N01 full photo functions bàn giao | B01 approve/recompute gần hoàn chỉnh; CI tiếp tục tăng coverage | **Mid-P3 integration check**, không gọi là release candidate; FE/Android xem API thật |
| Sat 28/11 | Buffer tự nguyện cho blocker | Buffer tự nguyện cho blocker | Buffer tự nguyện | Không coi weekend là 8h bắt buộc |
| Sun 29/11 | Buffer / review | Buffer / review | Buffer / review | Không thay đổi BM04 deadline đã ký |
| Mon 30/11 | H03 histories/legacy accuracy hoàn tất | N02 serving/source correction consistency hoàn tất | Report snapshot/approval integrated trên serving versions thật | Đạt/Cường regression data lineage và permission |
| Tue 01/12 | WF-02 PG/accuracy evidence | N02 historical serving regression | B01 monthly approve/recompute/diff complete | Đạt hai view report; owner nộp các fix tài liệu được giao |
| Wed 02/12 | Ingredient estimate + age reference regression | N03 PG cutoff/amendment/ack race tests | B02 CSV/PDF/scoped Inspector/trace complete | **Feature freeze 17:00**; sau đó chỉ fix lỗi và test, không mở scope |
| Thu 03/12 | WF-02 evidence/closure | WF-03/04 evidence/closure, support WF-05 | CI 24 designated cases, security regression và report exports | Cường real-device kill/restart/retry/403; Đạt viewport/UAT; 12 demos rehearsal |
| Fri 04/12 | Sign-off WF-02 | Sign-off WF-03/04 và hỗ trợ Cường | **RC 17:00**, CI30/24 và BM02 closure evidence | Cả5 owners demo6 main+6 exception; staging/performance/reviewer disposition |
| Sat 05/12 | Sửa blocker đã xác nhận | Sửa blocker đã xác nhận | Migration/regression/rebuild RC | Chỉ sửa defect có bằng chứng, retest liên quan |
| Sun 06/12 | Ký nhận module | Ký nhận module | **Release gate 17:00** | GO/NO-GO do người có thẩm quyền; chưa đạt thì no-go có blocker |

Mỗi PR phải có: exact base/head SHA, route existing/proposed rõ, OpenAPI JSON và sample errors, 1–3 acceptance của card với actual input/result, role/scope negative tests, append-only/replay evidence, PG logs nếu liên quan, migration/rollback Bảo review, handoff Đạt/Cường, không duplicate Phase1 implementation. `VERIFIED_IN_MAIN` chỉ sau merge + actual pass; ngoài branch là chưa merged. Không để ba BE sửa EF snapshot.

Các correction tài liệu BM04 §9 phải có owner và bằng chứng riêng, thực hiện trong implementation phase; run lập kế hoạch này không sửa DOCX/diagrams:

| Fix đã cam kết | Accountable | Recovery due / gắn task | Evidence cần reviewer xác nhận |
| --- | --- | --- | --- |
| Fix-01 ERD/session/items/cook/caregiver/source | Hùng | 30/11 / P3-H03, Nam cung cấp actual-serving mapping | ERD + physical FK mapping tới source-entry/actual split allocation; Supplier/BatchNumber trên Ingredient đơn lẻ không đủ |
| Fix-02 overview/context/activity notation | Đạt | 01/12 / FE/documentation phối hợp B03 | Hình và source diagram đúng actual stack, end-node notation; không Bảo nhận thay ownership FE |
| Fix-03 rejection loop/state machine | Nam | 01/12 / P3-N03 | Reject→revise/resubmit path và state mapping actual code; không chỉ thêm nhãn REJECTED_BY_ADMIN mà thiếu transition |
| Fix-04 SRS business context | Bảo | 01/12 / P3-B03 | Boundary/external entities đúng yêu cầu reviewer; tích hợp FR/source/test references |
| SR-07 offline E2E | Cường | 03/12 / WF-05 với N01 hỗ trợ | CI/device restart/retry evidence, không lấy file test tồn tại làm pass |

Recovery due không thay mốc signed12/11 và15/11; Bảo xin reviewer disposition như audit đã nêu. Chưa có xác nhận thì giữ open, không tự đánh “Fixed”.

## 6A. Lead release controls — đường găng và ranh giới giờ

- **CI là việc của cả dự án:** P2 phải có workflow skeleton chạy thực trên integration branch (BE tests + private PG; FE/Flutter jobs do Đạt/Cường đóng góp); P3-B03 chỉ hoàn thiện mapping 24 case, evidence, deployment. Không dành ngày27 để mới tạo `.github/workflows`.
- **Nam → Cường:** ngày23/11 17:00 có upload staging tối thiểu trả durable receipt hoặc nêu blocker. Ngày24/11 Cường chạy timeout/retry; ngày27/11 approval/private bytes; ngày26/11 smoke-test và ngày03/12 device kill/restart test nghiệm thu. Metadata/photo queue phải tạo từ thiết bị khi offline, không phụ thuộc server lúc capture.
- **Bảo → Hùng/Nam:** report monthly/recompute phải đọc dữ liệu actual use/source revision đã giao P2, không mock IDs. PDF + CSV theo BM04 DS-06a; thư viện PDF/font cần chọn sớm P2, không thử lần đầu 02/12. Inspector là granted scoped reader, **không thêm global fifth role**.
- **NFR / test gate:** 30 distinct functional case IDs được định nghĩa, **ít nhất 24 designated CI cases phải chạy pass trên release SHA**; >=95% số ca thực thi và 100% mandatory main path. Nếu đúng 30 case thực thi, 29/30 ≈96,7% thỏa ngưỡng số học nhưng **bất kỳ critical case fail hoặc Critical/High defect vẫn no-go**. Không tính skipped là pass hoặc dùng 278 pass/34 skip thay ledger BM04.
- **Tài liệu song song:** việc sửa SRS/SDD/ERD/diagram và 13 BM02 High có owner/commit chứng minh, reviewer quyết định. Một design finding chưa closure không tự động chứng minh runtime High; ngược lại không được xóa finding chỉ vì runtime test pass.
- **Quá tải:** nếu tới 23/11 vẫn còn P2 serving/menu/report draft chưa tích hợp, không giả định Bảo có 8h trống cuối tuần để sửa. Daily 17:00 báo số giờ còn lại/blocker, chuyển người hỗ trợ ở module đã đóng hoặc xin reviewer disposition, không giấu thiếu scope.

## 7. Ledger BM04 — 30 unique cases, 24 CI

**Provisional theo BM04 §8, chờ reviewer giải quyết §6/§7.** IDs dưới đây là case quản lý nghiệm thu mới, **không phải tên test đã tồn tại**. Owner phải map mỗi ID tới test method/artifact; không đếm một ID nhiều lần do unit+HTTP+device layers. Task acceptance ở §3–5 là chi tiết kiểm tra bổ sung/subassertions, không tăng/đổi committed totals. “CI” là bắt buộc tự động hóa ở release, không trạng thái hiện tại.

| ID | Owner / CI | Input → expected status/data/immutability | FR / M / WF |
| --- | --- | --- | --- |
| W01-01 | Đạt / Có | Valid OTP register với attempt staff role →account chỉ Parent, chưa child access; invalid/expired OTP400 không tạo user | FR-18/22 / M1 / WF-01 |
| W01-02 | Đạt / Có | Pending link→assigned reviewer approve→Parent child200; reject/cancel/revoke giữ history và access denied | FR-02/10 / M1 / WF-01 |
| W01-03 | Đạt / Có | Teacher classA request classB child/import→403; owned roster200, không ghi DB ngoài scope | FR-01/07/22 / M1 / WF-01 |
| W01-04 | Đạt / Có | XLSX invalid/duplicate rows preview có errors, confirm bị chặn không partial writes; valid import tạo đúng enrollment/history | FR-01 / M1 / WF-01 |
| W01-05 | Đạt / Không | Staging FE parent-link/roster ở320/390/768/desktop, thao tác Việt ngữ hoàn tất, không mất trạng thái hoặc lộ trẻ khác | FR-01/02/18 / M1 / WF-01 |
| W02-01 | Hùng / Có | K draft/submit→A approve/publish200; reject có reason/resubmit new version; published v1 retained | FR-05/13/23 / M3 / WF-02 |
| W02-02 | Hùng / Có | Independent g/ml/gross/edible fixtures →rounded kcal/macros đúng; missing density/macro422 không publish | FR-11/12 / M3 / WF-02 |
| W02-03 | Hùng / Có | Egg2 suppliers/2 receipts + correction v2 →distinct IDs/history preserved; nutrient SourceRef không bị thay | FR-11/23 / M3 / WF-02 |
| W02-04 | Hùng / Có | Published recipe100g ×latest30 portions→estimate3000g/CSV; stale token409, old estimate unchanged | FR-16/14/24 / M3 / WF-02 |
| W02-05 | Hùng / Không | Trường kiểm reference group/basis/source và manual hand-calculation; publish R1 rồi R2 correction vẫn As-Published R1 | FR-12/24 / M3 / WF-02 |
| W03-01 | Nam / Có | Tại21:00D−1 và07:00D, before accepted/exact denied theo API; đúng3 meal types; unknown type400 | FR-03/08/19 / M2 / WF-03 |
| W03-02 | Nam / Có | PG worker delayed/restart/multi-instance + late absence/transfer →one original snapshot, decisions trước cutoff | FR-03/04/19 / M2 / WF-03 |
| W03-03 | Nam / Có | Teacher wrong class403; parent range update/cancel giữ locked snapshot và chỉ đổi future sessions; cancelled meal không settle | FR-07/08/19/22 / M2 / WF-03 |
| W03-04 | Nam / Không | Staging clock-controlled breakfast/lunch/snack recovery từ shutdown →counts và event sources đúng, không manual settlement thay worker | FR-03/04 / M2 / WF-03 |
| W04-01 | Nam / Có | Teacher amendment→Admin approve→new version; reject giữ counts, previous version không đổi | FR-04/09 / M2 / WF-04 |
| W04-02 | Nam / Có | PG2 approvals cùng source version →one applied/one409, no partial or duplicate rows | FR-04/09/23 / M2 / WF-04 |
| W04-03 | Nam / Có | Amendment làm token đổi→old ack409/new ack201/retry same ID; Admin ack403, old ack retained | FR-14/22 / M2 / WF-04 |
| W04-04 | Nam / Có | A18→20,B10 latest total30, history sum48 không dùng; missing legacy roster không invent children | FR-04/07/14 / M2 / WF-04 |
| W05-01 | Cường / Có | API serving1000g split600/400, substitution có reason→201 đúng actor/class/session; v2 không overwrite v1 | FR-15/23 / M4 / WF-05 |
| W05-02 | Cường / Có | Durable queue file+metadata ghi rồi khởi tạo lại storage adapter→item/key/capturedAt còn nguyên | FR-17 / M4 / WF-05 |
| W05-03 | Cường / Có | Response lost sau server commit, retry3 lần→same photo ID200, one final object; khác hash409 | FR-17 / M4 / WF-05 |
| W05-04 | Cường / Có | Approved+linked image bytes200; pending/rejected/unlinked/revoked403, anonymous401, private object không public | FR-05/20/22 / M4 / WF-05 |
| W05-05 | Cường / Không | Real Android airplane capture→kill/relaunch→reconnect→sync, timestamps giữ đúng và local file không mất | FR-17 / M4 / WF-05 |
| W05-06 | Cường / Không | Device source picker cùng egg chọn2 receipts/quantity; source thiếu hiển thị gap, pending/rejected photo không xuất Parent | FR-15/17/20 / M4 / WF-05 |
| W06-01 | Bảo / Có | Monthly draft→approve→replay frozen inputs; later correction không đổi hash/totals/as-approved | FR-06/24 / M5 / WF-06 |
| W06-02 | Bảo / Có | Trace serving→ingredient use→two source entries→suppliers; thiếu400g trả explicit gap, approval theo policy được reviewer chốt | FR-06/15/23 / M5 / WF-06 |
| W06-03 | Bảo / Có | Nutrient100→110 trên1000g→delta+100; supplier-only correction→numeric delta0+source diff; old report intact | FR-06/24 / M5 / WF-06 |
| W06-04 | Bảo / Có | CSV/PDF approved200 cùng frozen figures; quotes/newline/formula safe; reject/stale approval giữ history, export unapproved409 | FR-06/23/24 / M5 / WF-06 |
| W06-05 | Bảo / Có | Admin/report-grant scope/expiry và wrong roles; audit redaction+source history; malicious field không lộ secret/child data | FR-22/23 / M5 / WF-06 |
| W06-06 | Bảo / Không | Staging3 năm: register≤5s/report≤10s theo protocol, PDF Việt ngữ/trang đủ,12-story pack/migration restore có chứng cứ | FR-06/24 + NFR-06 / M5 / WF-06 |

Kiểm tổng: WF-01 **5/4**, WF-02 **5/4**, WF-03 **4/3**, WF-04 **4/4**, WF-05 **6/4**, WF-06 **6/5** = **30/24**. Nam có8 cases/7 CI; Cường có6/4, Bảo6/5, Đạt5/4, Hùng5/4. Sáu non-CI là W01-05, W02-05, W03-04, W05-05, W05-06, W06-06. Test count không thay defect gate: một High dù case pass đủ vẫn stop-ship. Các PG cases đã skip ngày15/11 phải chạy lại, không đổi label thành CI done.

## 8. Sáu workflow demo stories — 12 scenarios

Tất cả dưới đây là **seed plan**, chưa nạp staging. Dùng dữ liệu giả, không trẻ/ảnh cá nhân production. Presenter đúng BM04 §8. Một main và một exception story/WF; nhánh assertions trong story không tăng scenario count.

### Seed chung S1–S4

- **S1 identity:** năm2026–2027, lớpA/B; Admin A, Kitchen K, TeacherTA chỉ lớpA, TeacherTB lớpB, ParentPA linked childCA trongA, ParentPX không liên kết, InspectorI grant chỉ reports lớpA có ngày hết hạn. OTP provider test có thể dùng test transport trong CI, **staging demo phải ghi rõ transport và bằng chứng nhận**, không gọi stub là SMS/WhatsApp thật.
- **S2 operations:** MealDay Breakfast/Lunch/Snack ngày19/11 (fake clock chỉ môi trường test/staging cô lập), cutoffs18/11 21:00 và19/11 07:00 UTC+7; classA initial18 rồi approved amendment20, B10. Latest total30, history sum48. Một ngày hủy, một child transfer sau cutoff và trước delayed worker.
- **S3 nutrition/source/serving:** Ingredient egg unitg, edible100%, fixture nutrition100kcal/100g, protein10/fat5/carb2g/100g, source “SYNTHETIC TEST ONLY” (không phải reference sức khỏe). Recipe friedEgg v1:1000g/20portions; actual classA serving20 uses1000g, sourceA600/sourceB400. SupplierA/sourceA nhận18/11 quantity2000g/refA18; SupplierB/sourceB nhận19/11 quantity3000g/refB19. Actual nutrition tổng1000kcal,50kcal/portion; report scope classA cho comparison. Correction nutrient110→total1100/delta100; source-only correction numeric delta0. Một recipe thứ hai cho substitution; fixture ml density0.9 riêng để demo validation. **Age reference demo dùng dataset thật được trường duyệt, tách fixture tổng hợp này.**
- **S4 evidence/report:** private storage rỗng, stable clientPhotoId và ảnh món ăn test không có mặt trẻ; report draft monthly11/2026 chọn actual served versions. Dataset performance có3 năm, số lớp/trẻ/sessions/source/events được ghi trong manifest, không lấy seed20 trẻ làm benchmark3 năm.

### WF-01 / Đạt — enrollment và verified linkage

**DS-01a main:** S1 childCA chưa linked ở đầu story, valid OTP; Parent đăng ký/sign in, request link, TA/Admin review approve, Parent đọc childCA200. Expected account role chỉParent, pending chưa được dữ liệu trẻ, approved link có actor/time; enrollment đúng lớp. Chứng cứ UI + HTTP/DB + W01-01/02/04.

**DS-01b exception:** OTP sai/hết hạn →400 không tạo account; hoặc link rejected có reason, Parent không đọc child; TA gọi classB403. Review/cancel/revoke không xóa lịch sử. Expected không cấp staff/child scope bằng client input. W01-01/02/03.

### WF-02 / Hùng — menu, nutrition và ingredient requirement

**DS-02a main:** S3 recipe/source entries + S2 latest30; K draft daily menu và week read, submit, A approve/publish; xem kcal/macros và age comparison nguồn đã duyệt; tạo estimate100g×30=3000g cho recipe planning riêng, xuất CSV. Expected published version/inputs frozen, physical sources riêng nutrient reference. W02-01…05.

**DS-02b exception:** recipe ml thiếu density hoặc legacy macro thiếu; publication422 có missing field, A reject với reason; revision sau correction mới được submit/approve. Unknown age range422/unknown rõ, không dùng0. Published v1 không thay đổi bởi revision rejected. W02-01/02/05.

### WF-03 / Nam — participation và automatic settlement

**DS-03a main:** S1/S2, Parent absence trước cutoff, Teacher exception đúng lớp; chạy clock qua21:00/07:00, worker tự settle, restart/retry. Expected đúng cutoff từng bữa, before-cutoff decisions included, một original snapshot/lớp, actor system; không cần Admin click settle. W03-01/02.

**DS-03b exception:** late absence/cancel/replacement hoặc cancelled meal; delayed worker sau transfer07:15. Expected locked decision không đổi, cancelled không settle, future open meals còn áp dụng valid report; unauthorized Teacher403. Không sửa old cutoff để demo pass. W03-02/03/04.

### WF-04 / Nam — amendments và kitchen acknowledgement

**DS-04a main:** S2 A18/B10, Teacher gửi justified amendment+2, A approve tạo A-v2=20; Kitchen diff total30, acknowledge new token. Expected A-v1=18 retained, ack attributable/time và latest version, retry same ID. W04-01/03/04.

**DS-04b exception:** concurrent reviews cùng source hoặc rejected request, stale token ack. Expected loser409/rejected count unchanged; stale ack409 phải refresh; Admin ack403; cancelled session không tạo settlement. Không đếm28+20 thành48 latest. W04-02/03/04. Story có worker repeat để giữ nội dung demo §6.4 dù tên WF là amendments.

### WF-05 / Cường — serving split sources và ảnh offline

**DS-05a main:** S3 K chọn dish/version, actual20portions/egg1000g/600+400/source dates/cook actor/classA; Android airplane capture S4, kill/relaunch/reconnect; sync1 photo, A approve, PA view bytes. Expected actual lineage và capture/server receipt timestamps, private image scoped. W05-01/02/05/06.

**DS-05b exception:** mạng chập chờn làm mất response, retry3 lần; PX và Parent đã revoked gọi content; cùng key khác hash. Expected one evidence/object, same ID hoặc409conflict, guardian403; pending/rejected không xuất. Nếu thiếu sourceB400, serving ghi gap rõ, không giả supplier và không báo trace complete. W05-03/04/06, W06-02 hỗ trợ nhưng không đếm lại ownership.

### WF-06 / Bảo — monthly report, export, trace và recalculation

**DS-06a main:** S3/S4 complete serving, Admin generate monthly actual report lớpA, approve, export **cả PDF và CSV**, trace tới hai dated source entries và responsible staff. Expected1000kcal total/50 mỗi phần theo fixture, frozen IDs/values/hash; report settled vs actual metrics tách biệt. Inspector grant lớpA read được, không admin write. W06-01/02/04/05.

**DS-06b exception:** sau approval, correct nutrient100→110 hoặc actual quantity/source; request Recomputed, xem delta+100kcal khi quantity1000g giữ nguyên và reason/changed IDs. As-Approved R1/hash/export vẫn1000. Supplier-only correction có source diff dù delta0; missing400g source tạo flagged draft, approval được xử lý theo policy A/B đã chốt với reviewer và không che giấu gap. W06-02/03, không “sửa” R1 để khớp số mới.

## 9. Stop-ship và bằng chứng release

**Dừng phát hành** nếu bất kỳ điều kiện sau chưa đạt vào06/12 17:00:

1. Không có nguồn thật hoặc missing-source flag cho ingredient-use, split allocation sai tổng/ingredient, mất actor/class/session; nhầm SourceRef nutrition thành receipt; report trace chỉ tới recipe kế hoạch.
2. Worker/cutoff/late enrollment/amendment/ack sai, duplicate/race hoặc cộng history thành latest; PG test cần thiết vẫn skip. Không giảm gate vì SQLite đã pass.
3. Photo mất sau restart, retry trùng, pending/unlinked/revoked đọc được metadata/bytes hoặc public object; Android device chưa có evidence. Offline auth restore không thay offline photo.
4. Published menu/report/source/serving history bị overwrite, As-Approved đổi sau correction, thiếu frozen range/rule/source inputs để replay, không có exact Recomputed diff; CSV hoặc PDF thiếu.
5. FR-16 estimate/export hoặc FR-12 reference mầm non chưa đủ nguồn/basis; số liệu giả được trình bày như validated reference. Không lấy flag “missing” để đóng Must-have này.
6. Ledger không đủ30 unique/24 CI, mandatory main path chưa100%, overall<95%, 12 demos chưa đủ6+6; bất kỳ open Critical/High kể cả BM02 chưa được reviewer đóng. Mốc benchmark không đạt hoặc protocol/test environment chưa được ghi.
7. Migration/restore chưa rehearsal, source/API/schema khác release SHA, Inspector/report/guardian scope unresolved, thiếu client collaborators hoặc chưa reviewer giải quyết mâu thuẫn BM04 và recovery dates.

Evidence pack tối thiểu: release SHA/full commit graph; generated OpenAPI; reviewed migrations+SQL+fresh/upgrade/restore logs; seed manifest/3-year dataset counts; TRX/FE/Flutter outputs và CI run URLs; mapping30IDs/24CI; device model/OS/video restart+retry; six owner-presented demo pairs; CSV/PDF và immutable hashes; từng BM02 High có fix/remaining evidence/reviewer disposition. Không cần artifact “đẹp” thay kết quả chạy. Phần này là yêu cầu cho implementation run, audit hiện tại chỉ tạo3Markdown.

Không tự cut Must-have để vừa deadline. Khi gate đỏ, Bảo nêu task, impact, người cần bổ sung và số giờ còn thiếu ngay daily17:00; fallback trong scope là UI/template đơn giản, một trường, private authenticated streaming thay hệ thống storage phức tạp, giữ toàn bộ business invariants. Nếu vẫn không đủ, no-go và xin reviewer quyết định lịch, không rewrite signed commitment/tổng test.

## AUDIT LIMITATIONS

Các P3 routes/models/CI là **PROPOSED ROUTE — CONFIRM WITH OPENAPI** hoặc work items, chưa tồn tại/chưa chạy tại audit SHA. Baseline15/11 không chứng minh PostgreSQL/migration/FE/Android/device/staging/provider/performance; chỉ278 pass/34skip đã ghi audit. BM04 §6/§7 khác §8, Inspector/age/gap approval cần reviewer chốt; ledger này provisional không thay signed DOCX. Sample numeric nutrition là fixture tính toán, không ngưỡng mầm non thật. Kế hoạch yêu cầu đầy đủ thời gian Cường/Đạt; ba BE không thể tự chứng nhận client ownership thay họ.

## NEXT HUMAN ACTIONS

1. Ngày23/11 đối chiếu actual merged P2 SHA, tạo issues P3-H01…H03/P3-N01…N03/P3-B01…B03 với owners/deadlines và checklist; không copy trạng thái “done” từ tài liệu.
2. Đặt lịch Cường device03/12 (smoke-test 26/11), Đạt FE/UAT03/12, trường reference26/11, reviewer04/12; cung cấp PG/staging/private storage và runtime CI trước khi chạy.
3. Chốt BM04 discrepancy/Inspector/source-gap approval bằng reviewer record, giữ30/24 và6+6; map từng W-ID sang actual test/CI evidence, đóng13BM02High bằng chứng thật.
4. Cả5 owner báo pass/fail/skip và remaining defects tại RC04/12; người có quyền quyết định release06/12 theo stop-ship, không tự merge/publish từ planning run.

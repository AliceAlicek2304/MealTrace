# MealTrace — Rebaseline Phase 2 và Phase 3 theo v1.4

> **LEAD REVIEW — 15/11/2026, awaiting final team sign-off.** This document is a consolidated *execution plan*, not a commit, a passing acceptance report or an approval to change BM04. Technical baseline: `origin/main@a6b187e6eae9553f7966d5444b8745f9896d2a11`; Codex ran backend tests on this SHA (278 passed / 0 failed / 34 skipped). Re-fetch and reconfirm before coding. See [Lead decisions](phase2-phase3-lead-decisions-v1.4.md).


Ngày audit: **15/11/2026, UTC+7**. Phạm vi: đối chiếu tài liệu, source và test; không triển khai production code. Kết luận: giữ nền Phase 1 đã merge; đường găng mới là **supplier/source-entry → menu công bố → serving/actual allocation → report bất biến**, kèm Android offline và quyền xem ảnh. Chưa đủ bằng chứng để nghiệm thu M1–M5 end-to-end.

## 1. Git và baseline kiểm thử

- Ban đầu `git status --short` rỗng. Nhánh làm việc `Bao`; HEAD và `origin/main` cùng **`a6b187e6eae9553f7966d5444b8745f9896d2a11`**.
- `git fetch --all --tags --prune` lần đầu bị sandbox chặn `.git/FETCH_HEAD`; chạy lại với quyền được cấp thành công, exit 0. Snapshot **LIVE VERIFIED tại thời điểm fetch**, khoảng 01:12 UTC+7 ngày 15/11; không phải cam kết remote không đổi về sau.
- Đã đọc `git log -n 20 --oneline --decorate --all`, `git branch -avv`, so sánh remote bằng `git rev-list --left-right --count origin/main...<ref>`. Không checkout/reset/commit/push/merge.
- Tag annotated `review-2` trỏ **`62c09d6b70caa4458fc24ac215df12cdcf95026f`**, nội dung “Review 2 baseline”. Local `main` vẫn tại tag, chậm hơn remote 17 commit; **không dùng local main/tag thay current main**. Diff tag→main: 127 files, 7.250 additions, 426 deletions.

| Remote branch | Commit tại fetch | Main-only / branch-only | Kết luận |
| --- | --- | --- | --- |
| `origin/Bao` | `ba68691` | 1 / 0 | Đã vào main qua PR #9 |
| `origin/cuong` | `a2e9207` | 4 / 0 | Đã merge; gồm cập nhật web/Android và nutrition |
| `origin/dat` | `9307fe0` | 45 / 0 | Không có commit riêng chưa merge |
| `origin/hung` | `aeb540f` | 25 / 0 | Nutrition v1.3 đã ở main |
| `origin/nam` | `840f77b` | 23 / 0 | Worker/ack đã ở main |
| `origin/mobile` | `22bc7f5` | 65 / 0 | Không có commit riêng chưa merge |
| `origin/web` | `d5285e1` | 65 / 0 | Không có commit riêng chưa merge |

GitHub connector xác nhận [PR #9](https://github.com/AliceAlicek2304/MealTrace/pull/9) merged, merge SHA đúng HEAD; [PR #8](https://github.com/AliceAlicek2304/MealTrace/pull/8) merged tại `30bed10`, đưa worker vào main; [PR #7](https://github.com/AliceAlicek2304/MealTrace/pull/7) merged tại `d53acac`, đưa centralized audit vào main. `8d961f9` tích hợp nhánh Cường; `eb9d479` tích hợp Sonar fixes. Search PR mở trả rỗng. Không thấy branch-only work ở **các remote refs đã fetch**; không suy ra không có thay đổi local của đồng đội/fork chưa công bố.

### Test thực sự chạy

Máy Windows, .NET SDK **8.0.421**, runtime **8.0.27**. Cả hai lượt dùng source SHA nêu trên, cấu hình `be/tests/MealTrace.Api.Tests/MealTrace.Api.Tests.csproj` (`net8.0`, xUnit, SQLite test host).

```powershell
dotnet test be/MealTrace.sln --no-restore --logger 'trx;LogFileName=rebaseline.trx' --results-directory "$env:TEMP/mealtrace-rebaseline-20261115" --verbosity minimal
# Chạy lại ngoài sandbox, chỉ đổi tên TRX:
dotnet test be/MealTrace.sln --no-restore --logger 'trx;LogFileName=rebaseline-unsandboxed.trx' --results-directory "$env:TEMP/mealtrace-rebaseline-20261115" --verbosity minimal
```

| Lượt, UTC+7 ngày 15/11 | Passed | Failed | Skipped | Tổng | Ý nghĩa |
| --- | ---: | ---: | ---: | ---: | --- |
| Sandbox 01:13:56–01:14:05 | 99 | 179 | 34 | 312 | 178 lỗi Windows Event Log access denied, 1 ObjectDisposedException; chưa quy thành 179 lỗi nghiệp vụ |
| Ngoài sandbox 01:15:28–01:17:50 | **278** | **0** | **34** | **312** | Exit 0; baseline cục bộ cuối cùng |

TRX ở `%TEMP%/mealtrace-rebaseline-20261115/`, không thêm artifact test vào repo. `NotExecuted` trong từng result là 34 dù summary TRX ghi `notExecuted=0`; console cũng ghi Skipped 34. Gồm **33 PostgreSQL cases và 1 opt-in baseline measurement**. `MEALTRACE_TEST_CONNECTION` không được cấu hình; không đọc connection production, không chạy script baseline tự lấy local DB. Không coi skipped là pass.

- `KitchenNutritionV13Tests`: 21/21 pass, chủ yếu fake repositories; không chứng minh migration/SQL concurrency.
- `MealSettlementWorkerTests`: 5/5 pass; delayed absence/retry/cancelled/before cutoff đã có test chạy.
- `PhaseOneRbacTests`: 6/6 pass; `AuditReviewTests`: 28/28 pass; `AuditTests`: 22 pass, 1 PostgreSQL skip.
- `where.exe node/npm/flutter/gh` không tìm thấy executable trên PATH. Không chạy FE `npm test`/build hoặc Flutter test/analyze/device; không cài dependencies trong audit này. Có source test không phải bằng chứng pass.
- Không thấy `.github/workflows`, pipeline thay thế hay `.sql` tracked qua inventory. GitHub workflow query tại HEAD trả rỗng, nhưng connector chỉ xét trang đầu các run trigger PR: **không chứng minh tuyệt đối không có CI bên ngoài**. 24 CI cases BM04 chưa được chứng minh.

## 2. Nguồn chuẩn và cách đọc trạng thái

Đã đọc toàn bộ text **và từng cell bảng DOCX** bằng OOXML, theo thứ tự:

1. [Registration v1.4](review2/Phieu_FA26SE270_V1.4.docx), §3.2(c), (d), (g), (h), §4.
2. [BM04](review2/SEP490_FA26SE270_BM04_Review2_Commitment_1.docx), §2 xác nhận v1.4 approved, §3 M1–M5, §6 WF, §8 ledger/gates, §9 fixes.
3. [BM01](review2/SEP490_FA26SE270_R2_BM01_Design_Brief_1.docx), [BM02](review2/SEP490_FA26SE270_R2_BM02_Self_Review_Report_1.docx), [BM03](review2/SEP490_FA26SE270_R2_BM03_Record_of_Changes_1.docx). BM03 dòng 21–30 giải thích phần thêm v1.4.

Bản registration ở `docs/review-2/` có cùng SHA256 `7955F5404988792442D2D0E90C57AA72100F240B6103651DDA5B529A830ABF13`. BM01/BM02/BM03 còn ghi “proposed”; dùng xác nhận BM04 §2 làm căn cứ, không nâng v1.3 thành scope cuối. Không xác thực chữ ký ngoài nội dung DOCX.

| Nhãn | Cách dùng trong audit |
| --- | --- |
| VERIFIED_IN_MAIN | Hành vi cụ thể có source trong main, test liên kết và kết quả chạy pass; chỉ trong môi trường/biên đã chạy |
| PARTIAL | Có phần chạy được nhưng thiếu yêu cầu hoặc bằng chứng cho toàn FR |
| SCAFFOLD_ONLY | Entity/read endpoint có sẵn nhưng chưa có write lifecycle được cam kết |
| MISSING | Không tìm thấy implementation qua routes, services, entities, schema, clients và tests đã khảo sát |
| UNVERIFIED_TEST | Test có nhưng chưa chạy/skip; không gán pass |
| HISTORICAL/OBSOLETE | Claim tại baseline cũ, không dùng quyết định scope/trạng thái hiện tại |

### Ma trận v1.4 → BM04 → source → test

Các đường dẫn dưới đây tính từ repo root. Test viết tắt `X.Method` luôn nằm trong `be/tests/MealTrace.Api.Tests/X.cs`; kết quả **pass** chỉ nói về lượt TRX thứ hai. Danh mục E bên dưới xác định đầy đủ source và routes, tránh nhầm route đề xuất với route đã có.

| FR theo registration §3.2(c) | M / WF BM04 | Hiện trạng source/route | Test đã chạy hoặc còn thiếu | Status toàn FR |
| --- | --- | --- | --- | --- |
| FR-01 trường/lớp/trẻ/import | M1 / WF-01 | E1/E3: `StudentAdministrationService`, `StudentImportService`, `WorkflowService`; Enrollment | Enrollment/StudentImport/StudentProfile suites: non-PG pass; PG import skip; thiếu xác minh cutoff chuyển lớp theo bữa | PARTIAL |
| FR-02 tài khoản/liên kết | M1 / WF-01 | E1/E2: `AccountService`, `ParentLinkService`, ParentStudent/TeacherAssignment | `ParentLinkTests.ApprovalRequiresSchoolReviewBeforeChildAccess` pass; history thay scope trực tiếp còn thiếu | PARTIAL |
| FR-03 calendar/cutoff tự động | M2 / WF-03 | E4/E5: `SchoolTime.Cutoff`, `MealSettlementWorker.ProcessPendingSettlementsAsync` | `MealSpecificCutoffCalculatesCorrectTimestampsForBreakfastAndDaytimeMeals`, 5 worker tests pass; free-text/07:30 còn tồn tại | PARTIAL |
| FR-04 settlement/amendment | M2 / WF-03,04 | E5: `WorkflowService.SettlePortionsAsync`, `PortionAmendmentService.ReviewAmendmentAsync` | PortionAmendment non-PG pass; concurrent approvals skip | PARTIAL; lõi version đã VERIFIED_IN_MAIN |
| FR-05 menu và photo approval | M3,M4 / WF-02,05 | E7/E8: MenuDish/MealEvidence chỉ đọc, không approval API | Chưa có workflow tests tương ứng | SCAFFOLD_ONLY |
| FR-06 report/trace/compare | M5 / WF-06 | E8: `MealService.GetReportLineageAsync` chỉ đọc SourceJson | Không có ReportLineage/Recalculation workflow test; audit test không thay thế | SCAFFOLD_ONLY |
| FR-07 Teacher xem lớp/quyết định | M2 / WF-03 | E3/E5: `MealExceptionService.GetDecisionsAsync`, `PortionService.ReadAsync` | `PhaseOneHttpScopeTests.ClassesPortionsDecisionsAndExceptionWritesEnforceHttpRolesAndScope` cả5 role inputs pass; PG search skip | VERIFIED_IN_MAIN cho HTTP scope đã chạy |
| FR-08 exception trước cutoff | M2 / WF-03 | E5: `RecordExceptionAsync`, MealRegistration | MealExceptionTests non-PG pass; SchoolTimeConcurrency skip | PARTIAL cho cả cam kết race/cutoff |
| FR-09 request adjustment | M2 / WF-04 | E5: `RequestAmendmentAsync`/`ReviewAmendmentAsync` | PortionAmendmentTests non-PG pass; race skip | PARTIAL |
| FR-10 duyệt liên kết đúng lớp | M1 / WF-01 | E2: `ParentLinkService.ReviewAsync`, `BulkReviewAsync`, `RevokeAsync` | `VerifiedSignupCanRequestClassLinkAndUseSameSessionAfterSchoolApproval` pass cả teacher/admin | VERIFIED_IN_MAIN cho paths đã chạy |
| FR-11 nutrient + supplier/source | M3 / WF-02 | E6 `IngredientService.AddVersionAsync`; không Supplier/SourceEntry trong domain/schema | `IngredientVersionRecordsSourceActorAndIncrementsVersion` pass; source-entry tests chưa có | PARTIAL; phần physical source MISSING |
| FR-12 recipe/nutrition/tuổi | M3 / WF-02 | E6 `RecipeService`, `NutritionService.CalculateAsync/CompareAsync`, NutrientReferenceRange | 21 KitchenNutritionV13 pass; tuổi dùng fixture giả, chưa seed nguồn mầm non đã duyệt | PARTIAL |
| FR-13 soạn/gửi menu ngày/tuần | M3 / WF-02 | MenuDish chưa có MenuVersion/state workflow | Chưa có menu state tests | SCAFFOLD_ONLY |
| FR-14 latest/ack | M2 / WF-04 | E6 `KitchenSettlementService.Summary/Token/AcknowledgeSettlementAsync` | `NewSettlementVersionMustRequireFreshKitchenAcknowledgement` pass SQLite; PostgreSQL variant skip | VERIFIED_IN_MAIN cho latest/stale ack đã chạy; PG UNVERIFIED_TEST |
| FR-15 phục vụ/đổi món/split sources/staff | M4 / WF-05 | Không ServingRecord/ServedDish/ActualIngredientUse; MenuDish là planned link | Chưa có actual-use tests | MISSING |
| FR-16 estimate/export nguyên liệu | M3 / WF-02 | Có recipe quantities + latest portions; không estimate service/API | Chưa có estimate test | MISSING |
| FR-17 offline photo/retry | M4 / WF-05 | MealEvidence timestamp chỉ scaffold; Android chưa có durable photo queue | Không EvidenceSync/OfflineStorage suite; device chưa chạy | MISSING |
| FR-18 signup/link request | M1 / WF-01 | E2 `AuthService.RegisterParentAsync`, OTP, ParentLink | ParentSignup/ParentSignupOtp/ParentLink non-PG pass; OTP provider thật chưa nghiệm thu | PARTIAL |
| FR-19 absence range/change/cancel | M2 / WF-03 | E5 `ReportAbsenceAsync/ReplaceAbsenceAsync/CancelAbsenceAsync`; quyết định bounded tại cutoff | LongMealAbsence non-PG, worker delayed recovery pass; lock-race skip | PARTIAL |
| FR-20 published menu/nutrition/photo linked child | M3,M4 / WF-02,05 | MealStaff = Admin/Kitchen; không Parent publication API/private-image stream | Chưa có GuardianPhotoVisibility tests | MISSING |
| FR-21 dietary note | Optional; không tính M bắt buộc | Chưa tìm thấy workflow; không đưa vào critical path | Không yêu cầu để đạt gate | MISSING — OPTIONAL |
| FR-22 auth/record scope | M1–M5 / tất cả WF | E1–E9, JWT role + service scope; endpoints mới chưa có | `PhaseOnePolicyMatrixCoversFourRolesAndAnonymous` và kitchen HTTP matrix pass | PARTIAL cho toàn v1.4 |
| FR-23 histories + source changes | M4,M5 và xuyên module / WF-05,06 | E9 audit projections; DbContext guard có settlement/nutrient, thiếu menu/source/serving/report | Audit 50 pass/1 PG skip; chưa có adapters nutrient/recipe/ack hoặc sources mới | PARTIAL |
| FR-24 approved calculation reproduction | M5 / WF-06 | Pinned recipe có; ReportSnapshot chưa có immutable approval lifecycle | `PinnedAndRecomputeAreDistinguishedAndReproducible` pass; report replay test thiếu | PARTIAL |

## 3. Inventory implementation và toàn bộ route hiện hữu

Đọc `be/src/MealTrace.Api/Program.cs:10`: config connection/JWT, DI Application/Infrastructure, JWT auth, CORS `Frontend`, rate limiter login/register/OTP/parentLinks; `Program.cs:87` đăng ký hosted worker. Swagger chỉ Development. Authorization đăng ký mặc định, hầu hết route dùng **inline RequireRole**, không có named policy cho menu/report/photo mới. `RoleNames.cs:5` có ADMIN, TEACHER, KITCHEN_STAFF, PARENT; `MealStaff` chỉ Admin/Bếp, `ReportReaders` chỉ Admin.

Quy ước A=ADMIN, T=TEACHER, K=KITCHEN_STAFF, P=PARENT; Auth=đăng nhập. Mọi route sau là **EXISTING**, liệt kê theo prefix; `{id}` giữ constraint GUID ở source (version là int). Role route chưa thay kiểm tra ownership ở service.

| Evidence | File trong `be/src/MealTrace.Api/Features/` | Prefix + method/suffix, quyền |
| --- | --- | --- |
| E1 | `AccountEndpoints.cs:13` | `/api/admin`: GET `/users`, `/users/{id}`, `/scope-options`; POST `/users`, `/users/{id}/reset-password`; PUT `/users/{id}` — A |
| E2 | `AuthEndpoints.cs:13` | `/api/auth`: POST `/login`, `/register/otp`, `/register` — anonymous/rate limited; GET `/me`, POST `/change-password`, `/logout` — Auth |
| E2 | `ParentLinkEndpoints.cs:12` | `/api/parent/link-requests`: GET root, `/classes`; POST root, `/{id}/cancel` — P. `/api/student-link-requests`: GET root, `/classes`; POST `/{id}/review`, `/bulk-review`, `/{id}/revoke` — A/T, class scope |
| E3 | `StudentAdministrationEndpoints.cs:13` | `/api/admin`: GET `/classes`, `/students`, `/students/{id}/enrollments`; PUT `/classes/{id}`, `/students/{id}`; POST `/students/{id}/enrollments` — A. GET `/api/students/search` — A/T |
| E3 | `StudentImportEndpoints.cs:15` | `/api/admin/students/import`: POST `/preview`, `/confirm`; GET `/history`, `/history/{id}` — A |
| E3 | `WorkflowEndpoints.cs:13` | `/api`: GET `/academic-years` — Auth; GET `/admin/academic-years`, `/admin/academic-years/{code}/next`, PUT `/admin/academic-years/{code}` — A; GET `/classes` — A/T/K; POST `/classes` — A; GET `/classes/{classId}/students`, POST `/students`, POST `/admin/students/{studentId}/parents` — A/T; GET `/parent/students` — P |
| E4 | `MealCalendarEndpoints.cs:13` | `/api/admin/meal-calendar`: GET `/{code}`, `/{code}/history`; PUT `/{code}/schedule`, `/{code}/days/{date}`; POST `/{code}/preview`, `/{code}/generate` — A |
| E5 | `WorkflowEndpoints.cs:38` | `/api/parent/absences`: POST root, `/{id}/replace`, `/{id}/cancel`; GET root, `/search` — P/owned child. `/api/meal-days`: POST root, `/{id}/settle` — A; GET `/workflow`, `/{id}/portions` — A/T/K |
| E5 | `MealExceptionEndpoints.cs:13` | `/api/meal-days`: GET `/{id}/decisions`, `/{id}/students/{studentId}/exceptions`; POST `/{id}/exceptions` — A/T, assigned-class checks |
| E5 | `PortionAmendmentEndpoints.cs:13` | `/api/meal-days/{dayId}/amendments`: GET root, `/{requestId}` — A/T/K; POST root — A/T; POST `/{requestId}/review` — A |
| E6 | `KitchenEndpoints.cs:12`, `IngredientEndpoints.cs:10` | `/api/kitchen/ingredients`: GET/POST root; POST `/{id}/ingredient-versions` — A/K |
| E6 | `RecipeEndpoints.cs:10` | `/api/kitchen/recipes`: POST root, `/{id}/versions`; GET `/{id}/versions/{v}/nutrition` — A/K. **Chưa có GET recipe catalogue** |
| E6 | `KitchenSettlementEndpoints.cs:11` | `/api/kitchen/settlements`: GET root, `/search`, `/{id}/diff`, `/{id}/acknowledgements` — A/K; POST `/{id}/acknowledge` — K và explicit NOT A |
| E7/E8 | `MealEndpoints.cs:11` | `/api/meal-days`: GET root, `/search`, `/{id}` — A/K. GET `/api/reports/{id}/lineage` — A |
| E9 | `AuditEndpoints.cs:10` | `/api/admin/audit-logs`: GET root, `/export`, `/entities/{entityType}/{entityId}`, `/{auditId}` — A; no-store/redaction |
| E9 | `NotificationEndpoints.cs:11` | GET `/api/notifications/settings` — A/T; POST `/api/admin/notifications/test` — A (có side effect gửi tin, **không gọi trong audit**) |
| E0 | `Program.cs` | GET `/api/health` — anonymous; Swagger development middleware không phải nghiệp vụ |

### Source và integrity đáng giữ

Liên kết kiểm chứng trực tiếp; tất cả outcomes dưới đây được đọc lại từ TRX cuối, không suy từ tên file:

| Hành vi / method source | Linked test thực tế | Outcome ngày15/11 |
| --- | --- | --- |
| `MealSettlementWorker.ProcessPendingSettlementsAsync` và cutoff query | [Worker_DelayedRecovery_AppliesAbsencesBeforeCutoff_AndIgnoresAbsencesAfterCutoff](../be/tests/MealTrace.Api.Tests/MealSettlementWorkerTests.cs#L115) | Passed; 5/5 worker tests Passed |
| `MealExceptionService.GetDecisionsAsync`, HTTP class scope | [ClassesPortionsDecisionsAndExceptionWritesEnforceHttpRolesAndScope](../be/tests/MealTrace.Api.Tests/PhaseOneHttpScopeTests.cs#L26) | Passed cả5 inputs: anonymous/A/T/K/P |
| `ParentLinkService.ReviewAsync`, duyệt bởi Teacher/Admin | [VerifiedSignupCanRequestClassLinkAndUseSameSessionAfterSchoolApproval](../be/tests/MealTrace.Api.Tests/ParentLinkTests.cs#L20) | Passed cả2 variants |
| `KitchenSettlementService.AcknowledgeSettlementAsync`, stale/current tokens | [NewSettlementVersionMustRequireFreshKitchenAcknowledgement](../be/tests/MealTrace.Api.Tests/KitchenSettlementRegressionTests.cs#L86) | Passed SQLite; PostgreSQL variant dòng87 NotExecuted |
| `RecipeService.BuildLinesAsync` và `NutritionService.Compute`, ml density | [MlLinesNeedAConfirmedDensityAndAreNeverTreatedAsGrams](../be/tests/MealTrace.Api.Tests/KitchenNutritionV13Tests.cs#L304) | Passed fake-repository service test |
| `NutritionService.CalculateAsync`, pinned/recompute | [PinnedAndRecomputeAreDistinguishedAndReproducible](../be/tests/MealTrace.Api.Tests/KitchenNutritionV13Tests.cs#L443) | Passed; chưa phải approved-report replay |
| `AuditService.ExportAsync`, encoding/escaping/redaction | [ExportRangeEncodingInjectionMaskingAndNestedRedaction](../be/tests/MealTrace.Api.Tests/AuditTests.cs#L79) | Passed; chỉ audit CSV |

Inventory schema gồm identity/scope (ApplicationUser, TeacherAssignment, ParentStudent, ParentLinkRequest, InspectorGrant, ParentSignupOtp, AccountPasswordResetAudit); school (AcademicYear, SchoolClass, Student, Enrollment, StudentImportBatch/Item); calendar/participation (MealSchedule, MealCalendarException/Audit, MealDay, MealAbsence, MealRegistration); portions (PortionSettlement, SettlementStudent/Decision/Acknowledgement, PortionAmendment/Student/Resolution); kitchen (Ingredient/Version, Recipe/Version/Ingredient, NutrientReferenceRange); scaffold (MenuDish, MealEvidence, ReportSnapshot). Repository interfaces ở `be/src/MealTrace.Application/Abstractions/Repositories/`, implementations ở `be/src/MealTrace.Infrastructure/Persistence/Repositories/`, mapping ở `Persistence/Configurations/`; Application services không thay schema thật.

Có22 migration Up/Down tracked, từ `20260930031430_InitialCreate` đến `20261028090000_KitchenV13Nutrition`. Các mốc liên quan: `20260930142728_StudentEnrollmentHistory`, `20260930161118_MealExceptionAudit`, `20261001114347_MealOperatingCalendar`, `20261001122617_PortionAmendmentWorkflow`, `20261006054822_BatchPortionAmendments`, `20261012162538_KitchenSettlementAcknowledgement`, `20261018172929_KitchenAcknowledgedSnapshot`, `20261018173502_KitchenAcknowledgementUniqueness`. Đã đối chiếu schema snapshot/config hiện tại với các field/constraint liên quan; **chưa chạy chuỗi migrations**. Không thấy source/test `.cs` ignored ngoài bin/obj làm thay source baseline.

- `be/src/MealTrace.Application/Features/Workflow/WorkflowService.cs:524`: settlement Serializable, lock MealDay, chống đã chốt, tạo snapshots một lần. `be/src/MealTrace.Infrastructure/Persistence/Repositories/MealDecisionRepository.cs:67` lọc absence/exception trước cutoff; `Eligible` lọc RecordedAt. Worker 30 giây và catch-up ở startup tại `be/src/MealTrace.Api/Workers/MealSettlementWorker.cs:14`.
- `be/src/MealTrace.Application/Features/Kitchen/KitchenSettlementService.cs:20`: hash latest settlement IDs theo lớp; Summary cộng latest, không cộng history; ack idempotent cùng actor/token, stale 409. `SettlementAcknowledgementConfiguration.cs:14` unique `(MealDayId,AcknowledgedBy,SnapshotToken)`; `PortionSettlementConfiguration.cs:13` unique `(MealDayId,ClassId,Version)` có filter.
- `be/src/MealTrace.Application/Features/Kitchen/IngredientService.cs:78` lưu full macros/source/actor/supersedes; `RecipeService.cs` `BuildLinesAsync` lưu quantity/unit/basis, đòi density; `NutritionService.cs` `Compute` áp edible một lần, rounding cuối, legacy unknown macros trả null. Migration `be/src/MealTrace.Infrastructure/Persistence/Migrations/20261028090000_KitchenV13Nutrition.cs` thêm nullable fields, reference ranges, constraints; không backfill density/nguồn giả.
- `be/src/MealTrace.Infrastructure/Persistence/Migrations/MealTraceDbContextModelSnapshot.cs:516`, `:537`, `:990` có MenuDish, NutrientReferenceRange, ReportSnapshot; không có model physical supplier/source/allocation. `be/src/MealTrace.Infrastructure/Persistence/MealTraceDbContext.cs:98` chỉ guard các loại history liệt kê; không bảo vệ ReportSnapshot/MenuDish/MealEvidence.
- `be/src/MealTrace.Application/Features/Meals/MealService.cs:42`, `:60` và `be/src/MealTrace.Infrastructure/Persistence/Repositories/MealRepository.cs:43` đã lấy latest settlement; `FindReportLineageAsync` ở dòng 68 chỉ đọc snapshot đã có. Không tạo report workflow từ một GET.
- Audit dùng `IAuditSource`, `AuditService.ListAsync/ExportAsync`, `EfAuditSource`, `CoreAuditSources`, `PortionAuditSources`, `StudentAuditSources`; danh sách DI tại `be/src/MealTrace.Infrastructure/DependencyInjection.cs:37`. Chưa đăng ký adapter nutrient/recipe/ack/menu/supplier/serving/report. `AuditService.Cell` escape quotes và neutralize formula; CSV audit **không phải** CSV/PDF report dinh dưỡng.

### Client integration và hazards còn lại

`fe/src/features/kitchen/KitchenWorkspace.tsx:57` gọi ingredient/recipe/nutrition thật; `kitchenApi.ts:9` gọi settlement search/diff/ack. `fe/src/features/access/AdminToolsPage.tsx:62` dùng audit thật. `mobile/lib/features/school/data/school_repository_impl.dart:20` có kitchen routes và dòng 35 report lineage; `report_lookup_page.dart:46` chỉ lookup theo ID. Test client tồn tại nhưng chưa chạy. Không tìm thấy capture/photo queue/serving-source API trong `mobile/lib`, `mobile/test`; `mobile/pubspec.yaml` có file_selector/http/secure storage, không chứng minh queue bền vững.

1. **Free-text meal types:** `MealCalendarService.NormalizeTypes` dòng 19 chỉ trim/length/distinct; `WorkflowService.CreateMealDayAsync` dòng 437 nhận chuỗi. `SchoolTime.Cutoff` chỉ nhận Breakfast hoặc chuỗi chứa “sáng”; chuỗi không rỗng khác lấy 07:00, null lấy 07:30. Sai chính tả/alias có thể áp sai bữa.
2. **Legacy 07:30 còn active:** `SchoolTime.EarliestEnrollmentDate` dòng 30; `MealCalendarService` dòng 90,176,215,227; `StudentAdministrationService` dòng 114,137,153,172. Không thay global constant rồi rewrite CutoffAt lịch sử. Đặc biệt worker trễ + thay ghi danh lúc 07:15: `EnrollmentQueries.OnDate` dòng 9 chỉ xét EndDate, chưa tái dựng theo EndRecordedAt; đây là **rủi ro suy từ code cần regression**, chưa có failing test được tái hiện ở audit này.
3. **Tuổi/pinned:** có CompareAsync nhưng reference data chỉ có fixtures giả; không có seed approved ranges. Pinned nutrient không đồng nghĩa frozen age reference: CompareAsync vẫn chọn range theo thời gian hiện tại trong pinned mode. Menu/report phải lưu range IDs, basis (cả ngày hay từng bữa), inputs và rounding lúc approval.
4. **Legacy `ml`:** dòng cũ không Quantity/Unit được tính từ GramsPerPortion, không đủ chứng cứ density gốc; không tự sửa lịch sử hoặc “chữa” bằng density mới. Flag incomplete provenance, tạo correction có người chịu trách nhiệm.
5. **Ảnh/private access:** MealEvidence.PhotoUrl chưa có approval/client upload key/class/actor. `fe/src/features/meals/MealDaysPage.tsx:58` mở URL; không có private retrieval enforcement để chứng minh FR-20. Không kết luận đã rò ảnh thực tế khi chưa có upload workflow.
6. **Inspector:** BM04 M5 ghi Inspector nhưng `RoleNames.ReportReaders` chỉ A; `InspectorGrant`/authentication có grant hạn, không có report-read policy đầy đủ. Cần quyết định quyền đọc scoped, không cấp Admin để vượt gate.
7. **PG/migration:** SQLite/fakes không chứng minh row locks, nullable unique filters, serializable conflicts, transaction rollback hay nâng cấp legacy DB. Source/version/report mới phải có DB constraints và kiểm thử race riêng.

## 4. Reconciliation Markdown lịch sử

Các citation dùng repo-relative `file:line`, không dùng link ổ đĩa Windows. Không sửa tài liệu cũ trong run này.

| Claim cũ và vị trí | Kết luận current main / việc còn đúng |
| --- | --- |
| `PROPOSAL.md:59`, `:152`, `:161`; `docs/backend-task-assignment-v1.3.md:52`: loại nhà cung cấp/trace khỏi scope | **HISTORICAL/OBSOLETE** phần immediate supplier: v1.4 FR-11/15/06/23 bắt buộc supplier directory, dated source-entry, split actual-use. Procurement/kho/certification/fee ledger vẫn excluded |
| `docs/backend-phase1-original-spec.md:25` dùng `/nutrient-versions`; `:68` 07:30 chung | Route thực `/ingredient-versions`; giờ chính 21:00/07:00; 07:30 chỉ là legacy hazard, không chuẩn nghiệm thu |
| `docs/backend-task-assignment-v1.3.md:40`, `:110`, `:118`, `:122`; `docs/gap-analysis-v1.3.md:39`, `:40`, `:50` | Baseline `4005331`, branch-only ack, worker missing đã lỗi thời. PR #8 + worker 5 tests pass; ack đã merge và SQLite regressions pass |
| `docs/backend-task-assignment-v1.3.md:71`, `:74`; `docs/gap-analysis-v1.3.md:54`–`:61` | Full macros/source/gross-edible/density/rounding/modes đã có và 21 tests pass. Chỉ tiếp tục phần dữ liệu tuổi thật, legacy provenance, frozen menu/report, PG |
| `docs/backend-task-assignment-v1.3.md:187`–`:190` centralized audit MISSING | Đã merge PR #7; giữ service/CSV/privacy, bổ sung adapters còn thiếu; không dựng audit engine thứ hai |
| `docs/gap-analysis-v1.3.md:68` direct Parent link dùng current ClassId | Đã sửa: `WorkflowService.LinkParentAsync` dùng `TeacherCanManageStudentAsync` tại school date; `ScopedStudentSearchTests.TeacherParentLinkFollowsCurrentEnrollmentInsteadOfFutureClassPointer` pass. Không giao sửa lại scope đã đúng |
| `docs/gap-analysis-v1.3.md:66`, `:67`, `:69` audit direct link/account scope và temporal TeacherAssignment | Vẫn cần khép: adapters hiện có không ghi đầy đủ before/after của direct link và replace account scopes. P3-B03 phải kiểm/fix history mới và policy historic scope, không chỉ thêm source adapters |
| `docs/backend-task-assignment-v1.3.md:336`–`:340`; `docs/implementation-plan.md:5` pending assignment G4–G8 | Nay giao rõ Hùng menu/source/estimate, Nam serving/allocation/photo API, Bảo reports/trace/CI; Cường Android, Đạt FE theo BM04 |
| `docs/implementation-plan.md:3` proposal quyết định scope; `:14`, `:20`, `:21` mô tả phase cũ | Nguồn chuẩn đổi sang registration v1.4/BM04. Không lặp nhiệm vụ worker/ack/macros đã merge |
| `docs/gap-analysis-v1.3.md:11` 167 pass/29 skip; `docs/implementation-plan.md:25` test cũ | **HISTORICAL**, thay baseline audit bằng 278 pass/34 skip; không cộng hai lần hoặc gọi đó là 30 BM04 |
| `continuity.md:93` test worker “trên nhánh nam” | Nay đã ở main. `continuity.md:102` thiếu dữ liệu khoảng tuổi và `:148` chưa device E2E vẫn phù hợp bằng chứng audit |
| `continuity.md:156` recipe/report lookup theo mã, không thêm menu API giả | Vẫn đúng trạng thái current main; P2 cần catalogue nhỏ để FE/Android chọn recipe/source thật |
| `docs/backend-task-assignment-v1.3.md:53`; `PROPOSAL.md:170` | Vẫn đúng: centralized audit không thay report lineage/replay; không cần full event sourcing toàn hệ thống |

## 5. Toàn bộ 13 BM02 High findings

Không sửa Team response trong BM02. “Fixed” dưới đây chỉ giới hạn phần có chứng cứ; không tự đóng finding thiết kế ký duyệt. `docs/review-2/` SRS/SDD và `docs/diagrams/` không đổi giữa tag và main.

| ID | Trạng thái re-audit | Bằng chứng và điều kiện đóng |
| --- | --- | --- |
| SC-01 | STILL OPEN trong SRS | Catalogue FR chưa được cập nhật trong DOCX; ma trận §2 này là planning evidence, không tự là SRS được duyệt. Đạt điều phối FR→UC→test, reviewer ký |
| RE-01 | STILL OPEN trong SRS | Acceptance/exception chi tiết chưa được cập nhật ở SRS §§2.2.2,3.2. Task P2/P3 bổ sung yêu cầu thực thi; cần đồng bộ SRS |
| RE-03 | PARTIAL | Cutoff/RBAC tests chạy; PostgreSQL skip, offline test thiếu, SRS §4.2.4 chưa đổi. Nam/Bảo/Cường phải có logs cho NFR-02–04 |
| DA-01 | STILL OPEN | Không thấy `.sql` tracked; migrations/snapshot có, chưa xác minh deployed schema. Bảo xuất script và thử migration trên DB biệt lập |
| DA-02 | STILL OPEN | DbContext/domain/ERD thiếu dated sources + split use. Hùng/Nam bổ sung source-entry/use/allocations, FK và test trace |
| DA-03 | PARTIAL | NutrientReferenceRange mới có; MenuDish/MealEvidence/ReportSnapshot vẫn scaffold, còn thiếu menu/serving/month report/estimate lifecycles. Không đóng bằng đổi tên bảng |
| DA-06 | PARTIAL; phần field đã FIXED IN NEWER MAIN | IngredientVersion fat/carb/SourceRef, RecipeIngredient Quantity/Unit/WeightBasis + migration hiện hữu; `CreateRecipeStoresOriginalQuantitiesBasisRoundingAndActor`, `NutritionPerServingCoversKcalAndAllMacros` pass. ERD parity/migration thực chưa xác minh |
| FL-01 | PARTIAL | Nutrition/age comparator có và unit tests pass; menu publication/ingredient estimate chưa có. P2-H02/H03, P3-H01/H02 |
| FL-02 | STILL OPEN | Không serving write/offline queue/private approved photo; không EvidenceSync tests. P2-N02, P3-N01 và Cường |
| FL-03 | STILL OPEN | `MealService.GetReportLineageAsync` chỉ read scaffold, chưa generation/approval/export/replay. P2-B02, P3-B01/B02 |
| FL-04 | STILL OPEN | AD-05/06 chưa thêm actual-source steps; model/API cũng thiếu; task không được chỉ thêm supplier master |
| DC-01 | UNVERIFIABLE / chưa đóng | SDD detailed APIs/PK-FK/class/sequence còn placeholders; endpoint inventory mới không thay thiết kế được duyệt. Team + reviewer phải đối chiếu source/docs |
| DC-02 | FIXED IN NEWER MAIN cho dòng mới; PARTIAL cho legacy | `RecipeService.BuildLinesAsync`, `NutritionService.Compute`; `MlLinesNeedAConfirmedDensityAndAreNeverTreatedAsGrams` pass. Không chứng minh dòng ml legacy đã sửa đúng; phải rà và flag/correct với evidence |

BM04 §9 **Fix-01 chỉ thêm Supplier/BatchNumber vào Ingredients không đủ**: một ingredient có nhiều ngày nhận, nhiều nguồn, một dish có 2 allocations cùng ingredient; cần separate source-entry và quantity-use junction, actor/session/class, prior versions. CookId/CaregiverId trong diagram phải có semantic mapping, không thêm tên cột cho đủ checklist.

### Mâu thuẫn BM04 cần reviewer giải quyết

- §6 mọi WF ghi 5 cases/4 CI; §8 ghi WF-03 4/3, WF-04 4/4, WF-05 6/4, WF-06 6/5. §6 vẫn cộng 30/24 nhưng phân bổ sai, không cho phép chuyển ownership ngầm.
- §6.1 demo dùng DS-01/DS-02, §8 dùng DS-01a/b; §7 dùng DS-05, DS-06…DS-12 không thống nhất 6 cặp §8. Dùng §8 IDs tạm thời, giữ 12 stories.
- §6 các dòng state test/total gán Bảo hoặc Hùng sai workflow owner; §7 fields bị lệch (CI/demos/due), §7.4 gán SR-07 cho Đạt nhưng §9 gán Cường. **Giữ WF-05 Cường, WF-01 Đạt theo §8**, BE2 chỉ API collaborator.
- §4 có measurement-method lệch: accuracy ghi RBAC, security ghi timing; §7 có p95≤2s và NFR numbering cũ, trái registration/BM04 §4 register≤5s/report≤10s. Cần reviewer xác nhận protocol, không tự đổi signed commitment.
- §6 menu path “submitted → published/rejected” nhưng §3 đòi approved version. Đề xuất explicit approved trước published hoặc một thao tác approve-and-publish atomic; P2 chọn explicit states để test rõ.
- §9 fixes due 12/11 và SR-07 due 15/11; §7 tests due 15/11, CI 20/11. Ba tuần 16/11–06/12 là **kế hoạch phục hồi**, không tự gia hạn cam kết BM04. Bảo xin xác nhận reviewer về các mốc trễ.
- M5 nói Inspector, source chỉ Admin report reader; cần policy grant đọc có phạm vi. Photo gap/report approval gap handling phải chốt: proposal chặn approve khi thiếu evidence bắt buộc, vẫn cho draft/trace có gap; không được gán complete giả.

Ledger provisional theo **BM04 §8**, không cộng test helper/regression thành functional cases mới:

| Owner / WF | Cases | CI |
| --- | ---: | ---: |
| Đạt / WF-01 M1 | 5 | 4 |
| Hùng / WF-02 M3 | 5 | 4 |
| Nam / WF-03 M2 | 4 | 3 |
| Nam / WF-04 M2 | 4 | 4 |
| Cường / WF-05 M4 | 6 | 4 |
| Bảo / WF-06 M5 | 6 | 5 |
| **Tổng unique** | **30** | **24** |

Gate: 6/6 main và 6/6 exception demos, ≥95% overall, 100% mandatory main paths, 0 open Critical/High. 29/30 mới đạt ≥95%; mọi skip cần chạy lại hoặc làm fail gate mandatory. 278 test pass hiện tại không chứng minh đạt ledger 30 mới.

## 6. Critical path, năng lực và rủi ro

**16/11 contract freeze → 17–18/11 supplier/source + cutoff hardening → 19–20/11 menu publish + serving split-source + report draft/trace trên PostgreSQL → 23–27/11 estimates, frozen age data, photo/private view và report approval/recompute → 30/11–02/12 histories, PostgreSQL regressions, CSV/PDF, source replay → 02/12 feature freeze → 03–04/12 staging/device/24 CI cases/12 demos + RC → 06/12 release gate.**

Hùng xuất supplier/source IDs, menu/recipe/nutrition versions. Nam tiêu thụ các IDs và latest settlement token, ghi actual-use cùng actor/class/session. Bảo tiêu thụ source/serving version IDs để report/trace; không được báo cáo từ recipe kế hoạch rồi gọi đó là actual supplier. Cường nhận contract photo/idempotency ngay 16/11, viết durable queue song song; Đạt bắt đầu menu UI với contract fixture, đổi sang API thật trước exit P2. **Bảo là migration integrator duy nhất**; Hùng/Nam cung cấp model/config changes, không đồng thời generate snapshot.

Nguồn lực kế hoạch: mỗi BE **5 ngày P2 + 10 ngày P3**, tổng 15 core weekdays × 8 giờ = **tối đa 120 giờ/người/3 tuần** (360 giờ BE tổng); task ước tính P2/P3 giữ nguyên, phần thời gian tăng dành cho tích hợp, test, fix, tài liệu và nghiệm thu, không thêm scope; cuối tuần không là năng lực mặc định. Cần **Cường và Đạt dành trọn 15 ngày làm việc** cho client/test/demo, reviewer/trường chốt policy từ16–17/11, reference chậm nhất 26/11 và nghiệm thu 04/12. Nếu không có đủ thời gian cộng tác, báo đỏ deadline; không cắt Must-have hoặc giao Android app cho 3 BE.

| Risk | Mức / impact | Owner, hạn xử lý | Fallback trong scope và evidence đóng |
| --- | --- | --- | --- |
| R1 physical source/use chưa có | P0; không trace được món thực phục vụ | Hùng→Nam→Bảo; contract 16/11, slice 20/11 | UI CRUD đơn giản, source-entry có ngày/lượng/ref + split use thật; trace 600g+400g tới 2 nguồn trên PG, missing-source flag; không thay bằng SourceRef nutrition |
| R2 menu/report/serving thiếu immutable workflow | P0; approved data đổi ngầm | Hùng/Nam/Bảo; 20/11 nền, 02/12 hoàn chỉnh | Daily menu version ghép week view, report template đơn giản; snapshot/hash/append-only + replay test sau correction |
| R3 meal-type/07:30/delayed enrollment | P0; chốt sai trẻ/suất | Nam; 17/11 | Map alias explicit, giữ CutoffAt cũ, disable input không xác định; regression 21:00/07:00 và delayed transfer sau cutoff; không chốt tay thay worker |
| R4 Android photo/private bytes | P0; mất ảnh hoặc sai guardian scope | Cường WF-05, Nam API, Bảo security; handoff 16/11, device 03/12 | Queue đơn giản bền vững, authenticated streaming; kill/restart/retry/device logs + 403 ngoài scope; không public bucket/demo online thay offline |
| R5 thiếu PG/CI/staging evidence | P0; race/migration không được kiểm chứng | Bảo điều phối, từng owner chạy; PG 20/11, 24 CI 04/12 | PG riêng/schema test, CI self-hosted nếu cần; logs SHA/migration/rollback/30 IDs; không nhận SQLite thay PG |
| R6 range tuổi/legacy nutrition chưa chứng thực | P0 với nutrition nghiệm thu | Hùng + trường; source 17/11, freeze 26/11 | Thiếu nguồn trả 422, incomplete flag; không lấy ví dụ fake làm ngưỡng mầm non; fixture tính tay và reference approved |
| R7 tài liệu BM02/BM04 chưa khớp | P0 với eligibility High=0 | Bảo/Đạt và reviewer; quyết định 16/11, closure 04/12 | Phụ lục giải thích được reviewer xác nhận, không sửa signed totals; từng High có commit/test/design evidence |
| R8 vượt capacity 15 core weekdays | P0 với deadline | Bảo/team; đánh giá mỗi ngày 17:00 | Giảm polish, dùng CSV/PDF mẫu gọn, một trường; thêm collaborator time đã chỉ rõ. Không bỏ FR-16/17/24 hoặc đẩy Must-have sau 06/12 |

## AUDIT LIMITATIONS

- Không chạy migration thật, PostgreSQL, FE/Flutter, device, staging, benchmark 3 năm hoặc provider OTP thật. Không khẳng định deployed DB đồng nhất snapshot. Không kiểm tra toàn bộ network/storage infrastructure.
- Đã đọc DOCX text/table; không xác minh chữ ký và hình render SRS/SDD/ERD như reviewer thị giác. Diagram sources và diff được đọc; findings thiết kế chưa được tự đóng.
- Thiếu failure repro cho hazard delayed enrollment/legacy type; nêu là rủi ro có code evidence, không bịa test fail. Nutrition age fixtures không phải nguồn y tế được xác thực.
- Trạng thái VERIFIED giới hạn từng hành vi đã chạy; không có FR mới nào được coi xong chỉ nhờ tồn tại class. Routes/tasks của hai tài liệu phân công là đề xuất đến khi PR/OpenAPI chứng minh.

## NEXT HUMAN ACTIONS

1. Bảo xác nhận kế hoạch phục hồi 16/11–06/12 với reviewer, giải quyết ledger/Inspector/gap-approval và overdue BM04; giữ 30 unique/24 CI cùng owners §8.
2. Team chốt shared contract 16/11 trước 12:00, Bảo tích hợp migration; Hùng xin trường xác nhận age reference/units/basis trước 17/11.
3. Cấp DB test biệt lập, staging/private image storage và Android device; bố trí Cường/Đạt đủ 15 ngày. Không gửi credential vào tài liệu.
4. Tạo issues theo [Phase 2](backend-task-assignment-phase2-v1.4.md) và [Phase 3](backend-task-assignment-phase3-v1.4.md); mọi closure cần source SHA + actual run evidence. Audit này không tự tạo issue/commit/merge.

---

## LEAD REVIEW ADDENDUM — 15/11/2026

**Nguồn:** các mục 1–6 phía trên là kết quả audit độc lập của Codex. Lead đã đối chiếu với bản `backend-review2-audit-2026-11-15.md` lập từ GitHub code inspection. Cả hai đồng ý về critical path; ưu tiên kết quả chạy thực `278 pass / 34 skip` của Codex so với nhận xét source-only trước đó.

**Các quyết định mới:** (1) lập CI skeleton 16–17/11, không chờ P3-B03; (2) một người tích hợp EF migrations — Bảo, các BE khác sở hữu model/service; (3) triển khai photo backend sớm nhất có thể vào đầu P3 để Cường có API thật; (4) phân công closure 13 BM02 High theo owner, phân biệt **design findings** với **open Critical/High runtime defects** và yêu cầu bằng chứng reviewer; (5) approval khi thiếu supplier source **chưa được BM04 quy định duy nhất một cách**. Đừng áp đặt 422 như yêu cầu ký duyệt khi chưa có quyết định bằng văn bản.

**Lưu ý số liệu:** 278/278 test đã chạy pass, nhưng 34/312 chưa được thực thi. Vì vậy không gọi 278/312 là tỷ lệ nghiệm thu toàn dự án, không dùng test skipped để thỏa >=95% BM04, và chưa tính test có sẵn như 30 unique functional cases cam kết.

**Giới hạn lead review:** Không chạy lại test/migration ở vòng so sánh này, không có SHA mới hơn audit 15/11; các route NEW và số giờ task là **kế hoạch**. Không tự đóng BM02, không push hoặc merge code.

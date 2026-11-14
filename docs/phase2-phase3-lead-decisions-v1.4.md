# MealTrace — Lead review & quyết định chốt Phase 2–3 (v1.4)

> **Trạng thái:** Đề xuất FINAL để lead/team ký nhận ngày 15/11/2026, **chưa phải BM04 sửa đổi**. Repository baseline đối chiếu: `origin/main@a6b187e6eae9553f7966d5444b8745f9896d2a11`. Phạm vi chốt: Registration v1.4 approved trong BM04; review 2 **Conditional pass**. 
> **Nội dung so sánh:** bản lead trước (`MealTrace_Backend_2Weeks_Review2_v1.4.zip`), 3 file Codex audit/assignments mới và BM01–BM04. Không có production code được thay đổi trong vòng so sánh này.

## 1. Kết luận review: ACCEPT WITH REQUIRED CORRECTIONS

- **Nhận:** audit kỹ thuật Codex là nguồn trạng thái đáng tin hơn bản lead trước vì Codex đã fetch và chạy backend tests; **278 passed, 0 failed, 34 skipped** trên cùng SHA. Bản lead trước chỉ đọc GitHub source, không chạy test. Không chuyển 34 skip (33 PostgreSQL, 1 performance) thành pass, chưa xác minh FE/Flutter/device/staging.
- **Nhận:** business scope, ownership, vertical slice, model/DTO/test inventory và chi tiết task Codex. Bản lead cũ đúng về đường găng source→serving→report nhưng thiếu sự cụ thể của Codex.
- **Sửa:** timeline quá gấp của Hùng: H01 14h + H02 14h không thể đều hoàn tất 17–18/11 từ lúc freeze trưa16/11. Dùng interface/model/IDs handoff trước, API đủ sau; hoàn thiện H03 ngày20.
- **Sửa:** Bảo không thể làm report draft 20h *sau* khi Nam hoàn tất serving ngày19; Bảo phải code schema/reader/aggregation dựa trên contract ngay từ16–18 và tích hợp dữ liệu thật20.
- **Sửa:** CI bắt đầu ở Phase 2, và 13 BM02 High có owner khác nhau; không giao tất cả việc triển khai + docs + CI cho Bảo vào cuối P3.
- **Sửa:** photo backend phải có API staging tối thiểu ngày23/11 để Android kiểm tra retry trước feature freeze, không chỉ gửi contract đến25.
- **Giữ mở:** nguồn nhóm tuổi được trường duyệt; Inspector scoped-read; policy approve report thiếu source; BM04 §6/§7/§8 số lượng/owner/due không khớp. Bảo xin decision record từ reviewer, tuyệt đối không sửa văn bản cam kết để đổi số.

## 2. Quyết định kiến trúc và việc không được phép làm

| ID | Quyết định bắt buộc | Rationale/test | Owner |
| --- | --- | --- | --- |
| LD-01 | Giữ 4 role chuẩn; Inspector dùng grant scoped/expiry hiện có, không thêm staff role thứ năm | FR-22, BM04 M5 | Bảo |
| LD-02 | SupplierRevision → dated IngredientSourceEntryVersion là **nguồn nhận thực tế**, khác nutrient SourceRef; IngredientUse/Allocation gắn ServedDish và actor/class/meal | Một ingredient có 2 source 600g+400g; FK/version lưu đủ | Hùng→Nam |
| LD-03 | `MealDay` là session hiện có; settlement luôn latest **mỗi class**, amendment tạo version, ack token cũ giữ lịch sử | Worker PG concurrent, stale 409, no historical double count | Nam |
| LD-04 | Menu, source, serving, approved report đều versioned/append history; report As-Approved là frozen inputs/outputs, Recomputed là artifact riêng | Supplier-only correction có delta số = 0 nhưng source diff != 0 | Hùng/Nam/Bảo |
| LD-05 | Ảnh phục vụ private bytes, idempotent `clientPhotoId` + checksum, capturedAt giữ nguyên; queue file+metadata tồn tại qua restart | Không có public object URL, parent unrelated 403 | Nam↔Cường |
| LD-06 | Một migration integrator duy nhất xử lý EF snapshot và SQL: Bảo. Không sửa migration đã deploy, không ba nhánh cùng generate snapshot | Fresh + upgrade PostgreSQL, rollback/restore rehearsal | Bảo |
| LD-07 | Planned ingredient estimate khác actual use; không mua hàng/tồn kho/giảm stock; bỏ FR-21 optional khỏi critical path | Đúng Registration v1.4 | Hùng/Nam |
| LD-08 | Không viết lại worker/ack/nutrition P/L/C/audit core đã có; chỉ fix lỗi test tái hiện được và bổ sung phần thiếu | 278 pass, but PG 33 skip | Cả nhóm |
| LD-09 | Policy report **missing supplier evidence** phải có quyết định reviewer: block 422 **hoặc** approved-with-disclosure. Không tự tuyên bố BM04 bắt buộc cách A | BM04 M5 cho phép explicit gap; test theo policy đã chốt | Bảo + reviewer |
| LD-10 | `.github/workflows` phải tạo ở P2, không đợi cuối P3; 30 test unique/24 CI có evidence SHA | BM04 §8, runtime tests != BM04 cases | Bảo + 5 owners |

## 3. Sức chứa thực tế và mốc bàn giao

- **16/11 (thứ Hai):** 12:00 freeze model/DTO/IDs, đề nghị reviewer xác nhận age reference/report source-gap/Inspector; khởi tạo CI workflow. Không đợi toàn bộ API để FE/Android bắt đầu.
- **17–18/11:** source model/IDs → Supplier APIs; cutoff test-first; report reader theo interface; CI skeleton + PostgreSQL; Hùng/Duy Nam nộp migrations notes cho Bảo.
- **19–20/11:** Menu publish + actual serving/source allocation + report draft/trace trên PostgreSQL cùng seed. **20/11 17:00** là gate có log; không đủ thì 21–22/11 chỉ sửa blocker, không thêm module ngoài scope.
- **23/11:** Nam giao staging upload tối thiểu 17:00, Cường bắt đầu retry thật; BE3 triển khai report approval/recompute, BE1 estimates + approved ranges.
- **24–27/11:** photo review/private bytes (đủ 27/11), estimates và approved age reference (đủ 26/11), report approval/recompute phát triển theo frozen inputs; 27/11 checkpoint tích hợp, **chưa feature freeze**.
- **28–29/11:** buffer tự nguyện; không release gate hoặc công việc bắt buộc.
- **30/11–02/12:** history/corrections và monthly approve/recompute, PostgreSQL race, PDF/CSV/export, Inspector; **feature freeze 02/12 17:00**.
- **03–04/12:** Android device kill/restart, FE integration, 24 CI designated test, 12 demo workflows, 3-year benchmark + **release candidate 04/12 17:00**.
- **05–06/12:** chỉ blocker/retest/restore; **06/12 17:00** go/no-go. Không tự coi weekend là capacity đã cam kết.

**Budget:** 3 BE × (40h P2 + 80h P3) = **360h capacity tối đa** trên **15 core weekdays**, không phải 360h task estimate. Các task IDs/giờ đã lập giữ nguyên; 40h thêm mỗi BE ở P3 dùng cho integration, security/PG tests, CI, sửa lỗi và closure BM02; không thêm yêu cầu chức năng. **Không tính cuối tuần là ngày công mặc định.** Đạt và Cường cần 15 core days song song; nếu thiếu 2 người này, BM04 WF-01/WF-05/CI+device không thể được 3 BE nghiệm thu thay. Các giờ là *estimate*, cần WIP limit và daily replan.

## 4. Ranh giới bàn giao (chống ba BE chờ nhau)

| Handoff | Minimum ready | Consumer | Due |
| --- | --- | --- | --- |
| Hùng → Nam | `SupplierVersionId`, `IngredientSourceEntryVersionId`, ingredientId, date, quantity/unit, reference; stable errors/history | Nam | 17/11 model; 18/11 API |
| Hùng → Nam/Bảo | `MenuVersionId` published, recipeVersionIds, nutrient calculation snapshot (ranged/unknown labelled) | Nam, Bảo | 19/11 API; 20/11 pinned snapshot |
| Nam → Bảo | `ServingVersionId` + ServedDish + actual IngredientUse allocations/gaps + responsible actor/class/session, latest settlement version | Bảo | 19/11 graph; 20/11 live API |
| Nam → Cường | `clientPhotoId`/checksum/`capturedAt`/`receivedAt`, retry idempotency, HTTP error matrix, private bytes policy | Cường | 17/11 signed spec, 23/11 staging upload (full 27/11) |
| Bảo → Đạt | Report draft/approved/recomputed views, explicit source gaps, PDF/CSV export and error contract | Đạt | 20/11 draft, 01–02/12 report endpoints |

## 5. BM04 acceptance ledger — giữ tổng đã cam kết, ghi ranh giới bằng chứng

| Workflow | Accountable | Unique cases | Required in CI | Demo |
| --- | --- | ---: | ---: | --- |
| WF-01 | Đạt | 5 | 4 | DS-01a/b |
| WF-02 | Hùng | 5 | 4 | DS-02a/b |
| WF-03 | Nam | 4 | 3 | DS-03a/b |
| WF-04 | Nam | 4 | 4 | DS-04a/b |
| WF-05 | Cường | 6 | 4 | DS-05a/b |
| WF-06 | Bảo | 6 | 5 | DS-06a/b |
| **Tổng** | **Cả 5** | **30** | **24** | **6 main + 6 exception** |

**Ý nghĩa ngưỡng:** phải có tối thiểu 30 unique case, >=24 designated CI cases, >=95% overall pass và 100% Must-have main paths pass; không có open Critical/High runtime defect. Nếu có đúng 30 case được thực thi, 29/30 ≈96,7%, nhưng 1 failed case thuộc main path/High vẫn no-go. BM02 High design findings cần evidence closure hoặc reviewer disposition, **không được vừa làm ngơ vừa đánh tráo thành runtime defect**.

## 6. Quy tắc quản trị Git, status và go/no-go

1. Mỗi PR gắn task ID, base SHA, exact modified paths, OpenAPI diff, result command, PG/sql/replay evidence, negative authorization test, reviewer. Không merge code chưa review hoặc sửa chung EF snapshot trên nhiều nhánh.
2. Status chỉ có `NOT_STARTED`, `IN_PROGRESS`, `READY_FOR_REVIEW`, `MERGED_UNVERIFIED`, `VERIFIED_IN_MAIN`, `BLOCKED`. `DONE` = `VERIFIED_IN_MAIN` + linked test/demo evidence; 278 pass baseline không chứng minh task NEW.
3. 17:00 hàng ngày check 5 điểm: model/API handoffs, test/CI, migrations, FE/Android integration, blockers/risks. Task trễ phải có owner, explicit ETA internal, impact, giảm UI polish (không hạ Must-have), hoặc reviewer disposition.
4. Nhiều source cùng ingredient phải hỗ trợ phần thiếu rõ ràng; không suy supplier từ Ingredient master hay nutrient source. Parent chỉ thấy approved published và scoped child.
5. 04/12 chốt release-candidate SHA; 06/12 `GO` chỉ khi mọi signed BM04 gate có chứng cứ, `NO-GO` khi không đủ. Không thay đổi Phiếu v1.4 hoặc BM04 đã phê duyệt.

## 7. Những việc lead cần duyệt trước khi commit / code

- [ ] Confirm `main` HEAD mới, P2/P3 chưa có task NEW đang ở branch riêng.
- [ ] Confirm Hùng/Nam/Bảo có 15 core coding days, Cường/Đạt có 15 core integration days; sắp xếp reviewer PG/staging/device.
- [ ] Confirm Hùng/BE1 gửi yêu cầu reference nhóm tuổi mầm non có source/basis tới trường trước 17/11; không dùng nutrient food table làm age-range threshold.
- [ ] Chốt policy Inspector scoped-read + source-gap approval bằng ghi chú reviewer; nếu chưa chốt, đánh dấu BLOCKED, không tự claim compliance.
- [ ] Chốt test ledger BM04 §8 (30/24) làm ledger kỹ thuật *tạm*, xin reviewer xác nhận các điểm §6/§7 sai lệch; không sửa BM04 lén.
- [ ] PR CI skeleton từ 17/11; bảo đảm test cần PostgreSQL chạy trên DB riêng và không chạy secrets/seed trên production.
- [ ] Commit ba file `audit/P2/P3` và tài liệu này lên `docs/`; prompt Codex giữ ngoài repo hoặc lưu riêng `docs/prompts/` nếu team muốn provenance, **không dùng như kế hoạch được duyệt**.

**Phê duyệt kế hoạch:** Lead: _______ · Ngày: _______ · Baseline code SHA (re-fetched): _______ · Policy source gap: A / B / Pending · Người quyết định acceptance: _______.

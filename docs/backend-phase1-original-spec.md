# MealTrace

## Đặc tả chi tiết API Pha 1

*Nền tảng khởi tạo: BE 1, BE 2, BE 3 · Cập nhật 04/10/2026*

## Quy ước chung

Cả 3 bạn BE tuân thủ các quy ước sau:

- Prefix /api, xác thực JWT Bearer. Lỗi trả application/problem+json kèm mã nghiệp vụ (code). Xung đột phiên bản hoặc đồng thời trả 409, sai quyền 403, chưa đăng nhập 401.
- Thời gian dùng DateTimeOffset (+07:00), lấy giờ qua TimeProvider, không dùng DateTime.Now.
- Danh sách phân trang page/pageSize (mặc định 25, tối đa 100), sắp xếp ổn định.
- Path là đề xuất. Endpoint đọc đã tồn tại thì đối chiếu với Swagger và giữ route cũ, chỉ thêm endpoint mới.
Sửa so với bản phân chia trước: nạp dữ liệu dinh dưỡng đổi từ PUT sang POST .../nutrient-versions (append-only nên không cập nhật tại chỗ). "Xác nhận bản chốt" đổi sang cấp meal-day để bếp bấm một lần cho tất cả lớp.

## BE 1: Nguyên liệu, dinh dưỡng, công thức có phiên bản (Bitemporal)

### 1. Danh sách API, input và output

| Method | Path | Input chính | Output chính |
| --- | --- | --- | --- |
| POST | /api/kitchen/ingredients | name, unit (g/ml), ediblePercent (tỷ lệ ăn được, 0–100), category | 201 {id, ...} |
| GET | /api/kitchen/ingredients?search=&page= | query | Danh sách phân trang kèm dữ liệu dinh dưỡng đang hiệu lực |
| POST | /api/kitchen/ingredients/{id}/nutrient-versions | validFrom, validTo?, trên 100 g: energyKcal, proteinG, fatG, carbG, source (bắt buộc), sourceRef, supersedesId? | 201 {versionId, validFrom, recordedAt, recordedBy} |
| POST | /api/kitchen/recipes | name, servingBasis (khẩu phần, số suất), lines[{ingredientId, quantity, unit}], roundingRule | 201 {recipeId, version:1} |
| POST | /api/kitchen/recipes/{id}/versions | expectedVersion, lines[], reason | 201 {version:n+1} |
| GET | /api/kitchen/recipes/{id}/versions/{v}/nutrition?validAt=&knownAt=&ageGroup= | query | {perServing:{kcal,P,L,G}, lines[{ingredientId, nutrientVersionId, contribution}], roundingRule, inputs} |

Tùy chọn, chỉ làm nếu có dữ liệu giá đáng tin: POST /api/kitchen/ingredients/{id}/price-versions theo cùng mẫu bitemporal. Giá nguyên liệu không đồng nhất với phí thu phụ huynh.

Quyền: Kitchen và Admin ghi/đọc. Teacher và Parent bị 403.

### 2. Business rules cần validate

1. Append-only: không có PUT/DELETE trên nutrient-versions và recipe versions. Sửa dữ liệu là chèn dòng mới, có thể trỏ supersedesId. Chặn UPDATE/DELETE ở DbContext như cách đã làm với MealRegistration.
2. Hai trục thời gian: validFrom/validTo là hiệu lực thực tế của số liệu; recordedAt là thời điểm hệ thống biết. Truy vấn validAt + knownAt chọn dòng có validFrom <= validAt < validTo và recordedAt <= knownAt; nếu nhiều dòng thì lấy recordedAt mới nhất.
3. Công thức tính (mỗi dòng): chất = quantity × (ediblePercent/100) × giá_trị_trên_100g / 100. Cộng các dòng rồi chia servingBasis. Làm tròn một lần ở cuối theo roundingRule đã lưu cùng phiên bản (làm tròn từng dòng sẽ lệch tổng).
4. Mỗi chỉ số trả kèm nutrientVersionId của từng dòng để Pha 3 dựng lineage.
5. Bắt buộc source và đơn vị khi nhập thủ công. Không cho nhập chỉ số rời khỏi dữ liệu nguồn.
6. Phiên bản công thức bất biến. Tạo phiên bản mới với expectedVersion cũ trả 409. quantity > 0, ediblePercent trong 0–100, không trùng nguyên liệu trong một công thức.
7. Thiếu dữ liệu dinh dưỡng tại validAt thì trả 422 NUTRIENT_DATA_MISSING kèm danh sách nguyên liệu thiếu, không tính ngầm bằng 0.
### 3. Kịch bản demo End-to-End

1. Bếp tạo "Gạo tẻ", "Thịt heo nạc", "Cà rốt" và nạp dinh dưỡng với nguồn.
2. Tạo công thức "Cháo thịt bằm" v1 và xem kcal, P-L-G trên khẩu phần.
3. Sửa hàm lượng đạm của thịt heo (valid từ tuần sau) rồi tạo công thức v2.
4. Tính lại validAt tuần này và tuần sau cho kết quả khác nhau.
5. Tính với knownAt trước lần sửa thì ra đúng số cũ, và vẫn truy được dòng dữ liệu nào tạo ra từng con số.
## BE 2: Bếp nhận bản chốt 07:30 và xác nhận

### 1. Danh sách API, input và output

| Method | Path | Input chính | Output chính |
| --- | --- | --- | --- |
| GET | /api/kitchen/settlements?date=&mealType= | query | Phiên bản mới nhất của từng lớp: {settlementId, class, version, total, settledAt, ackStatus} |
| GET | /api/kitchen/settlements/{id} | — | Tổng, danh sách tên trẻ, nguồn (Enrollment / báo vắng / ngoại lệ / điều chỉnh) |
| GET | /api/kitchen/settlements/{id}/diff | — | {baseTotal, currentTotal, added[], removed[], amendments[]} |
| POST | /api/kitchen/meal-days/{mealDayId}/acknowledgements | items[{settlementId, version}], note? | 201 {ackId, ackedBy, ackedAt, items[]} |
| GET | /api/kitchen/meal-days/{mealDayId}/acknowledgements | — | Lịch sử xác nhận (ai, lúc nào, bản nào, còn hiệu lực hay đã bị thay) |

Đã có (chỉ chuẩn hóa/thêm test): Admin chốt suất, các endpoint đọc và điều chỉnh sau chốt. Mới: 2 endpoint acknowledgement.

Quyền: Kitchen ghi/đọc, Admin chỉ đọc. Teacher và Parent bị 403.

### 2. Business rules cần validate

1. Cut-off: cutoffAt = mealDate 07:30 +07:00. Báo vắng và ngoại lệ chỉ ghi khi now < cutoffAt; đúng cutoffAt thì từ chối. Admin chỉ chốt khi now >= cutoffAt, mỗi phiên/lớp một lần. Kiểm tra lại now sau khi chờ khóa và ngay trước khi lưu (đã có pattern này, cần test biên 07:29:59 / 07:30:00).
2. Xác nhận chỉ áp dụng cho phiên bản mới nhất. version gửi lên không phải bản mới nhất thì 409 STALE_SETTLEMENT. Cả lô rollback nếu một item sai.
3. Admin duyệt điều chỉnh sau chốt tạo phiên bản mới, nên xác nhận cũ chuyển thành "đã bị thay thế" và bếp phải xác nhận bản mới (ackStatus = pending).
4. Append-only: không PUT/DELETE xác nhận. Lưu AckedByUserId kèm snapshot tên và AckedAt từ TimeProvider.
5. Mỗi user chỉ một xác nhận cho mỗi settlement version. Gửi lại thì trả 200 với bản đã có (idempotent), không tạo trùng. Nhiều nhân viên bếp vẫn được xác nhận riêng.
6. Chưa có bản chốt hoặc MealDay đã bị hủy (ngày nghỉ) thì 409.
7. Xác nhận không làm đổi số suất, và đây là suất dự kiến chứ không phải có mặt thực tế.
### 3. Kịch bản demo End-to-End

1. 07:29 Teacher ghi ngoại lệ thành công. 07:30 ghi tiếp thì bị từ chối.
2. Admin chốt hai lớp, bếp xem danh sách và thấy ackStatus = pending.
3. Bếp xác nhận cả ngày, trạng thái chuyển acknowledged.
4. Teacher gửi điều chỉnh +1 suất, Admin duyệt, bản v2 xuất hiện, diff hiện +1 và trạng thái quay lại pending.
5. Bếp xác nhận v2. Lịch sử cho thấy hai lần xác nhận, lần đầu ghi "đã bị thay thế".
6. Xác nhận bằng version cũ trả 409.
## BE 3: Audit log tập trung và siết RBAC

### 1. Danh sách API, input và output

| Method | Path | Input chính | Output chính |
| --- | --- | --- | --- |
| GET | /api/admin/audit-logs | from, to, actorId, entityType, entityId, action, page | Danh sách bản ghi chuẩn hóa |
| GET | /api/admin/audit-logs/{auditId} | auditId = {source}:{id} | Chi tiết, trước/sau |
| GET | /api/admin/audit-logs/entities/{entityType}/{entityId} | — | Timeline của một đối tượng, cũ → mới |
| GET | /api/admin/audit-logs/export?from=&to=&format=csv | query | Tệp CSV |

Bản ghi chuẩn hóa: {id, source, occurredAt, actor{id,name}, action, entityType, entityId, reason, before, after, correlationId}.

Cách làm (không đổi schema các bảng cũ): tạo IAuditSource trả về các entry chuẩn hóa, rồi một service gộp kết quả theo occurredAt giảm dần.

| Nguồn hiện có | action ví dụ |
| --- | --- |
| MealCalendarAudit | calendar.update, calendar.generate |
| AccountPasswordResetAudit | account.password_reset |
| Sự kiện MealRegistration | meal.exception.set/restore |
| Báo vắng (tạo/hủy/thay thế) | absence.create/cancel/replace |
| Enrollment | enrollment.transfer/stop/reenroll |
| Yêu cầu và duyệt điều chỉnh | portion.amend.request/resolve |
| PortionSettlement | portion.settle |

BE 1 và BE 2 phải cài IAuditSource cho bảng mới của mình (nutrient-versions, recipe versions, acknowledgements). Chốt interface này trong ngày đầu để 3 bạn không chờ nhau.

### 2. Business rules cần validate

1. Chỉ Admin. Teacher, Kitchen, Parent trả 403. Không có endpoint ghi/sửa/xóa audit.
2. Che dữ liệu nhạy cảm: không bao giờ trả mật khẩu, mật khẩu tạm, token, SecurityStamp. Export che SĐT (chỉ giữ 3 số cuối).
3. Giới hạn export tối đa 31 ngày/10.000 dòng, vượt thì 422 và yêu cầu thu hẹp khoảng.
4. Thứ tự ổn định: occurredAt desc, rồi source, rồi id. Bản ghi cũ thiếu actor hiển thị "chưa ghi người xử lý", không bịa.
5. Siết RBAC (deliverable kèm theo): mọi endpoint mới deny-by-default, gắn policy theo vai trò; viết ma trận test 4 vai trò × tất cả endpoint Pha 1 (200/401/403); thống nhất xử lý 409 và conflict DB thành một ProblemDetails (mục "Important" còn tồn đọng trong review); Teacher chỉ thao tác lớp được phân công theo Enrollment tại ngày ăn; Parent chỉ thấy trẻ còn liên kết.
6. Không thêm quyền thanh tra ở Pha này (để Pha 3).
### 3. Kịch bản demo End-to-End

1. Admin lọc theo entityId của một trẻ và thấy chuỗi: ghi danh, chuyển lớp, ngoại lệ, điều chỉnh sau chốt, mỗi dòng đủ người, lúc nào, lý do, trước/sau.
2. Lọc account.password_reset thấy người xử lý và lý do, không có mật khẩu.
3. Xem timeline phiên bản công thức của BE 1 và xác nhận của BE 2 ngay trong cùng màn.
4. Đăng nhập Teacher gọi /api/admin/audit-logs nhận 403.
5. Chạy ma trận quyền để chứng minh tự động.
## Kịch bản demo gộp cuối Pha 1 (khoảng 10 phút)

## BE 1: bếp tạo nguyên liệu và công thức, xem dinh dưỡng, sửa số liệu và so sánh theo thời điểm.

## BE 2: Admin chốt suất, bếp xác nhận, điều chỉnh +1 và bếp xác nhận lại.

## BE 3: Admin mở audit, truy một ngày ăn qua toàn bộ chuỗi thao tác, rồi chạy thử quyền sai để thấy 403.

## Cần xác nhận

"Hệ số Ak" không xuất hiện trong `continuity.md` hay `PROPOSAL.md`, nên chưa tự định nghĩa công thức. Tạm để một tham số cấu hình theo nhóm tuổi (ageGroup) trong endpoint nutrition ở BE 1, và công thức Ak chỉ nằm trong một hàm duy nhất, dễ thay. Cần xác nhận Ak là hệ số quy đổi khẩu phần theo nhóm tuổi, hệ số thải bỏ hay thứ khác, cùng nguồn công thức, để cập nhật đặc tả.

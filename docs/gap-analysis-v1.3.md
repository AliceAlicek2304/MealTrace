# GAP Analysis MealTrace — baseline v1.3

## 1. Kết luận và căn cứ

**Implementation hiện tại chưa đáp ứng MVP v1.3.** Luồng quản lý trẻ → báo không ăn → ngoại lệ → chốt suất → điều chỉnh đã có nền tảng tương đối đầy đủ theo quy tắc cũ. Các phần còn thiếu tập trung vào tự chốt hai mốc giờ, thực đơn/dinh dưỡng đầy đủ, dự tính nguyên liệu, món phục vụ, ảnh offline và báo cáo tái lập.

Đã đối chiếu `PROPOSAL.md`, `continuity.md`, toàn bộ tài liệu hiện có trong `docs/`, tài liệu Android, cùng các lớp xử lý nghiệp vụ, API, entities, repositories, cấu hình EF/migrations và giao diện BE/FE/Mobile liên quan.

Kết quả kiểm chứng:

- Backend: **167 test pass, 29 skip, 0 fail** sau khi chạy lại ngoài sandbox. Lần đầu test host bị lỗi quyền Windows Event Log.
- Các ca PostgreSQL/concurrency và baseline hiệu năng cần cấu hình riêng nên chưa được xác minh.
- Chưa chạy lại FE/Flutter vì môi trường hiện không có npm/Flutter khả dụng; chưa nghiệm thu trình duyệt hay thiết bị Android.
- Test hiện có chủ yếu kiểm chứng hành vi cũ; kết quả pass **không chứng minh đã đạt v1.3**.

Không sửa code, tài liệu, migration hoặc database dự án. `PROPOSAL.md` có thay đổi sẵn từ đầu phiên và được giữ nguyên.

## 2. Chức năng đã triển khai

“Đã triển khai” dưới đây áp dụng cho phần hành vi đã xác minh trong source, không đồng nghĩa toàn bộ FR tương ứng đã được nghiệm thu.

- **Tài khoản và phân quyền:** bốn role chuẩn; đăng nhập email/SĐT; quản lý tài khoản/phạm vi; đổi/reset mật khẩu; thu hồi JWT qua security stamp. Có giao diện web và Android.
- **Năm học, lớp, trẻ, ghi danh — phần nền FR-01:** mã trẻ ổn định, hồ sơ ngày sinh/giới tính, chuyển lớp/ngừng học/ghi danh lại, revision chống sửa hồ sơ lỗi thời. Khoảng ghi danh dùng ngày kết thúc exclusive.
- **Import XLSX — FR-01:** Admin chọn lớp, preview/lỗi từng dòng, kiểm tra trùng, xác nhận trong transaction và lưu lịch sử import. Không tự tạo Parent hoặc gửi WhatsApp.
- **Yêu cầu liên kết — FR-02/10/18:** Parent gửi yêu cầu; Admin/Teacher đủ phạm vi duyệt/từ chối; có xử lý danh sách, kiểm tra revision và thu hồi liên kết từ yêu cầu đã duyệt. Thu hồi làm mất hiệu lực phiên Parent cũ.
- **Báo không ăn — phần nền FR-19:** ngày/khoảng ngày/đến cuối năm học; sửa/hủy giữ dữ liệu cũ; kiểm tra guardian; không kết thúc ghi danh hay tự hoàn phí.
- **Quyết định suất và ngoại lệ — FR-07/08:** mặc định theo Enrollment, áp dụng báo Parent rồi ngoại lệ Teacher/Admin; khôi phục mặc định vẫn xét báo Parent; có nguồn, lý do và lịch sử.
- **Bản chốt và điều chỉnh — phần nền FR-04/09/14:** snapshot theo lớp, phiên bản, liên kết bản trước; điều chỉnh nhiều trẻ hoặc số lượng riêng bếp; chỉ áp dụng khi Admin duyệt; chặn nguồn lỗi thời, tổng âm và một số trường hợp trùng suất giữa lớp. Tổng hiện hành lấy bản mới nhất.
- **Nguyên liệu/công thức — phần nền FR-11/12:** BE có tạo nguyên liệu, phiên bản thành phần, công thức versioned và tính kcal/protein; dữ liệu thành phần thiếu trả lỗi, không âm thầm tính bằng 0.

Các snapshot suất, quyết định và điều chỉnh đã có bảo vệ append-only tại DbContext. Đây là nền tảng nên tiếp tục sử dụng. [Bằng chứng](D:/Capstone/MealTrace/be/src/MealTrace.Infrastructure/Persistence/MealTraceDbContext.cs)

## 3. GAP chức năng và business rules

### Đã có nhưng chưa đáp ứng v1.3

**G01 — Cutoff và tự chốt: P0 · FR-01/03/04/08/09/19/22**

- `SchoolTime` vẫn dùng **07:30 chung**, không phân biệt sáng 21:00 D−1 với trưa/xế 07:00 D.
- Chốt vẫn do Admin gọi endpoint; chưa có worker/job tự chốt và phục hồi các phiên đến hạn sau restart.
- Đã có logic loại báo nhận sau cutoff và đọc quyết định tại cutoff. Tuy nhiên, chưa kiểm chứng toàn luồng theo hai mốc mới.
- `MealType` là chuỗi tự nhập; chưa có định danh loại bữa ổn định để áp chính sách cutoff an toàn.
- Lịch còn khóa ở mức ngày trong một số nhánh. Không thể chỉ thay hằng số rồi đáp ứng trường hợp sáng đã khóa, trưa/xế còn mở.
- Quy tắc chuyển lớp/ngừng học và thông báo trên web/Android vẫn phụ thuộc 07:30.

[Bằng chứng giờ chốt](D:/Capstone/MealTrace/be/src/MealTrace.Domain/Time/SchoolTime.cs:7), [chốt thủ công](D:/Capstone/MealTrace/be/src/MealTrace.Application/Features/Workflow/WorkflowService.cs:493), [lịch vận hành](D:/Capstone/MealTrace/be/src/MealTrace.Application/Features/Calendar/MealCalendarService.cs)

**G02 — Xác nhận bếp nhận bản suất: P1 · FR-14**

Bếp đã xem được bản hiện hành, bản gốc và chênh lệch. Chưa có thao tác/bản ghi xác nhận **đã nhận đúng phiên bản**, người nhận, thời điểm nhận và trạng thái cần xác nhận lại sau điều chỉnh.

**G03 — Dinh dưỡng và công thức: P1 · FR-11/12/24**

- Mới có kcal/protein; thiếu lipid/carbohydrate, nguồn tham chiếu thành phần và phiên bản nguồn có cấu trúc.
- Chưa có nhóm tuổi, khoảng tham chiếu có nguồn và kết quả đối chiếu.
- Chưa phân biệt rõ đầu vào gross với edible.
- Cho phép đơn vị `ml`, nhưng công thức lưu trực tiếp thành `GramsPerPortion` mà chưa có chuyển đổi khối lượng tương ứng.
- Có rule rounding cố định trong code và trả về response; chưa lưu thành đầu vào/rule của bản tính đã duyệt.
- **Kết quả của cùng recipe version có thể đổi:** calculator dùng ingredient version được resolve tại `validAt/knownAt`, thay vì mặc định tính từ version nguyên liệu đã ghim. Cơ chế này có ích cho tính lại, nhưng chưa bảo đảm giữ nguyên bản công bố.
- Recipe version chưa có thời điểm hiệu lực riêng; chưa đủ cơ chế bảo toàn toàn bộ đầu vào của bản tính đã duyệt.

[Bằng chứng calculator](D:/Capstone/MealTrace/be/src/MealTrace.Application/Features/Kitchen/NutritionService.cs:31), [đơn vị và định lượng](D:/Capstone/MealTrace/be/src/MealTrace.Application/Features/Kitchen/RecipeService.cs:126)

**G04 — Audit và phạm vi liên kết chưa đồng nhất: P1 · FR-02/10/22/23**

- Luồng duyệt yêu cầu có lịch sử tốt; luồng liên kết trực tiếp chỉ thêm `ParentStudent`, chưa lưu thông tin xác minh/người thực hiện/lý do tương đương.
- Sửa tài khoản xóa rồi tạo lại các liên kết Parent và phân công Teacher; chưa có lịch sử thay đổi tương đương luồng duyệt/thu hồi.
- Liên kết trực tiếp kiểm tra `Student.ClassId`, trong khi pointer này có thể đã đổi do chuyển lớp tương lai. Cần dùng ghi danh hiệu lực khi xác định phạm vi Teacher.
- `TeacherAssignment` chưa có khoảng hiệu lực; cần làm rõ chính sách quyền lịch sử khi đổi phân công.

[Bằng chứng thay phạm vi](D:/Capstone/MealTrace/be/src/MealTrace.Application/Features/Accounts/AccountService.cs:222), [liên kết trực tiếp](D:/Capstone/MealTrace/be/src/MealTrace.Application/Features/Workflow/WorkflowService.cs:187)

**G05 — Android và đăng ký Parent: P1 · FR-17/18**

Android đã có các luồng vận hành hiện hữu, không còn chỉ là scaffold đăng nhập. Tuy nhiên, auth restore khi mất mạng không thay thế chức năng ảnh offline. OTP tự đăng ký cũng đang bị giới hạn ở SĐT tester cấu hình; chưa có bằng chứng nghiệm thu nhận OTP trên thiết bị thật.

### Chưa có workflow hoàn chỉnh

- **FR-05/13 — Thực đơn:** chưa có soạn ngày/tuần, gửi duyệt, duyệt/công bố và sửa thành phiên bản mới. `MenuDish` và `PublishedAt` mới là nền dữ liệu/API đọc; FE/Mobile chưa có màn quản lý nguyên liệu, công thức, dinh dưỡng hay workflow thực đơn.
- **FR-15 — Món phục vụ/đổi món:** chưa có nghiệp vụ ghi món phục vụ, số suất/định lượng, món thay thế, lý do và recipe version thực tế.
- **FR-16 — Dự tính nguyên liệu:** chưa có tính/xuất theo menu, recipe và settlement hiện hành; chưa lưu cơ sở tính hoặc đối chiếu sau điều chỉnh.
- **FR-17 và phần ảnh của FR-05 — Evidence:** chưa có capture, lưu file/metadata bền vững, queue, retry/idempotency và duyệt/công bố. Entity đã có `CapturedAt`/`SyncedAt`, nhưng chưa có workflow bảo đảm hai timestamp. Chưa có API phục vụ ảnh với quyền truy cập phù hợp.
- **FR-20 — Parent xem bản công bố:** chưa có API/UI xem menu, dinh dưỡng và ảnh đã công bố theo con liên kết. API hồ sơ ngày ăn hiện dành cho Admin/Kitchen.
- **FR-06/24 — Báo cáo tái lập:** mới có `ReportSnapshot` và endpoint đọc `SourceJson`; chưa có tạo báo cáo kỳ, duyệt/xuất, drill-down hoàn chỉnh, As-Approved/Recomputed và giải thích chênh lệch.
- **FR-23 — Audit xuyên luồng:** đã có ở suất và một số nghiệp vụ; còn thiếu nguồn/người thực hiện/phiên bản tại các workflow chưa triển khai và các nhánh liên kết nêu trên.
- **FR-21 Optional — Dị ứng/lưu ý:** chưa thấy implementation; không tính là thiếu Must-have.

[Bằng chứng API đọc hiện có](D:/Capstone/MealTrace/be/src/MealTrace.Api/Features/MealEndpoints.cs), [report scaffold](D:/Capstone/MealTrace/be/src/MealTrace.Domain/Entities/ReportSnapshot.cs), [phạm vi operation Android](D:/Capstone/MealTrace/mobile/lib/features/school/domain/school_models.dart)

### GAP kiểm thử và bàn giao

- Chưa có bộ kiểm chứng v1.3 cho hai cutoff, tự chốt chạy trễ/restart, ảnh offline và tái lập báo cáo.
- Có test kiến trúc, contract, quyền, lịch, import và điều chỉnh; phần dinh dưỡng chưa có bộ ca độc lập bao phủ yêu cầu mới.
- Harness hiệu năng hiện có không phải bằng chứng đạt mục tiêu danh sách lớp ≤5 giây và báo cáo tháng ≤10 giây trên dữ liệu tương đương ba năm học.
- `docs/` hiện chỉ có implementation plan; chưa thấy SRS/SDD/STD, bộ nghiệm thu và hướng dẫn sao lưu/phục hồi đầy đủ. README gốc đang trống.
- Android có mã E2E hồ sơ trẻ, nhưng chưa chứng minh đã chạy trên thiết bị thật; release signing còn cần cấu hình.

## 4. Mâu thuẫn cần xử lý

1. **07:30 thủ công so với 21:00/07:00 tự động:** `continuity.md`, roadmap và code thống nhất với nhau theo luật cũ, nhưng lệch proposal mới. Phải ghi nhận là legacy, không dùng để bác bỏ v1.3.
2. **Offline ảnh bị xếp “làm sau”:** trái yêu cầu Must-have FR-17. Tương tự, hướng Android “chỉ làm chức năng đã có trên web” không đủ để hoàn thành phần capture/offline riêng của Android.
3. **Roadmap import đã lỗi thời:** còn ghi chờ mẫu và định hướng nhập lớp/Parent; source đã có import trẻ vào lớp chọn sẵn, tách khỏi liên kết Parent. `mobile/docs/web-parity.md` cũng vừa liệt kê import đã có, vừa ghi import chưa nằm trong đợt.
4. **Thuật ngữ ngoài scope:** web mô tả Kitchen “ghi nhận kiểm thực”; Android đặt nhãn evidence là “Kiểm thực”. Proposal loại trừ kiểm thực ba bước và workflow sản xuất trong bếp; cần đổi thuật ngữ thành ghi nhận phục vụ/ảnh món ăn.
5. **Grant thanh tra ngoài baseline:** code/UI còn cấp quyền thanh tra có hạn. Đây không phải role thứ năm trong `RoleNames`, nhưng chưa có yêu cầu tương ứng trong v1.3; cần quyết định cách giữ tương thích và trình bày phạm vi.
6. **Trộn scope với tiến độ:** roadmap hướng dẫn cập nhật proposal theo chức năng chạy được, trái vai trò WHAT/WHY của proposal. Tiến độ phải ghi ở continuity; thay scope cần quyết định nghiệp vụ riêng.
7. **Các điểm proposal chủ động để mở:** chuyển lớp giữa hai cutoff, khoảng tham chiếu tuổi, rounding, hạn công bố, mẫu báo cáo và chính sách lưu ảnh vẫn cần trường/GVHD xác nhận. Không tự biến ví dụ 16:00 hoặc hệ số hao hụt thành rule bắt buộc.

[Bằng chứng roadmap](D:/Capstone/MealTrace/docs/implementation-plan.md), [phạm vi Android](D:/Capstone/MealTrace/mobile/docs/web-parity.md)

## 5. Lộ trình đề xuất để duyệt

**P0 — Chốt chính sách chuyển đổi và hoàn thiện tự chốt**

- Xác nhận quy tắc ghi danh/chuyển lớp trong khoảng sáng đã khóa, trưa/xế chưa khóa; cách ánh xạ các tên bữa hiện có và thời điểm áp dụng chính sách mới.
- Bổ sung định danh loại bữa/chính sách cutoff, giữ nguyên `CutoffAt` và bản chốt lịch sử.
- Dùng chung nghiệp vụ chốt cho worker tự động và đường phục hồi được phân quyền; có catch-up sau restart, chống trùng/race và xử lý phiên không có trẻ.
- Rà đồng bộ lịch, báo Parent, ngoại lệ, ghi danh, FE/Mobile và test. Việc sửa phiên còn mở phải không ảnh hưởng phiên đã khóa.
- Kiểm chứng đúng ví dụ **30 sáng → 29 trưa/xế → 28 trưa sau duyệt**, cùng biên đúng giờ, job trễ, retry và cạnh tranh PostgreSQL.

**P1 — Hoàn thiện nguồn dữ liệu và thực đơn**

- Bổ sung P/L/C, nguồn/phiên bản, đơn vị/chuyển đổi, gross/edible, nhóm tuổi và rounding đã xác nhận.
- Tách bản tính dùng đầu vào cố định khỏi bản tính lại; giữ nguyên kết quả đã công bố.
- Thêm workflow menu và UI web tương ứng; bổ sung API đọc recipe/version phục vụ thao tác thực tế.
- Test độc lập về khối lượng, tỷ lệ ăn được, đơn vị, làm tròn và thay đổi nguồn sau công bố.

**P1 — Hoàn thiện vận hành bếp và Android**

- Thêm xác nhận nhận theo settlement/version cụ thể.
- Tính/xuất nguyên liệu dự kiến từ bản suất hiện hành; truy về menu/recipe và giữ cơ sở các bản xuất đã lưu.
- Thêm món phục vụ/đổi món và lý do.
- Thêm ảnh offline Android: file/metadata bền vững, hàng đợi theo tài khoản, định danh chống trùng, hai timestamp và chỉ báo thành công sau server xác nhận.
- Thêm duyệt/công bố ảnh và Parent xem đúng phạm vi. Test airplane mode → chụp → kill/relaunch → retry hai lần → một bản ghi server; chặn ảnh chưa duyệt và ngoài scope.

**P1 — Báo cáo, audit và tái lập**

- Hoàn thiện audit các đường liên kết/phân quyền hiện có trước khi dùng chúng làm nguồn báo cáo.
- Thêm tạo/duyệt/xuất báo cáo suất và dinh dưỡng; lưu đủ nguồn, giá trị và rule.
- Tách As-Approved/Recomputed, drill-down và giải thích chênh lệch.
- Test sửa ingredient/recipe/suất/món sau duyệt: bản duyệt giữ nguyên, bản tính lại thể hiện đúng thay đổi.

**P2 — Nghiệm thu và bàn giao MVP**

- Chạy PostgreSQL concurrency và kiểm thử migration trên DB thử biệt lập; kiểm tra dữ liệu legacy thiếu nguồn được hiển thị trung thực.
- Nghiệm thu web responsive, Android thật, OTP và các luồng theo role.
- Đo hiệu năng theo dataset/môi trường công bố; hoàn thiện tài liệu triển khai, backup/restore và demo end-to-end.
- Sau từng phần được duyệt và kiểm chứng, cập nhật continuity/roadmap theo thực tế. P2 là thứ tự thực hiện, không phải hạ các yêu cầu bàn giao thành tùy chọn.

Mặc định giữ bốn role, bảo toàn dữ liệu/API hiện hữu và không mở rộng sang kho, mua hàng, tài chính, kiểm thực, iOS hay full offline. FR-21 chỉ đưa vào kế hoạch khi được giao riêng. Các quy tắc chưa được xác nhận phải được chốt ở bước đặc tả tương ứng trước khi triển khai; hiện chưa thực hiện thay đổi nào.

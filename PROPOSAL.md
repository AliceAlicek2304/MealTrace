# MealTrace — Đề xuất đề tài và đặc tả nghiệp vụ nền (Proposal)

> **Căn cứ phạm vi:** `Phieu_FA26SE270_V1.3(3).docx`, phiên bản 1.3, ngày 15/10/2026; nội dung được nhóm điều chỉnh sau góp ý GVHD.  
> **Ngày đồng bộ tài liệu:** 23/10/2026.  
> **Mục đích:** làm tài liệu nghiệp vụ nền để người phát triển và coding agent hiểu **đích cần triển khai**, không dùng làm bằng chứng tính năng đã hoàn thành.  
> **Trạng thái:** baseline nhóm đề xuất, còn phụ thuộc phản hồi/phê duyệt chính thức của GVHD và quy tắc thử nghiệm được trường xác nhận.  
> **Theo dõi tiến độ:** đọc `continuity.md` và kiểm tra **codebase thực tế**. `continuity.md` ngày 09/10/2026 có các mô tả triển khai theo quy tắc **cũ**; không dùng các mô tả đó để phủ định phạm vi mới.

## 0. Quy tắc sử dụng tài liệu cho nhóm và AI coding agent

1. **Phiếu đề tài v1.3 xác định phạm vi cam kết**; tài liệu này diễn giải thành nghiệp vụ và tiêu chí chấp nhận. Nếu có xung đột với hướng dẫn cũ, **không tự chọn cách triển khai cũ**. Nêu xung đột để nhóm quyết định trước khi sửa mã.
2. **Yêu cầu đích ≠ trạng thái đã code.** Đặc biệt, tự động chốt 21:00/07:00, đồng bộ ảnh offline, đầy đủ P/L/C, báo cáo tái lập và dự tính nguyên liệu là yêu cầu phạm vi; không được ghi “đã hoàn thành” nếu chưa xác minh source, test và demo.
3. `continuity.md` dùng để hiểu tình trạng tại lần cập nhật gần nhất; source code, schema/migrations và kết quả kiểm thử thực tế mới là căn cứ xác định điều **đang chạy**. Nếu khác nhau, lập danh sách GAP; không ghi đè trạng thái lịch sử bằng phỏng đoán.
4. Khi code, đọc các tài liệu liên quan trong `docs/` (đặc biệt SRS, kế hoạch triển khai, hợp đồng API nếu có), cùng codebase và migrations hiện tại trước khi lên plan. **Ưu tiên plan → rà tác động → triển khai có test**; không tự mở rộng scope.
5. Giữ lịch sử, dữ liệu đã khóa và contract FE/BE. Thay đổi điều kiện khóa suất có thể đụng lịch ăn, ghi danh, API, migration, job tự động, FE/Android và test; không được chỉ sửa một hằng số `07:30`.
6. Khi nhóm bổ sung/chỉnh quy tắc được GVHD xác nhận, cập nhật nhất quán SRS, tài liệu nghiệp vụ, API contracts, test cases và `continuity.md` theo trạng thái thực tế.

## 1. Tên, bối cảnh và định hướng

**Tên tiếng Anh:** MealTrace: Design and Implement a Traceable Meal Management and Nutrition Reporting System for Public Preschools.  
**Tên tiếng Việt:** MealTrace: Thiết kế và xây dựng hệ thống quản lý bữa ăn bán trú và báo cáo dinh dưỡng có truy vết cho trường mầm non công lập.  
**Tên viết tắt:** MealTrace.

MealTrace hỗ trợ **một trường mầm non** quản lý danh sách trẻ/lớp, báo không ăn, lịch bữa, số suất chốt gửi bếp, thực đơn, công thức và dinh dưỡng ước tính, món phục vụ/đổi món, ảnh món ăn và báo cáo. Phụ huynh chỉ xem thông tin đã công bố trong phạm vi trẻ được liên kết.

Vấn đề cần giải quyết: số liệu báo không ăn, ngoại lệ giáo viên và số suất bếp có thể chênh nhau; thực đơn dự kiến có thể khác món phục vụ; dữ liệu nguyên liệu và cách tính dinh dưỡng có thể thiếu nguồn/phiên bản; sửa dữ liệu về sau có thể làm mất khả năng tái hiện báo cáo cũ.

**Giá trị kỹ thuật cốt lõi là truy vết nguồn của số liệu (data lineage)**: từ một số suất hoặc chỉ số kcal/P-L-C có thể trở lại bản chốt, món/công thức, khối lượng, hệ số, nguồn và phiên bản dữ liệu từng được sử dụng. Sau điều chỉnh, bản gốc vẫn tồn tại để so sánh với kết quả tính lại.

**Data lineage không có nghĩa là truy xuất nguồn gốc thực phẩm.** Hệ thống **không** theo dõi trang trại, lô hàng, giấy phép nhà cung cấp hay hành trình thực phẩm trong chuỗi cung ứng.

### 1.1. Nguyên tắc tránh diễn giải sai

- **Dự kiến có suất** không đồng nghĩa **đã điểm danh có mặt**.
- **Món/số suất ghi nhận đã phục vụ** không đồng nghĩa hệ thống đo từng trẻ thực sự ăn bao nhiêu gram.
- **Dinh dưỡng ước tính** được tính từ dữ liệu công thức và nguyên liệu; không phải xét nghiệm hoặc đánh giá dinh dưỡng lâm sàng từng trẻ.
- **Danh sách nguyên liệu dự tính cần mua** là bảng tính toán phục vụ kế hoạch, **không phải** đơn mua hàng, tồn kho, nhập/xuất kho hoặc chứng từ nhà cung cấp.
- Ảnh món ăn dùng để ghi nhận việc phục vụ và công bố cho phụ huynh, **không phải** quy trình kiểm thực an toàn thực phẩm hoặc quản lý sản xuất bên trong bếp.

## 2. Phạm vi bắt buộc của MVP

1. **Dữ liệu nền và phân quyền:** tài khoản, bốn role, năm học/lớp/trẻ/ghi danh, liên kết phụ huynh–trẻ, nhập học sinh từ Excel, phạm vi truy cập theo lớp/trẻ.
2. **Lịch ăn và số suất:** xác định suất dự kiến mặc định, báo không ăn ngày/khoảng ngày, ngoại lệ giáo viên, tự khóa/chốt riêng từng bữa, gửi/nhận bản chốt, yêu cầu và duyệt điều chỉnh sau chốt có phiên bản.
3. **Thực đơn và dinh dưỡng:** dữ liệu thành phần thực phẩm có tham chiếu, đơn vị và tỷ lệ phần ăn được, công thức có phiên bản, tính kcal và protein/lipid/carbohydrate, đối chiếu khoảng tham chiếu theo nhóm tuổi mầm non, lập/duyệt/công bố thực đơn.
4. **Dự tính nguyên liệu:** tính và xuất danh sách nguyên liệu/khối lượng dự kiến theo thực đơn, công thức và **số suất đã chốt đang áp dụng**; không quản lý giao dịch mua hàng.
5. **Món phục vụ và ảnh:** ghi món đã phục vụ, số suất/định lượng được ghi nhận, đổi món và lý do; chụp/lưu ảnh món ăn trên Android khi offline và đồng bộ sau; Admin duyệt, Parent xem dữ liệu được công bố.
6. **Báo cáo có truy vết:** xuất báo cáo số suất và dinh dưỡng, truy ngược nguồn, lưu bản duyệt/bản tính lại riêng, giải thích chênh lệch sau điều chỉnh.
7. **Kiểm thử và bàn giao:** backend, web, Android, tài liệu, dữ liệu demo và bài test cho phân quyền, cutoff, dinh dưỡng, offline ảnh và tái lập báo cáo.

### 2.1. Chức năng tùy chọn / mở rộng

- **FR-21 (Optional):** phụ huynh khai báo dị ứng hoặc lưu ý ăn uống để trường xem xét; không tự quyết định thực đơn an toàn hay sinh thực đơn cá nhân.
- Sổ cái tiền ăn, hoàn phí, đối soát/thanh toán; hệ thống không được ngầm áp dụng quy tắc hoàn phí từ báo không ăn hoặc điều chỉnh suất.
- Bảng điều khiển nâng cao, mẫu báo cáo tùy biến, quản trị trường dạng ERP, thông báo đa kênh, micronutrients mở rộng hoặc chế độ ăn điều trị.
- Tích hợp liên thông hệ thống cơ quan quản lý; các thông báo WhatsApp/OTP đã có trong code nếu được xác minh thì duy trì như chức năng hiện hữu, **không tự xem việc mở rộng kênh nhắn tin là yêu cầu MVP mới**.

### 2.2. Ngoài phạm vi (Out of Scope)

- **Nguồn gốc và mua hàng:** farm-to-fork, quản lý nhà cung cấp, chứng chỉ/giấy phép nhà cung cấp, đặt mua/đối soát mua hàng.
- **Quy trình chế biến, kho và kiểm thực:** workflow nấu ăn bên trong bếp, dây chuyền chế biến, xuất/nhập kho, định giá tồn kho, lô thực phẩm, kiểm thực ba bước, lưu mẫu thức ăn, thanh tra an toàn thực phẩm vật lý và tự chứng nhận tuân thủ pháp luật.
- **Tài chính:** thanh toán điện tử, sổ cái thu phí, tự hoàn phí, báo cáo công nợ/doanh thu và đối soát kế toán (chỉ có thể xem xét như mở rộng).
- **AI/IoT & dinh dưỡng nâng cao:** AI tạo thực đơn, menu optimization tự động, IoT cân/cảm biến, vi chất chi tiết, khẩu phần điều trị riêng từng trẻ.
- **Nền tảng/triển khai bổ sung:** native iOS, chế độ offline cho toàn bộ nghiệp vụ Admin/Teacher/Parent, báo cáo chính phủ tự gửi, full ERP, dashboard nâng cao. **Offline chụp ảnh món ăn trên Android vẫn bắt buộc.**
- Không yêu cầu Docker hay nền tảng CI/CD cụ thể làm điều kiện hoàn thành MVP; cần hướng dẫn cấu hình/triển khai có thể tái lập.

## 3. Vai trò, quyền và ranh giới dữ liệu

| Role kỹ thuật | Trách nhiệm nghiệp vụ trong MVP | Giới hạn |
| --- | --- | --- |
| `ADMIN` | Niên khóa, lớp, tài khoản/phân quyền; lịch bữa; duyệt điều chỉnh suất; duyệt/công bố thực đơn, ảnh; duyệt/xuất báo cáo. | Duyệt có lưu người, thời điểm, phiên bản và lý do khi thay đổi. |
| `TEACHER` | Xem lớp được phân công; ghi ngoại lệ trước giờ khóa; yêu cầu điều chỉnh sau giờ khóa; hỗ trợ xác minh liên kết phụ huynh–trẻ. | Chỉ dữ liệu lớp được giao theo ghi danh/hiệu lực hợp lệ. |
| `KITCHEN_STAFF` | Nguyên liệu, dữ liệu dinh dưỡng, công thức, lập thực đơn; xem và xác nhận đã nhận bản suất; ghi món phục vụ, đổi món; ước tính nguyên liệu; chụp ảnh và đồng bộ. | Không tự duyệt thay Admin, không quản lý mua hàng/kho/kiểm thực. |
| `PARENT` | Đăng ký/đăng nhập, yêu cầu liên kết trẻ, báo không ăn, xem lịch sử, thực đơn/dinh dưỡng/ảnh đã công bố. | Chỉ trẻ có quan hệ giám hộ **đã xác minh**; không tự cấp liên kết hay sửa bản chốt. |

Hệ thống chỉ có bốn role trên. **Không tạo `NUTRITIONIST` hoặc `ACCOUNTANT` thành role độc lập** để phục vụ MVP. `System-Wide Functions` là nhóm yêu cầu chung, **không phải actor hay role thứ năm**. Một tài khoản có thể có nhiều vai trò nếu chính sách ứng dụng cho phép; API vẫn kiểm tra quyền và phạm vi dữ liệu tương ứng.

## 4. Quy tắc nghiệp vụ chi tiết

### 4.1. Năm học, lớp, trẻ, ghi danh và Parent

- Admin thiết lập năm học, lớp, phân công giáo viên; trẻ có mã định danh ổn định và có thể chuyển lớp/ngừng học/ghi danh lại.
- Ghi danh (`Enrollment`) lưu lớp và khoảng ngày hiệu lực; **ngày bắt đầu được tính, ngày kết thúc là ngày đầu không còn thuộc lớp**. Không có ghi danh hiệu lực chồng lấn khiến một trẻ xuất hiện ở hai lớp cho cùng phiên ăn.
- Quyền giáo viên và xác định lớp phục vụ **lấy theo ghi danh tại ngày/phiên cần xét**, không lấy lớp hiện tại để ghi đè thông tin lịch sử.
- **Mọi thay đổi ghi danh tác động tới suất phải xét các cutoff tương ứng của từng bữa**. Không được sửa ngược lại các bản suất đã khóa. Ràng buộc chi tiết về thời điểm chuyển lớp/ngừng học dự kiến cần được kiểm tra với trường khi chuyển từ mốc cũ 07:30 sang hai mốc mới.
- Admin nhập học sinh bằng XLSX vào **lớp đã chọn**, có preview/validation/lỗi theo dòng và tránh nhập trùng. Đề xuất dữ liệu cốt lõi theo luồng đã ghi nhận: họ tên, ngày sinh, giới tính (nếu có), tự cấp mã và ghi danh. **Import không mặc định tự tạo hoặc ghép tài khoản Parent từ SĐT**; quản lý liên kết là luồng riêng.
- Cho phép một phụ huynh liên kết nhiều trẻ và một trẻ có nhiều người giám hộ; liên kết mới phải được xác minh bởi Admin hoặc Teacher đủ quyền; hủy liên kết phải thu hồi quyền truy cập, giữ lịch sử.
- Cơ chế tự đăng ký/OTP WhatsApp và gửi tin đang được mô tả tại `continuity.md` như **trạng thái code**, không phải lý do mở thêm tính năng thông báo ngoài phạm vi phiếu. Không đưa bí mật API hoặc mật khẩu lên repository.

### 4.2. Lịch ăn và trạng thái dự kiến

- Trên ngày học có phiên ăn hợp lệ, trẻ có ghi danh hiệu lực mặc định **dự kiến có suất** ở phiên đó, trừ khi có báo không ăn hoặc ngoại lệ hợp lệ.
- Admin quản lý lịch theo niên khóa/thứ/bữa, ngày nghỉ, học bù; không sinh bản chốt cho phiên không tổ chức ăn. Tạo phiên ăn không đồng nghĩa đã chốt; mỗi phiên có cutoff và bản chốt riêng.
- Phụ huynh có thể báo không ăn **một ngày, nhiều ngày hoặc tới hết năm học**; ngày đầu và cuối thuộc phạm vi báo. Báo không ăn **không đồng nghĩa kết thúc ghi danh hoặc xác nhận trẻ thực tế vắng học**.
- Giáo viên/Admin ghi ngoại lệ trước cutoff cho trẻ thuộc lớp được phép: có suất, không suất hoặc trở về mặc định, kèm nguồn và lý do. “Trở về mặc định” vẫn phải xét báo không ăn của phụ huynh; không xóa nguồn báo cũ.
- Mọi tạo/sửa/hủy báo, ngoại lệ và điều chỉnh giữ audit/version, thời điểm hệ thống ghi nhận và chủ thể thực hiện; không sửa lịch sử đã dùng trong bản khóa.

### 4.3. Hai giờ khóa/chốt suất mới — bắt buộc trong v1.3

Tất cả mốc dưới đây dùng **giờ Việt Nam (Asia/Ho_Chi_Minh, UTC+7)**, theo **ngày phục vụ D**:

| Phiên | Thời điểm khóa tự động | Báo không ăn/ngoại lệ thông thường hợp lệ |
| --- | --- | --- |
| Bữa sáng (nếu trường phục vụ) | **21:00 ngày D−1** | Ghi nhận **trước** 21:00 ngày D−1 |
| Bữa trưa | **07:00 ngày D** | Ghi nhận **trước** 07:00 ngày D |
| Bữa xế chiều | **07:00 ngày D** | Ghi nhận **trước** 07:00 ngày D |

**Đây là quy tắc thử nghiệm của nhóm theo phiếu v1.3; trường thí điểm/GVHD sẽ xác nhận khi nghiệm thu.** Không coi đây là giờ chốt theo luật chung cho mọi trường.

- **Tự khóa/chốt theo từng phiên, không chờ Admin bấm đúng giờ.** Dữ liệu đầu vào dùng để tính bản chốt phải là dữ liệu hợp lệ **đã được server ghi nhận trước cutoff**, theo thứ tự thời gian tin cậy của hệ thống.
- Thời điểm **bằng hoặc sau cutoff** được xem là muộn đối với phiên tương ứng. Nếu tác vụ chốt chạy chậm, vẫn tính trạng thái tại cutoff, **không lấy dữ liệu nhận sau giờ chốt** rồi gộp vào bản gốc.
- Một báo không ăn cho **cả ngày** có thể cập nhật các phiên **chưa khóa**, trong khi phiên đã khóa giữ nguyên. Không bắt buộc Parent gửi một yêu cầu riêng cho mỗi bữa chỉ để đạt hiệu lực theo cutoff.
- Việc sửa/hủy báo không ăn muộn vẫn giữ lịch sử nhưng **không tự đảo quyết định trong bản chốt**; Teacher/Admin tạo yêu cầu điều chỉnh có lý do và Admin xem xét phê duyệt.
- Bếp nhận **bản chốt đang áp dụng** và có cơ chế xác nhận đã nhận đúng phiên bản; khi phiên bản thay đổi, cần thấy chênh lệch so với bản trước. Không cộng các bản chốt lịch sử để tạo tổng hiện hành.
- Job, endpoint hoặc phương thức bảo đảm chốt đúng giờ là quyết định thiết kế kỹ thuật; cần có idempotency/concurrency guard để một phiên không bị chốt hai lần khi retry/race.
- **Không được áp dụng lại cutoff mới cho bản chốt lịch sử tạo theo quy tắc cũ**; dữ liệu cũ phải được hiển thị theo bản ghi/phiên bản thực tế. Lúc chuyển đổi từ code cũ cần bảo toàn lịch sử và có test hồi quy.

**Ví dụ kiểm chứng (theo phiếu v1.3):** Thứ Hai có 30 suất dự kiến mỗi bữa. 21:00 Chủ nhật, bữa sáng khóa 30 suất. 22:00 Chủ nhật, Parent báo con không ăn thứ Hai: sáng vẫn 30; trưa/xế dự kiến còn 29. 07:00 thứ Hai, trưa/xế khóa 29. Sau 07:00 phát sinh thêm một ca không ăn trưa: Teacher lập phiếu giảm một suất; khi Admin duyệt, bản trưa `v2 = 28`, bản `v1 = 29` vẫn được lưu và bếp nhận bản cập nhật.

### 4.4. Điều chỉnh sau giờ khóa

- Teacher trong phạm vi lớp hoặc Admin được lập phiếu điều chỉnh, gắn **phiên nguồn đang áp dụng**, lý do, đối tượng bị tác động (trẻ cụ thể hoặc số lượng bếp không gắn trẻ theo trường hợp được phép).
- Admin duyệt hoặc từ chối; từ chối không đổi số suất. **Chỉ khi duyệt** mới tạo bản suất mới có liên kết tới bản trước; ghi người duyệt, lúc duyệt, lý do và chênh lệch.
- Nếu bản nguồn không còn là phiên hiện hành thì phải từ chối xử lý dựa trên phiên cũ, tránh hai yêu cầu đồng thời cùng thay đổi một nguồn.
- Phiếu điều chỉnh **theo trẻ** có thể thay đổi trạng thái suất của trẻ đó; phiếu số lượng **không gắn trẻ** chỉ thay đổi tổng gửi bếp, không dựng dữ liệu từng trẻ giả. Không cho tổng âm/giảm vượt suất đang áp dụng.
- Báo không ăn sau giờ chốt **không tự kích hoạt hoàn phí**, không tự sinh bản chốt mới và không tự làm thay đổi tình trạng điểm danh.

### 4.5. Nguyên liệu, công thức, dinh dưỡng và nhóm tuổi

- Dữ liệu nguyên liệu gồm tên/định danh, đơn vị, **thành phần trên 100 g phần ăn được** (khi nguồn trình bày như vậy), năng lượng, protein, lipid, carbohydrate và tỷ lệ phần ăn được nếu có; lưu nguồn tham chiếu/phiên bản hiệu lực.
- **Nguồn dữ liệu được nhóm chọn để tham khảo thử nghiệm:**
  1. [Foods According to Four Nutritional Groups for Preschool Children — Bo De Preschool](https://mnbode.hanoi.edu.vn/anh-dep-cua-be/thuc-pham-theo-4-nhom-dinh-duong-cho-tre-mam-non/ctfull/17009/1057362) — tham khảo phân loại bốn nhóm thực phẩm, **không phải bảng số liệu kcal định lượng**.
  2. [Decision 2195/QD-BGDDT (2022)](https://thuvienphapluat.vn/van-ban/the-thao-y-te/Quyet-dinh-2195-QD-BGDDT-2022-to-chuc-bua-an-hoc-duong-trong-cac-co-so-giao-duc-mam-non-525649.aspx) — tham khảo tổ chức bữa ăn học đường mầm non/tiểu học.
  3. [Decision 3958/QD-BYT (2025), Appendix 7](https://thuvienphapluat.vn/van-ban/The-thao-Y-te/Quyet-dinh-3958-QD-BYT-2025-Huong-dan-dinh-duong-doi-voi-bua-an-hoc-duong-690450.aspx) — tham khảo bảng thành phần dinh dưỡng và tỷ lệ phần ăn được của thực phẩm thông dụng.
- **Khoảng tham chiếu dinh dưỡng theo nhóm tuổi mầm non** căn cứ Chương trình giáo dục mầm non áp dụng, được trường/GVHD xác nhận; **không áp trực tiếp các ngưỡng học sinh phổ thông** của Quyết định 3958 lên trẻ mầm non.
- Công thức món có nguyên liệu, định lượng, đơn vị, phiên bản và thời điểm hiệu lực. Nếu nhập trọng lượng **nguyên liệu thô/gross** thì tính phần ăn được bằng tỷ lệ đã xác định; nếu nhập **khối lượng ăn được** thì không chuyển đổi hai lần. Cách nhập trọng lượng phải được ghi rõ để kiểm thử.
- Công thức tính kcal và P/L/C là **deterministic**, từ khối lượng hợp lệ × giá trị thành phần quy đổi về đơn vị thống nhất, tổng theo món/khẩu phần. Lưu độ chính xác/quy tắc làm tròn, không nhập tùy ý một kết quả báo cáo không có nguồn.
- **FR-12 (nguyên văn phiếu v1.3):** *Manage versioned recipes and calculate estimated kcal and macronutrients using recorded ingredient quantities, conversion factors, and nutritional sources; compare the results against documented age-group reference ranges.*
- Công thức đã được dùng trong thực đơn/báo cáo công bố không bị thay đổi ngầm khi công thức hay bảng thành phần nguyên liệu được cập nhật sau đó. Bản tính lại là kết quả riêng, có nguồn/căn cứ chênh lệch.
- Không tự yêu cầu quản lý hệ số hao hụt chế biến/yield chi tiết nếu trường chưa xác nhận mô hình và dữ liệu. Không triển khai thuật toán AI sinh hoặc tối ưu thực đơn.

### 4.6. Lập, duyệt và công bố thực đơn

- Kitchen tạo thực đơn ngày/tuần từ món và công thức có phiên bản, theo bữa và nhóm tuổi nếu được áp dụng. Hệ thống trình bày dinh dưỡng ước tính và kết quả so với khoảng tham chiếu có nguồn.
- Admin duyệt/công bố phiên bản thực đơn. Giữ bản công bố, người/thời điểm duyệt; sửa sau công bố phải có phiên bản/lý do, không thay nội dung bản cũ đã tham chiếu.
- Hạn công bố thực đơn (ví dụ trước 16:00 ngày học trước ngày ăn) **chưa được chốt trong phiếu v1.3**; là quy tắc cấu hình cần trường/GVHD xác nhận, không hardcode như cam kết.

### 4.7. Dự tính nguyên liệu cần mua (không phải Purchasing)

- Sau khi có thực đơn/công thức và bản suất **đang áp dụng**, Kitchen xem danh sách nguyên liệu với khối lượng dự kiến theo từng bữa/ngày/phạm vi được phép.
- Kết quả có thể tổng hợp theo nguyên liệu, đơn vị và tổng số lượng; nếu yêu cầu làm tròn đơn vị mua hàng, phải ghi **quy tắc làm tròn**. Số liệu phải truy lại được đến món, công thức, định lượng và phiên bản số suất.
- Khi điều chỉnh suất sau cutoff hoặc đổi thực đơn, hệ thống có thể tính **bản dự tính mới** mà không sửa bản báo cáo/bản xuất trước đã lưu; cần thể hiện cơ sở tính mới và chênh lệch nếu được dùng để đối chiếu.
- **Không** tạo purchase order, quản lý giá mua/nhà cung cấp, kho, vận chuyển, nghiệm thu hàng, hay tracking số lô thực phẩm.

### 4.8. Món phục vụ, đổi món và Android offline

- Kitchen ghi món thực tế đã phục vụ theo ngày, bữa và phiên liên quan; khi thay món so với thực đơn công bố, lưu món thay thế và lý do, liên kết phiên bản công thức dùng để tính dinh dưỡng.
- Khi cần, ghi số suất hoặc định lượng món đã phục vụ ở mức bữa/lớp theo thiết kế đã chốt. **Không** mô hình hóa quá trình nấu/chế biến theo công đoạn, thất thoát trong dây chuyền hay lượng mỗi trẻ đã ăn.
- Chụp **ảnh món/khay ăn**, không mặc định chụp trẻ. Ảnh gắn vào bữa/món; chỉ Parent thuộc phạm vi được phép xem **ảnh đã được Admin duyệt/công bố**.
- **Offline Android là Must-have cho chụp ảnh món ăn và metadata gắn ảnh**, không phải offline đầy đủ mọi màn hình: lưu bản nháp/queue cục bộ khi mất mạng, mở app lại vẫn còn dữ liệu chờ; khi có mạng thì đồng bộ retry an toàn, idempotent, không tạo ảnh trùng, giữ thời điểm chụp và thời điểm server nhận.
- Cần xử lý khác biệt thời gian thiết bị/server, quá trình upload thất bại và việc thay đổi quyền công bố; **client báo đã đồng bộ chỉ sau khi server xác nhận**. File ảnh không public tràn lan và truy cập phải kiểm tra quyền từ server.
- **Không** làm kiểm thực ba bước, phiếu lưu mẫu, truy xuất nguồn gốc thực phẩm hay quy trình sản xuất bên trong bếp. Tài liệu tham khảo về bữa ăn không tự mở rộng phạm vi thành các module này.

### 4.9. Báo cáo, phiên bản và truy vết tính toán

- Admin tạo, xem, duyệt và xuất báo cáo theo kỳ về **số suất** và **dinh dưỡng ước tính** từ bản chốt đang xét, điều chỉnh đã duyệt, món đã phục vụ và phiên bản công thức/nguồn thành phần dinh dưỡng.
- Từ một chỉ số báo cáo, truy đến bản chốt/điều chỉnh, bữa/món, công thức, định lượng, nguồn dinh dưỡng, đơn vị và phiên bản/rule đã áp dụng (drill-down / data lineage).
- Bản báo cáo **đã được duyệt** lưu đủ giá trị và nguồn/rule để tái lập **đúng số liệu của bản duyệt**, dù sau đó có thay đổi nguyên liệu, công thức, số suất hoặc bản ghi món.
- Sau sửa dữ liệu, bản tính lại **không ghi đè bản duyệt**. Có thể so hai bản và lý giải nguồn chênh lệch; không tự tuyên bố bản duyệt ban đầu sai hoặc báo cáo cơ quan nhà nước được tự động chứng thực.
- Có thể xuất tệp báo cáo phục vụ trường rà soát/gửi bằng quy trình hiện hành. Không tích hợp trực tiếp cổng cơ quan quản lý; không có báo cáo thu phí/doanh thu trong baseline.
- Dùng các khái niệm `valid time` và `recorded/transaction time` **tại dữ liệu report-critical cần thiết**. Không áp đặt full event sourcing hoặc WORM cho toàn hệ thống nếu không cần để đạt khả năng tái lập đã cam kết.

## 5. FR baseline — mapping theo phiếu v1.3

Bảng này có tính chất **yêu cầu đích** để lập kế hoạch/test, không thể hiện trạng thái code đã xong.

| ID | Actor/nhóm | Yêu cầu tóm tắt | Ưu tiên |
| --- | --- | --- | --- |
| FR-01 | Admin | Niên khóa, lớp/trẻ, giáo viên, ghi danh, import Excel | Must |
| FR-02 | Admin | Tài khoản/quyền và duyệt/thu hồi liên kết phụ huynh–trẻ | Must |
| FR-03 | Admin | Lịch ăn/ngày nghỉ; bữa sáng ngày D khóa 21:00 ngày D−1, trưa/xế khóa 07:00 ngày D | Must |
| FR-04 | Admin | Xem bản suất tự chốt; duyệt/từ chối điều chỉnh, giữ phiên bản | Must |
| FR-05 | Admin | Duyệt/công bố thực đơn, ảnh món ăn; giữ lịch sử duyệt/sửa | Must |
| FR-06 | Admin | Duyệt/xuất báo cáo; truy nguồn và đối chiếu tái tính | Must |
| FR-07 | Teacher | Xem trẻ/suất dự kiến và nguồn quyết định trong lớp được giao | Must |
| FR-08 | Teacher | Ngoại lệ trước cutoff theo bữa, giữ lý do và audit | Must |
| FR-09 | Teacher | Đề nghị điều chỉnh sau cutoff và xem kết quả | Must |
| FR-10 | Teacher | Xác minh liên kết Parent–Student trong lớp được giao | Must |
| FR-11 | Kitchen | Danh mục nguyên liệu, thành phần, tỷ lệ ăn được, nguồn/phiên bản | Must |
| FR-12 | Kitchen | Công thức versioned, tính kcal/P-L-C, đối chiếu nhóm tuổi | Must |
| FR-13 | Kitchen | Soạn và gửi duyệt thực đơn ngày/tuần, giữ bản công bố | Must |
| FR-14 | Kitchen | Xem và xác nhận nhận bản suất hiện hành/điều chỉnh | Must |
| FR-15 | Kitchen | Ghi món/số suất phục vụ, thay món/lý do/công thức nguồn | Must |
| FR-16 | Kitchen | Tính danh sách nguyên liệu dự kiến theo menu, recipe và suất | Must |
| FR-17 | Kitchen | Ảnh món ăn offline Android, sync chống trùng, hai timestamp | Must |
| FR-18 | Parent | Đăng ký/đăng nhập, yêu cầu liên kết trẻ cần xác minh | Must |
| FR-19 | Parent | Báo không ăn ngày/khoảng ngày; chỉ ảnh hưởng phiên chưa khóa | Must |
| FR-20 | Parent | Xem thực đơn/dinh dưỡng/ảnh được công bố cho con liên kết | Must |
| FR-21 | Parent | Ghi lưu ý dị ứng/ăn uống để trường xem xét | **Optional** |
| FR-22 | System-wide | Auth và phân quyền theo role + record scope | Must |
| FR-23 | System-wide | Audit và tham chiếu nguồn của dữ liệu quan trọng | Must |
| FR-24 | System-wide | Giữ đầu vào/rule/kết quả duyệt để tái lập và so sánh | Must |

**Lưu ý:** Dòng FR-03 nói *bữa sáng ngày D* khóa lúc 21:00 **ngày D−1**; không được giải mã thành bữa sáng “hôm sau” ở mọi truy vấn mà bỏ thông tin ngày phục vụ.

## 6. Các NFR và cách nghiệm thu dự kiến

| NFR | Yêu cầu | Minh chứng tối thiểu |
| --- | --- | --- |
| NFR-01 — Accuracy & Reproducibility | Dùng số liệu/đơn vị/hệ số/nguồn/rule có ghi nhận; báo cáo duyệt tái lập được | Bộ ca dinh dưỡng tự tính độc lập; chụp snapshot báo cáo, sửa nguồn rồi tái tạo bản cũ/bản mới |
| NFR-02 — Data Integrity | Hai cutoff server-side; bản trước khóa không bị sửa bởi báo muộn; điều chỉnh chỉ tạo version mới khi duyệt | Test `20:59:59/21:00:00` và `06:59:59/07:00:00`, job chạy chậm, retry, cạnh tranh phiên bản |
| NFR-03 — Security & Privacy | Quyền theo role/lớp/con liên kết; không public dữ liệu trẻ/ảnh | API negative tests cho Parent khác trẻ, Teacher khác lớp; truy cập ảnh chưa duyệt/không có quyền bị chặn |
| NFR-04 — Offline Reliability | Ảnh + metadata còn sau restart/mất mạng; sync retry idempotent; giữ capture & server receipt time | Bật airplane mode, chụp ảnh, kill/relaunch app, đồng bộ hai lần, kiểm tra một bản ghi trên server |
| NFR-05 — Usability | Giao diện tiếng Việt, responsive web, Android theo vai trò | Kiểm tra luồng chính và trạng thái lỗi trên kích thước màn hình representative |
| NFR-06 — Performance & Testability | Mục tiêu **≤ 5 giây** danh sách lớp và **≤ 10 giây** báo cáo tháng trên dataset tương đương 3 năm học trong môi trường thử nghiệm có mô tả | Kịch bản đo thời gian, bộ dữ liệu và cấu hình, kết quả đo thực tế; test tự động các luồng trọng yếu |

**Đây là mục tiêu đề xuất để nghiệm thu, không phải kết quả đo đã đạt.** Chi tiết số liệu thử nghiệm, môi trường, benchmark và điều kiện áp dụng đặt trong Test Plan/STD; nếu GVHD điều chỉnh ngưỡng thì cập nhật thống nhất.

## 7. Work Packages theo phiếu v1.3

Năm gói việc sau là **phân chia phạm vi trách nhiệm**, không áp đặt rằng người đang phụ trách từng module trong repo hiện tại phải đổi ngay. Việc gán người/nhánh/PR thực tế và những API đã được merge phải đối chiếu `continuity.md`, Git và kế hoạch nhóm.

| WP | Nội dung / đầu ra |
| --- | --- |
| WP1 — Analysis and Web | Business rules, SRS, giao diện React/TS, API integration; **đầu ra:** màn web, hợp đồng tích hợp, integration test. |
| WP2 — Nutrition Backend | Nguồn dinh dưỡng, nguyên liệu, công thức versioned, P-L-C, so sánh nhóm tuổi, nguyên liệu dự kiến, duyệt/công bố thực đơn; **đầu ra:** nutrition/menu API, data model, calculation/workflow test. |
| WP3 — Meal Operations Backend | Báo không ăn, lịch ăn, tự chốt theo từng bữa, điều chỉnh, bếp xác nhận, món đã phục vụ/đổi món; **đầu ra:** operation API, lịch sử phiên, cutoff/workflow test. |
| WP4 — Audit and Reporting Backend | Authorization integration, audit, lineage, báo cáo/xuất, snapshot và tái lập; **đầu ra:** audit/report API, reproducibility test. |
| WP5 — Android and Evidence | Flutter Android, màn món phục vụ, chụp ảnh offline, queue/sync và tích hợp; **đầu ra:** ứng dụng Android, chức năng ảnh và sync test. |

**Phụ thuộc kỹ thuật chính:** WP3 thống nhất `MealSession`/cutoff và settlement version trước khi WP2 tính nguyên liệu theo số suất; WP2 thống nhất schema công thức/nguồn và bản công bố trước khi WP4 tạo report lineage; WP5 chốt contract ảnh/sync với BE trước khi hoàn thiện giao diện. Mọi thay đổi schema/API dùng chung cần có review trước merge.

## 8. Kịch bản demo end-to-end (minimum demonstrable workflow)

1. Admin tạo năm học, hai lớp, nhập trẻ từ Excel, phân công Teacher và liên kết Parent theo quy trình xác minh.
2. Admin tạo ngày/phiên ăn. Trẻ ghi danh hợp lệ mặc định dự kiến ăn. Parent báo không ăn theo ngày/khoảng ngày; Teacher xử lý ngoại lệ hợp lệ trước thời điểm khóa của từng bữa.
3. Hệ thống tự khóa bữa sáng ngày D lúc 21:00 ngày D−1; bữa trưa và xế lúc 07:00 ngày D. Demonstrate báo 22:00 làm thay đổi trưa/xế nhưng không sửa bản sáng.
4. Teacher yêu cầu điều chỉnh bữa trưa sau 07:00; Admin duyệt; bếp thấy bản trước/bản mới và xác nhận đã nhận; bản cũ không bị ghi đè.
5. Kitchen lập menu, công thức và dữ liệu nguyên liệu; xem kcal/P-L-C theo nguồn/phiên bản, đối chiếu nhóm tuổi; Admin duyệt/công bố.
6. Kitchen xuất bảng **nguyên liệu dự kiến** theo menu và số suất áp dụng. Không có đơn mua hàng, kho hay nhà cung cấp.
7. Kitchen ghi món đã phục vụ/đổi món. Android **offline** chụp ảnh món, đóng/mở lại app vẫn còn hàng đợi, sau đó đồng bộ; Admin duyệt ảnh. Parent chỉ xem dữ liệu đã công bố của con mình.
8. Admin duyệt/xuất báo cáo; drill-down từ một chỉ số dinh dưỡng đến nguồn; thay đổi hợp lệ một dữ liệu nguồn sau khi duyệt, hiển thị **As-Approved** và **Recomputed** riêng cùng nguyên nhân chênh lệch.

## 9. Các tình huống nghiệm thu trọng yếu

- **Cutoff:** đúng biên giờ, ngày D−1 qua ngày D, ngày nghỉ, không có phiên sáng, job chốt đến trễ, retry/trùng job, client/server lệch giờ, báo không ăn cả ngày sau cutoff sáng nhưng trước cutoff trưa.
- **Enrollment/Permission:** chuyển lớp, thêm/ngừng học, hai Parent cùng một trẻ, Teacher khác lớp, Parent chưa được xác minh, thu hồi liên kết và phiên đăng nhập.
- **Adjustment:** duyệt trùng, phiếu dựa trên version cũ, từ chối, không giảm âm, phiếu theo trẻ và phiếu số lượng bếp không gắn trẻ, bảo toàn bản gốc.
- **Nutrition:** gram, 100 g phần ăn được, tỷ lệ ăn được, tránh chuyển đổi hai lần, rounding, công thức thay phiên bản, cập nhật giá trị thành phần, nhóm tuổi, nguồn tham chiếu thiếu/không hợp lệ.
- **Serving/Offline:** đổi món và lý do, ảnh thuộc sai bữa, offline sau restart, upload retry gây trùng, dấu thời gian chụp khác thời gian upload, ảnh chưa duyệt hoặc ngoài scope Parent.
- **Reports:** số suất phiên hiện hành khác lịch sử, snapshot giữ nguyên sau correction, nguồn truy vết đủ, hiển thị rõ trường hợp dữ liệu cũ không có nguồn thay vì tự tạo nguồn giả.

## 10. Kiến trúc và nguyên tắc triển khai

- **Web:** React + TypeScript + Vite, responsive; giao diện tiếng Việt.
- **Android:** Flutter (Dart), dùng chung backend API; lưu cục bộ cho ảnh/metadata chờ đồng bộ của bếp.
- **Backend:** C# / ASP.NET Core 8, REST API, Clean Architecture; xác thực/phân quyền thực thi ở server, không chỉ ẩn nút ở FE.
- **Database:** PostgreSQL + EF Core Code First/Migrations; giữ dữ liệu lịch sử, phiên bản và migration an toàn. Dùng thời gian lưu trữ UTC và so cutoff theo `Asia/Ho_Chi_Minh` hoặc cách tương đương được kiểm thử; không tin đồng hồ client để xét hạn.
- **Kiểm thử:** Swagger/API contracts, xUnit và Vitest theo module đã có; thêm Flutter/unit-widget-integration/E2E phù hợp thực tế repo. Kiểm tra PostgreSQL cho điều kiện unique/concurrency, không chỉ SQLite mocks.
- **Triển khai:** không bắt buộc Docker; có hướng dẫn chạy, cấu hình secret ngoài Git, migrations, dữ liệu thử và sao lưu/phục hồi.
- **Lưu ảnh:** file/object storage ngoài nội dung bảng lớn; DB lưu metadata, liên kết bản ghi, người/timestamp, trạng thái duyệt. URL truy cập ảnh phải có kiểm tra quyền; không phát public link vô thời hạn cho ảnh chứa dữ liệu nhạy cảm.
- **Lịch sử:** số suất/công thức/thực đơn/báo cáo đã chốt/duyệt phải theo version hoặc snapshot bất biến; không bắt toàn bộ domain phải event sourcing hoàn chỉnh.

## 11. Quy tắc chuyển đổi từ hệ thống đã code theo 07:30

Đây là **kế hoạch rà soát cần triển khai**, không phải xác nhận migration đã chạy.

1. Liệt kê mọi chỗ đang giả định `07:30` chung: validation của Parent, Teacher, enrollment, meal calendar, settlement service, API contract, FE/Android, các job, test và dữ liệu demo.
2. Tách `cutoffAt` theo **ngày phục vụ + loại bữa**; thời điểm nhận báo lưu bằng thời gian server. Quy tắc “chốt thủ công từ 07:30” chỉ là **legacy behavior** ở trạng thái cũ.
3. Đảm bảo một sự kiện báo không ăn theo **ngày** có thể được xét khác nhau cho từng bữa; các bản đang khóa có nguồn và số suất tại đúng cutoff, không tính lại theo dữ liệu sau giờ chốt.
4. Lưu quan hệ version bản chốt/điều chỉnh, giữ dữ liệu cũ, không ngầm đổi giờ/ý nghĩa của bản đã phát hành. Nếu dữ liệu legacy thiếu nguồn, gắn tình trạng không đủ nguồn; không dựng lịch sử giả.
5. Bổ sung test 21:00 D−1 và 07:00 D, job chậm, idempotency, concurrent adjustment và lịch của ngày không có bữa.
6. Tách việc đồng bộ ảnh offline trong Android khỏi session/auth và các màn khác; dữ liệu chưa gửi phải giữ an toàn, tách thời điểm chụp/thời điểm nhận và trạng thái duyệt.
7. Kiểm tra dữ liệu nguyên liệu/recipe hiện có: chức năng đã merge mới được `continuity.md` ghi là kcal/protein, **không suy ra P/L/C, đối chiếu nhóm tuổi, nguyên liệu dự tính hay publish menu đã đạt** nếu chưa có bằng chứng code/test.
8. Cập nhật `continuity.md` sau từng phần **theo những gì merge và đã kiểm thử thực tế**; các mô tả chưa xác minh giữ nhãn cần làm/đang làm.

## 12. Những điểm còn phụ thuộc xác nhận và không được tự bịa quy tắc

- Trường áp dụng các bữa nào, lịch nghỉ/lễ/học bù thực tế; xác nhận 21:00/07:00 là giờ thử nghiệm và quy tắc xử lý ngày nghỉ.
- Hạn công bố thực đơn, ai nhập và ai duyệt dữ liệu nguồn dinh dưỡng, bộ khoảng tham chiếu theo nhóm tuổi từ Chương trình giáo dục mầm non, rule làm tròn và bộ ca mẫu tính độc lập.
- Mẫu xuất báo cáo, ai duyệt/ký theo quy trình trường, cách trình bày thay món và ảnh cho phụ huynh.
- Quy tắc nghiệp vụ cho ghi danh/chuyển lớp trong giai đoạn **sáng đã khóa nhưng trưa/xế chưa khóa**, đặc biệt khi lịch có phiên sáng.
- Cấu hình thiết bị/môi trường thử Android offline, quy định ảnh món ăn không chứa dữ liệu cá nhân trẻ và thời hạn lưu ảnh.
- FR-21 Optional có được triển khai theo kế hoạch nhóm không; nếu có, trường xác nhận ghi chú ăn uống trước khi cho bếp thấy. Không tự sinh thực đơn cá nhân hoặc khẳng định “an toàn dị ứng”.
- Bản phiếu v1.3 dùng làm **căn cứ nhóm đề xuất** cho tới khi GVHD xác nhận cuối; không tự tuyên bố văn bản/giải pháp được cơ quan nhà nước chứng nhận.

## 13. Quan hệ với continuity, roadmap và repository

- `PROPOSAL.md` (tài liệu này): **WHAT / WHY / quy tắc đích của phạm vi v1.3**.
- `continuity.md`: **CURRENT STATE** tại thời điểm cập nhật của người phát triển; có thể chứa logic cũ 07:30, trạng thái mobile/OTP/import cũ. Đọc như nhật ký hiện trạng, **không phải định nghĩa scope mới**.
- `docs/implementation-plan.md` (nếu tồn tại): **HOW / WHEN** theo roadmap; phải cập nhật nếu còn công việc bị cắt như kiểm thực, quản lý tài chính hoặc thiếu tự chốt/ảnh offline.
- Phiếu đăng ký Capstone v1.3 và SRS/SDD sau khi được xác nhận: mốc đối chiếu phạm vi/thiết kế; thay đổi mới cần có log quyết định và đánh giá tác động.
- **Không refactor hàng loạt khi mới thay tài liệu**. Coding agent phải báo cáo GAP giữa v1.3 và codebase, đề xuất thứ tự việc trước, sau đó nhóm mới giao từng phần thực hiện.

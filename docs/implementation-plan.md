# MealTrace — Kế hoạch triển khai tiếp theo

## 1. Điểm xuất phát

- Mốc trước phần điều chỉnh: `cf1bdfd`, nhánh `main`; điều chỉnh sau chốt được bàn giao cùng commit cập nhật kế hoạch này.
- Đã có đăng nhập email/SĐT, 4 role, quản lý tài khoản/phạm vi, Admin reset mật khẩu có audit.
- Đã có mã trẻ, ghi danh/chuyển lớp/ngừng học/ghi danh lại theo ngày, giữ liên kết phụ huynh; tìm kiếm/phân trang; chỉnh sửa bằng modal.
- Đã có báo/hủy vắng, tạo phiên ăn, xem số suất theo Enrollment và chốt một lần từ 07:30 UTC+7.
- Đã triển khai ngoại lệ trước chốt: có/vắng/khôi phục mặc định, nguồn quyết định, lịch sử actor và bảo vệ phiên bản nguồn.
- Parent đăng ký không ăn trong năm học đã thiết lập (kể cả vẫn đi học), chọn tuần/tháng/đến hết năm học, sửa khoảng bằng modal hoặc hủy; giữ lịch sử và Enrollment. Không dùng trạng thái không ăn để suy ra vắng học.
- Năm học dùng chung: tạo trước lớp, nhập mốc lần đầu; năm tiếp theo sao chép lịch cũ/tăng năm, Admin kiểm tra và lưu. Form lớp/phiên dùng dropdown. Import sau này chọn năm chung nếu Excel thiếu niên khóa/ngày.
- Kiểm chứng gần nhất ở workspace: 45 test SQLite + 8 test PostgreSQL, 5 test FE; build thành công. Kiểm tra Admin mở/đóng modal ngoại lệ/tạo năm học, layout ngoại lệ 390px không tràn ngang; chưa visual QA toàn bộ màn hình, chưa chạy Sonar mới.
- Thực đơn, món thực tế/ảnh, báo cáo và lineage hiện mới có model/API đọc nền tảng; chưa hoàn thành workflow.

Ngoại lệ trước chốt và năm học dùng chung đã push. Lịch vận hành/tạo hàng loạt đã triển khai và đưa vào đợt push này; cần nghiệm thu UI. Điều chỉnh sau chốt đã triển khai và đưa vào đợt push này, cần nghiệm thu UI; thực đơn và các chặng sau là kế hoạch. Checklist test thủ công vẫn giữ local; không đưa vào Git. README để trống. Các bước không cần Docker.

## 2. Thứ tự và tiêu chí hoàn thành

| Ưu tiên | Chặng | Kết quả bàn giao | Điều kiện chuyển sang chặng sau |
| --- | --- | --- | --- |
| P0 | Nghiệm thu lớp/trẻ | Sửa lỗi từ test người dùng, kiểm tra modal desktop/mobile và Sonar khi có scan. | Không có lỗi tái hiện làm mất dữ liệu hoặc sai quyền trong các luồng đã làm. |
| P1 | Ngoại lệ trước chốt | Chọn có/vắng/khôi phục mặc định; nguồn quyết định và lịch sử theo trẻ. | Đúng lớp tại ngày ăn, đúng cut-off, giữ lịch sử; hoàn tác không vô hiệu báo vắng của Parent. |
| P1 | Lịch bữa ăn/ngày nghỉ | Lịch vận hành và kiểm tra khi tạo phiên ăn; trạng thái phiên hủy có lý do. | Ngày nghỉ không phát sinh suất; không sửa/xóa bản chốt đã có. |
| P1 | Điều chỉnh sau chốt | Đề nghị, duyệt/từ chối và bản áp dụng có phiên bản; bếp thấy chênh lệch. | Bản gốc bất biến; truy được nguồn và người xử lý; duyệt đồng thời không tạo hai bản áp dụng. |
| P1 | Thực đơn/dinh dưỡng | Nhóm tuổi, nguyên liệu, đơn vị/nguồn dinh dưỡng, công thức có phiên bản; thực đơn Draft → Approved → Published. | Tính được Kcal/P-L-G từ nguồn; bản công bố giữ đúng công thức đã dùng. |
| P1 | Món thực tế/ảnh/Parent | Ghi chế biến/đổi món, ảnh có mốc thời gian và duyệt; Parent xem bản công bố. | Phân quyền theo con; không lộ ảnh ngoài phạm vi; đổi món có lý do và nguồn. |
| P1 | Báo cáo/truy vết | Báo cáo suất và dinh dưỡng, xuất tệp, bản duyệt và bản tính lại. | Từ chỉ số mở được dữ liệu nguồn; tái lập đúng bản đã duyệt. |

Không dùng lịch 14 tuần trong proposal để suy ra tiến độ đã hoàn thành. Chuyển chặng theo bằng chứng nghiệm thu, không chỉ theo kết quả build.

## 3. Chặng vừa hoàn thành: ngoại lệ trước chốt

### Quy tắc đã triển khai

- Admin được thao tác toàn trường; Teacher chỉ trong lớp được phân công, kiểm tra Enrollment tại ngày ăn.
- Danh sách hiển thị cả trẻ dự kiến ăn và trẻ bị loại vì báo vắng/ngoại lệ, để chọn được thao tác khôi phục.
- Có ba lựa chọn: **dự kiến có suất**, **dự kiến không có suất**, **khôi phục mặc định**. Đây không phải xác nhận điểm danh thực tế.
- Khôi phục mặc định = không dùng override của giáo viên; tiếp tục áp dụng Enrollment và báo vắng Parent. Trẻ vẫn có báo vắng không tự có suất sau hoàn tác.
- Mỗi thay đổi cần lý do, actor, thời điểm và liên kết sự kiện trước; ghi thêm sự kiện, không xóa lịch sử cũ.
- Chỉ được thay đổi trước CutoffAt và khi chưa chốt. Sau cut-off, kể cả chưa bấm chốt, không sửa số suất dự kiến; chuyển sang quy trình điều chỉnh ở chặng sau.

### Công việc

1. BE: bổ sung metadata actor và hành động khôi phục vào ngoại lệ; không dựng actor giả cho bản ghi cũ thiếu thông tin.
2. BE: API đọc danh sách đủ trẻ/nguồn loại khỏi suất và lịch sử ngoại lệ, cùng quy tắc scope với API ghi.
3. BE: thống nhất kết quả tính suất; mặc định → báo vắng → override đang hiệu lực, khôi phục trả về mặc định + báo vắng.
4. BE: bảo vệ thao tác đồng thời trên cùng trẻ/phiên bằng phiên bản nguồn và transaction; yêu cầu dùng bản cũ trả 409.
5. FE: Admin/Teacher mở modal thao tác; có/vắng/khôi phục, lý do, xem lịch sử; cập nhật số suất và toast đúng màu.
6. Test: dùng clock có thể điều khiển trong test để kiểm tra ngay trước/đúng/sau cut-off, không sửa giờ hệ thống hay DB người dùng.

### Ca nghiệm thu bắt buộc

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

## 4. Lịch và điều chỉnh đã triển khai; đợt tiếp theo là thực đơn/dinh dưỡng

### Lịch vận hành — nghiệm thu

- Admin lưu các thứ có ăn và bữa phục vụ theo năm học, không tự suy ra lịch trường.
- Chọn tuần/tháng/cả năm học → xem trước → tạo phiên còn thiếu; không tự chốt suất. Giờ chốt cố định 07:30 UTC+7.
- Modal chỉnh ngày nghỉ/ngày ăn đặc biệt/về lịch tuần, lý do bắt buộc, lịch sử trước/sau.
- Hủy/khôi phục phiên tương lai giữ ID; phiên hủy có 0 suất, không chốt/ghi ngoại lệ. Bữa mới cần tạo bổ sung qua preview.
- Khóa thay đổi sau cut-off hoặc ảnh hưởng dữ liệu đã chốt/công bố; revision/token ngăn xác nhận kế hoạch lỗi thời.
- Đã kiểm thử cả năm học, ngày lễ/học bù, lặp tạo không trùng, hủy/khôi phục, quyền, boundary cut-off và cập nhật đồng thời PostgreSQL. Chưa nghiệm thu trực quan đầy đủ.
- Tạo thủ công kiểm tra lịch; màn chốt suất lọc ngày/phân trang 25 phiên.
- Chuẩn bị test: lưu ngày năm học thực tế → lưu lịch tuần → xem trước một tuần → tạo → kiểm tra số suất → chỉnh ngày nghỉ/mở lại → đối chiếu lịch sử.
### Điều chỉnh sau chốt — nghiệm thu

- Teacher/Admin đề nghị theo trẻ, lý do/bản nguồn bắt buộc; Admin duyệt/từ chối bằng modal. Kitchen chỉ đọc. Admin được duyệt yêu cầu tự gửi.
- Duyệt tạo snapshot mới theo lớp và phiên bản, giữ bản gốc; từ chối không đổi suất. Yêu cầu lỗi thời cần từ chối/gửi lại, không tự rebase.
- Đã có nguồn Enrollment/báo vắng/ngoại lệ trong bản chốt mới; snapshot legacy đánh dấu thiếu nguồn, không suy diễn.
- Bếp thấy bản gốc, bản đang áp dụng, tên trẻ tăng/giảm và lịch sử/snapshot từng yêu cầu. Tổng ngày ăn chỉ tính bản mới nhất.
- Kiểm thử nguyên bản, scope, stale, duplicate, sau chốt, ghi danh muộn, nguồn legacy và concurrent PostgreSQL. Chưa visual QA đầy đủ.
- Chuẩn bị test: phiên đã chốt → Teacher gửi thêm/giảm → Admin xem nguồn/duyệt → Kitchen đối chiếu gốc/mới → gửi yêu cầu thứ hai từ nguồn cũ để kiểm tra conflict.
- Báo vắng muộn không tự đổi suất/hoàn phí; chưa có gửi thông báo bếp hoặc điều chỉnh tổng không gắn trẻ.
### Thực đơn và dinh dưỡng

- Bổ sung nhóm tuổi và nguồn thành phần dinh dưỡng được trường chấp nhận; chốt đơn vị/định lượng, tỷ lệ ăn được/hao hụt và làm tròn.
- Mở rộng model hiện tại: đang mới có năng lượng/protein, chưa đủ P-L-G và workflow thực đơn. Tiếp tục EF code-first theo từng use case.
- Bếp nhập món/công thức, lập tuần/ngày/phiên; Admin rà soát và công bố. Hạn 16:00 là đề xuất, chưa phải quy tắc đã xác nhận.
- Công thức và dữ liệu nguồn có phiên bản; bản thực đơn công bố cố định phiên bản đã dùng. Cảnh báo dữ liệu thiếu không biến thành số 0 giả.
- Đối chiếu kết quả với bộ ca tính độc lập trước khi dùng trong báo cáo. Dị ứng/lưu ý chuyên môn cần nhà trường xác nhận; không tự sinh thực đơn riêng.

### Món thực tế, ảnh và báo cáo

- Bếp ghi món thực tế/định lượng/đổi món, lý do và ảnh; lưu thời điểm chụp và upload, Admin duyệt công bố.
- Parent xem đúng trẻ liên kết và nội dung đã công bố; ảnh cần API kiểm tra quyền trước khi cấp truy cập.
- Báo cáo lưu dữ liệu nguồn, phiên bản công thức tính và quy tắc làm tròn; phân biệt số suất dự kiến, suất đã chốt/điều chỉnh và món phục vụ.
- Bản đã duyệt giữ nguyên; bản tính lại chỉ ra phần chênh lệch. Không dùng scaffold ReportSnapshot hiện tại như báo cáo đã nghiệm thu.

## 5. Phần chờ dữ liệu hoặc làm sau

| Hạng mục | Điều kiện bắt đầu |
| --- | --- |
| Import Excel lớp/trẻ/Parent | Có file mẫu: mã trẻ, lớp/niên khóa, SĐT và cách ghi nhiều người giám hộ; làm preview/lỗi từng dòng/nhập lại không trùng. |
| SMS/email | Sau luồng tạo/import ổn định; chọn dịch vụ gửi và cấu hình riêng; không gửi tới số demo. |
| Hủy/sửa lịch chuyển lớp tương lai | Chốt cách xử lý lịch đã xếp; giữ audit và không thay đổi dữ liệu đã chốt. |
| Mobile, tài chính, offline, dashboard nâng cao | Sau khi luồng MVP chính đạt nghiệm thu. |

## 6. Quy tắc thực hiện mỗi chặng

- Đọc continuity và phạm vi trước khi sửa; ghi rõ thay đổi model/migration, hành vi cũ và mới.
- BE kiểm tra quyền và dữ liệu; FE tái dùng clay style, Modal và Sonner. Modal giữ dữ liệu khi lỗi, chặn đóng khi đang lưu.
- Test hành vi và các rủi ro cụ thể: sai scope, cut-off, phiên bản, transaction, nguồn báo cáo; dùng PostgreSQL biệt lập cho concurrency.
- Sau khi kiểm chứng, cập nhật PROPOSAL.md và continuity.md theo chức năng thực sự chạy; checklist thủ công cập nhật local.
- Commit/push khi người dùng yêu cầu. Không đưa config local, secrets hoặc checklist vào Git.

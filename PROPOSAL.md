# Đề xuất đề tài MealTrace

**Tên đề tài dự kiến:** MealTrace – Hệ thống quản lý bữa ăn bán trú và báo cáo dinh dưỡng có khả năng truy vết cho trường mầm non.

**Phiên bản:** Cập nhật ngày 30/09/2026 theo góp ý của giảng viên, quyết định nghiệp vụ và tiến độ hiện tại. Các mục tiêu MVP chưa hoàn thành được phân biệt tại mục tiến độ thực tế.

## 1. Tóm tắt

MealTrace hỗ trợ một trường mầm non quản lý quy trình bữa ăn bán trú từ danh sách lớp và trẻ, báo vắng, lập thực đơn, chốt số suất gửi bếp, ghi nhận món thực tế và hình ảnh, đến lập báo cáo dinh dưỡng cho nhà trường. Phụ huynh xem thực đơn và thông tin bữa ăn của trẻ đã được liên kết với tài khoản của mình.

Điểm tập trung của đề tài là **số liệu có nguồn gốc rõ ràng**. Một số suất chốt phải truy về danh sách trẻ đủ điều kiện ăn và các báo vắng tại thời điểm chốt; một chỉ số dinh dưỡng phải truy về món, định lượng, dữ liệu thành phần thực phẩm và phiên bản công thức đã dùng. Khi có điều chỉnh sau chốt hoặc sau khi báo cáo được duyệt, hệ thống giữ bản cũ và lập bản điều chỉnh để có thể giải thích chênh lệch.

Đề tài ưu tiên một quy trình vận hành hoàn chỉnh có thể trình diễn và kiểm thử trong phạm vi **một trường mầm non**, thay vì triển khai đồng thời tất cả phân hệ quản trị nhà trường.

## 2. Bối cảnh và vấn đề

Ở trường có tổ chức ăn bán trú, danh sách trẻ, báo nghỉ, thực đơn và số suất bếp cần chuẩn bị thường do nhiều người cập nhật vào những thời điểm khác nhau. Nếu tổng hợp thủ công, nhà trường khó trả lời một cách nhất quán: vì sao bếp nhận số suất này, món nào thực tế đã nấu, dữ liệu dinh dưỡng được tính từ đâu, và thông tin nào đã thay đổi sau khi báo cáo được duyệt.

Việc yêu cầu giáo viên đánh dấu “có mặt” cho toàn bộ trẻ mỗi ngày cũng tạo thao tác lặp lại. MealTrace áp dụng nguyên tắc **trẻ đang học bán trú được mặc định dự kiến ăn trong ngày học có tổ chức bữa ăn**. Phụ huynh chỉ cần báo vắng theo ngày hoặc theo khoảng thời gian; giáo viên kiểm tra và ghi ngoại lệ khi thực tế khác với thông tin đã báo. Cách này giảm thao tác hằng ngày mà vẫn lưu được nguồn dữ liệu dùng để chốt suất.

## 3. Mục tiêu và kết quả dự kiến

1. Tạo lớp, quản lý niên khóa và nhập danh sách lớp hoặc trẻ từ tệp theo mẫu; kiểm tra trùng lặp và trả kết quả nhập để nhà trường sửa lỗi.
2. Cho phụ huynh báo vắng một ngày hoặc vắng dài hạn, xem trạng thái xử lý, và hủy/sửa thông báo trước hạn chốt theo quy tắc của trường.
3. Tự tổng hợp số trẻ dự kiến ăn theo lớp và phiên ăn, chốt một bản số suất tại hạn quy định rồi chuyển danh sách/tổng số cho bếp.
4. Cho bếp lập thực đơn theo tuần/ngày, nhập công thức và thông tin nguyên liệu để tính dinh dưỡng; người được phân công kiểm tra và công bố trước hạn. Ghi rõ các thay đổi thực đơn sau công bố.
5. Ghi nhận món đã chế biến/phục vụ, trường hợp đổi món, ảnh món ăn và các mốc thời gian tạo/chuyển ảnh. Phụ huynh chỉ xem thực đơn và thông tin bữa ăn đã công bố của trẻ mình.
6. Tạo báo cáo cho nhà trường về suất ăn, thực đơn thực tế, dinh dưỡng ước tính theo khẩu phần và dữ liệu minh chứng; hỗ trợ xuất báo cáo để nhà trường rà soát, ký/duyệt và gửi cơ quan chuyên trách bằng quy trình hiện hành.
7. Cho phép truy ngược từ kết quả báo cáo tới dữ liệu nguồn và so sánh bản đã duyệt với bản tính lại khi có điều chỉnh.

**Giới hạn diễn giải:** “Dinh dưỡng ước tính” là giá trị tính từ nguyên liệu, định lượng và công thức cho khẩu phần dự kiến/phục vụ; hệ thống không đo lượng thức ăn từng trẻ đã thực sự ăn.

## 4. Người tham gia và hướng thiết kế code-first

Các vai trò dưới đây mô tả **trách nhiệm trong quy trình**, không giả định mỗi vai trò là một người riêng. Một tài khoản có thể được giao nhiều vai trò. Quyền trên từng dữ liệu vẫn phải được API kiểm tra theo trường, lớp và quan hệ phụ huynh–trẻ.

| Nhóm tham gia | Trách nhiệm chính trong phạm vi đề tài |
| --- | --- |
| Nhà trường / Admin | Quản lý tài khoản, niên khóa, lớp và trẻ; theo dõi số suất; duyệt/xuất báo cáo. |
| Giáo viên | Xem danh sách lớp; kiểm tra báo vắng và ghi các trường hợp thực tế khác dự kiến; xác nhận số suất lớp nhận nếu trường áp dụng. |
| Nhân viên bếp | Xem số suất chốt; lập thực đơn và công thức; ghi nhận chế biến, đổi món và ảnh món ăn. |
| Phụ huynh | Xem thông tin của trẻ được liên kết; báo vắng một ngày/dài hạn; khai báo lưu ý về ăn uống; xem thực đơn và thông tin bữa ăn đã công bố. |

Hệ thống triển khai **ASP.NET Core, EF Core và PostgreSQL theo hướng code-first**: model và migration được phát triển theo từng use case đã chốt. Proposal chỉ nêu các khái niệm dữ liệu cần có như lớp, trẻ, quan hệ giám hộ, khoảng vắng, thực đơn, món/công thức, bản chốt suất, lượt thực hiện bữa ăn, minh chứng và báo cáo. Nhóm không khóa cứng trước một danh sách 32 bảng hay một ma trận quyền chi tiết khi quy trình thực tế của trường chưa được xác nhận. Thay đổi schema được quản lý bằng EF migrations và có kiểm thử dữ liệu cũ.

## 5. Quy trình nghiệp vụ đề xuất

### 5.1. Thiết lập niên khóa và danh sách trẻ

Nhà trường tạo lớp theo niên khóa, thêm trẻ và phân công giáo viên. Trẻ có mã định danh cố định, nhập thủ công hoặc tự sinh, không đổi khi sửa tên/chuyển lớp. Mỗi lần ghi danh lưu lớp, khoảng ngày hiệu lực, lý do và người xử lý. Hệ thống hỗ trợ sửa tên lớp/hồ sơ trẻ bằng modal, chuyển lớp, ngừng học, ghi danh lại và xem lịch sử. Import Excel chờ mẫu tệp thực tế để chốt cách ánh xạ dữ liệu.

Khi đăng ký nhập học, phụ huynh cung cấp họ tên, SĐT và email nếu có. Nhà trường lưu hồ sơ vào file Excel. SĐT thuộc phụ huynh/người giám hộ; phụ huynh không bắt buộc có email. Hệ thống cho phép đăng nhập bằng SĐT hoặc email và không yêu cầu OTP/xác minh SĐT trong phạm vi đã thống nhất.

Khi import được triển khai, hệ thống xem trước dữ liệu, chuẩn hóa SĐT, gom phụ huynh theo SĐT và tạo tài khoản cho các số chưa tồn tại. Tài khoản đã có được dùng lại để liên kết với trẻ, không đặt lại mật khẩu. 100 trẻ có 100 SĐT mới khác nhau sẽ tạo 100 tài khoản; các trẻ cùng SĐT dùng chung tài khoản phụ huynh. Nhập lại cùng dữ liệu không tạo trùng trẻ, tài khoản hoặc quan hệ giám hộ. Tên cột và cách biểu diễn nhiều người giám hộ sẽ chốt sau khi có mẫu Excel.

Admin vẫn có thể tạo hoặc liên kết phụ huynh thủ công tại màn Lớp và trẻ để bổ sung/sửa sai dữ liệu. Một trẻ có nhiều phụ huynh; một phụ huynh có nhiều con. Thêm người giám hộ không tự xóa quan hệ cũ. Thông tin sai được sửa trong quản lý tài khoản và danh sách trẻ liên kết.

Mỗi tài khoản mới có mật khẩu tạm riêng, lưu hash/salt bằng Identity. Gửi thông tin đăng nhập qua SMS và qua email nếu có sẽ triển khai sau, chỉ thực hiện sau khi giao dịch import/tạo tài khoản thành công. Hiện chưa có dịch vụ gửi tin nhắn.

### 5.2. Khai báo báo vắng theo ngoại lệ

Vào ngày học có tổ chức bữa ăn, trẻ đủ điều kiện được hệ thống đánh dấu **dự kiến ăn**; phụ huynh không phải đăng ký lại mỗi ngày. Phụ huynh chọn một ngày hoặc khoảng ngày vắng. Vắng dài hạn chỉ áp dụng cho ngày học nằm trong khoảng hiệu lực; có thể kết thúc sớm và phải giữ lịch sử thay đổi. Trước giờ chốt, báo vắng hợp lệ loại trẻ khỏi số suất dự kiến; sau giờ chốt, thay đổi được ghi thành yêu cầu điều chỉnh, không âm thầm sửa bản chốt đã gửi bếp.

Giáo viên xem danh sách theo lớp và ghi **ngoại lệ** khi trẻ có mặt/vắng khác với dự kiến. Hệ thống không bắt giáo viên điểm danh từng trẻ “có” mỗi ngày. Tuy vậy, trạng thái “dự kiến ăn” **không được gọi là “đã có mặt thực tế”**; báo cáo phải thể hiện hai chỉ số riêng khi cần.

### 5.3. Chốt suất và gửi bếp

Giờ chốt hiện tại cố định **07:30 UTC+7**. Từ giờ chốt trở đi, Admin thực hiện thao tác chốt; hệ thống lưu bản chốt theo **ngày, phiên ăn và lớp**, gồm danh sách trẻ đủ điều kiện, báo vắng đã nhận đúng hạn, tổng số suất và thời điểm chốt. Giáo viên/nhà trường kiểm tra ngoại lệ; bếp nhận cùng một phiên bản số liệu. Hệ thống ghi nhận người xác nhận và thời điểm nhận nếu quy trình trường yêu cầu.

Chức năng điều chỉnh sau chốt chưa triển khai. Trong quy trình mục tiêu, nếu có thay đổi sau giờ chốt, người có quyền lập điều chỉnh với lý do, thời điểm, người thao tác và liên kết bản chốt cũ. Quy tắc có chuẩn bị thêm/giảm suất và quy tắc hoàn phí là hai quyết định riêng của nhà trường; không mặc định báo vắng muộn sẽ tự hủy suất hoặc tự hoàn tiền.

### 5.4. Lập, kiểm tra và công bố thực đơn

Bếp lập thực đơn theo tuần và định lượng cho từng món; nhà trường và bếp kiểm tra công thức, dữ liệu nguyên liệu, cảnh báo dinh dưỡng và các lưu ý ăn uống được nhà trường xác nhận. Bản công bố có thời điểm, người duyệt và phiên bản. **Đề xuất** hạn công bố là trước 16:00 của ngày học trước ngày ăn; quy tắc cụ thể phải được trường/giảng viên chốt. Sửa sau công bố tạo phiên bản mới, nêu lý do và thông báo cho người liên quan.

Thông tin dinh dưỡng được tính từ định lượng và dữ liệu thành phần thực phẩm có nguồn; khi cần nhập số liệu thủ công, phải ghi đơn vị, nguồn và người xác nhận. Hệ thống không cho nhập một chỉ số báo cáo rời khỏi các dữ liệu tạo ra nó.

### 5.5. Thực hiện bữa ăn và hình ảnh

Bếp xác nhận món thực tế đã chế biến, khối lượng/định lượng thực dùng và trường hợp thay thế món so với thực đơn. Sau chế biến, nhân viên chụp ảnh **món ăn**, gắn với ngày, phiên ăn và món; lưu riêng thời điểm chụp và thời điểm tải lên. Ảnh chỉ được công bố sau bước rà soát của nhà trường. Ưu tiên ảnh món/khay ăn, hạn chế xuất hiện mặt trẻ và dữ liệu cá nhân trong ảnh.

Thông tin kiểm thực ba bước và lưu mẫu thức ăn có thể được ghi nhận bằng biểu mẫu điện tử phù hợp với quy trình trường. Hồ sơ số hỗ trợ lưu, tra cứu và xuất minh chứng; mẫu thức ăn vật lý và việc thực hiện kiểm thực tại bếp vẫn do nhà trường chịu trách nhiệm.

### 5.6. Phụ huynh theo dõi và cung cấp lưu ý

Phụ huynh xem thực đơn đã công bố, trạng thái báo vắng, món thực tế/ảnh đã được duyệt và thông tin phù hợp với trẻ được liên kết. Phụ huynh có thể khai báo dị ứng hoặc lưu ý ăn uống; nhà trường xác nhận trước khi chuyển thành cảnh báo cho bếp. **Thực đơn riêng từng trẻ là tính năng mở rộng**, cần quy trình chuyên môn và xác nhận của nhà trường; hệ thống không tự suy luận thực đơn an toàn từ thông tin phụ huynh nhập.

### 5.7. Báo cáo và điều chỉnh

Nhà trường lập báo cáo theo kỳ từ số suất đã chốt, các điều chỉnh, món/định lượng thực tế và phiên bản dữ liệu dinh dưỡng đã dùng. Nếu trường dùng báo cáo chi phí nguyên liệu, báo cáo đó cũng ghi phiên bản giá tương ứng. Báo cáo lưu phiên bản công thức tính, quy tắc làm tròn và liên kết dữ liệu nguồn để có thể tái lập đúng bản đã duyệt. Khi sửa dữ liệu về sau, bản đã duyệt vẫn giữ nguyên; hệ thống tạo bản tính lại và giải thích nguồn chênh lệch.

Nhà trường có thể xuất báo cáo/minh chứng để gửi cơ quan chuyên trách theo mẫu được trường xác nhận. MVP **không tích hợp gửi trực tiếp** tới hệ thống của cơ quan quản lý và không tuyên bố phần mềm tự chứng nhận tuân thủ pháp luật.

## 6. Phạm vi MVP và ưu tiên

**Bắt buộc để nghiệm thu:**

1. Đăng nhập, gán vai trò và kiểm tra quyền truy cập dữ liệu theo lớp/trẻ.
2. Tạo/nhập danh sách lớp, nhập danh sách trẻ, liên kết phụ huynh–trẻ.
3. Báo vắng theo ngày/khoảng ngày; danh sách dự kiến ăn; ghi ngoại lệ; chốt suất có lịch sử.
4. Lập và công bố thực đơn đúng hạn; công thức/nguyên liệu đủ để tính chỉ số dinh dưỡng cơ bản.
5. Gửi số suất cho bếp; ghi món thực tế, thay thế món và ảnh món ăn; phụ huynh xem bản đã công bố.
6. Báo cáo suất ăn và dinh dưỡng cơ bản; từ một chỉ số có thể mở dữ liệu nguồn; bản đã duyệt và bản tính lại được lưu riêng.

**Mở rộng nếu còn thời gian sau khi MVP đạt tiêu chí:** gửi thông tin tài khoản qua SMS/email, quyền thanh tra chỉ đọc có thời hạn, thực đơn cá nhân theo xác nhận chuyên môn; nhập liệu offline và đồng bộ ảnh; quản lý phí/sổ cái/đối soát tháng; thông báo đa kênh; dashboard nâng cao; bộ báo cáo nhiều mẫu. Thống kê **chi phí nguyên liệu** có thể thực hiện trong MVP khi dữ liệu giá đủ tin cậy, nhưng không được đồng nhất với **mức phí nhà trường thu phụ huynh**.

**Ngoài phạm vi:** thanh toán trực tuyến, quản lý nhà cung cấp và kho theo kiểu ERP, truy vết tới nông trại, IoT, AI sinh thực đơn/tính dinh dưỡng, ứng dụng iOS/Android native trong MVP (thư mục mobile hiện chỉ giữ chỗ), quyết định khẩu phần điều trị hoặc xác nhận an toàn dị ứng tự động. MVP dùng web responsive; Docker không phải điều kiện để chạy hay nghiệm thu trên máy phát triển.

## 7. Yêu cầu chất lượng và cách kiểm chứng

| Nhóm | Yêu cầu có thể kiểm chứng |
| --- | --- |
| Tính đúng số suất | Bộ kịch bản gồm báo vắng đúng hạn, vắng dài hạn, hủy báo vắng, chuyển lớp, ngày nghỉ và báo vắng sau chốt phải cho kết quả đúng quy tắc đã thống nhất; bản chốt cũ không đổi sau điều chỉnh. |
| Dinh dưỡng | Công thức, đơn vị, dữ liệu nguồn và quy tắc làm tròn được ghi rõ; bộ ca mẫu được nhà trường và bếp đối chiếu với kết quả tính độc lập trước khi dùng để báo cáo. |
| Truy vết | Từ ít nhất một số suất và một chỉ số dinh dưỡng trong báo cáo mẫu, người dùng mở được bản ghi và phiên bản nguồn; có thể tái lập bản đã duyệt sau khi dữ liệu mới được điều chỉnh. |
| Quyền riêng tư | API kiểm tra phụ huynh–trẻ và giáo viên–lớp; ảnh không có URL công khai, có thời hạn truy cập và nhật ký truy cập; xác định thời hạn lưu/xóa theo quy định và chính sách của trường. |
| Nhập tệp | Hiển thị lỗi theo dòng và không tạo bản ghi trùng khi nhập lại cùng tệp. |
| Hiệu năng | Đo trên bộ dữ liệu thử tương đương 3 niên khóa; ngưỡng thời gian cụ thể cho danh sách lớp, chốt suất và báo cáo được chốt trước giai đoạn kiểm thử. |
| Triển khai | Chạy được React/TypeScript, ASP.NET Core và PostgreSQL không cần Docker; có migration, sao lưu và hướng dẫn phục hồi dữ liệu thử. |

## 8. Hướng kỹ thuật và kế hoạch 14 tuần

Repository gồm fe, be và mobile song song; mobile chỉ giữ chỗ. Frontend dùng React/TypeScript responsive với style claymorphism và Sonner toast; backend dùng ASP.NET Core; PostgreSQL lưu dữ liệu qua EF Core code-first migrations. API là nơi kiểm tra quyền; giao diện chỉ hiển thị hành động phù hợp. Những dữ liệu có ảnh hưởng đến bản chốt hoặc báo cáo đã duyệt được lưu phiên bản/điều chỉnh; không ghi đè vật lý bản đã phát hành. Ảnh được lưu ngoài bảng dữ liệu lớn, trong DB chỉ lưu metadata, quyền truy cập và liên kết hồ sơ.

| Giai đoạn | Tuần | Sản phẩm kiểm chứng |
| --- | --- | --- |
| Chốt quy trình và dữ liệu mẫu | 1–2 | Vai trò, hạn công bố/hạn chốt, quy tắc báo vắng, bộ ca mẫu được giảng viên/trường xác nhận. |
| Danh sách và báo vắng | 3–5 | Nhập lớp/trẻ, liên kết phụ huynh, báo vắng ngày/khoảng ngày, danh sách dự kiến ăn. |
| Thực đơn và chốt suất | 6–8 | Công thức/dữ liệu dinh dưỡng cơ bản, thực đơn có phiên bản, bản chốt theo lớp/phiên gửi bếp. |
| Thực hiện và phụ huynh xem | 9–10 | Món thực tế, đổi món, ảnh món đã duyệt, màn xem của phụ huynh theo đúng trẻ. |
| Báo cáo và truy vết | 11–12 | Báo cáo mẫu, drill-down, bản đã duyệt, điều chỉnh và so sánh tính lại. |
| Hoàn thiện | 13–14 | Kiểm thử tình huống, quyền riêng tư, nhập tệp, hiệu năng mẫu, trình diễn luồng từ đầu đến cuối. |

Kế hoạch 14 tuần là phân bổ tham khảo, không phải xác nhận các giai đoạn đã hoàn thành. Import triển khai sau khi có mẫu Excel; SMS/email làm sau luồng tạo tài khoản. Nhóm chỉ nhận thêm phần mở rộng sau khi luồng bắt buộc chạy ổn định và có kiểm thử.

## 9. Kịch bản trình diễn cuối kỳ

Nhà trường nhập hai lớp và danh sách trẻ; phụ huynh A báo vắng ba ngày, phụ huynh B báo vắng hôm nay trước giờ chốt. Hệ thống tự tạo danh sách dự kiến ăn, loại các ngày vắng hợp lệ và chốt số suất cho bếp. Bếp xem bản chốt, lập/công bố thực đơn, ghi một món thay thế có lý do và tải ảnh món thực tế. Phụ huynh chỉ xem dữ liệu của con mình. Nhà trường lập báo cáo, mở nguồn của một chỉ số dinh dưỡng, sau đó tạo một điều chỉnh hợp lệ và so sánh bản tính lại với bản đã duyệt. Kịch bản phải chứng minh rằng số suất, món và chỉ số báo cáo có thể giải thích được từ dữ liệu đã lưu.

## 10. Điểm cần giảng viên và trường xác nhận

1. Mẫu Excel nhập học, cách biểu diễn nhiều người giám hộ; hạn công bố thực đơn, giờ chốt suất, múi giờ và cách xử lý ngày nghỉ/lễ.
2. Trẻ mặc định dự kiến ăn khi nào; cách xử lý trẻ mới nhập học, chuyển lớp hoặc tạm ngừng bán trú.
3. Giáo viên có cần xác nhận danh sách ngoại lệ hay hệ thống tự chốt hoàn toàn; ai được sửa sau chốt.
4. Báo vắng muộn tác động thế nào đến chuẩn bị suất và hoàn phí; không suy ra hai quy tắc này là một.
5. Ai nhập và ai duyệt công thức/thành phần dinh dưỡng; nguồn dữ liệu chuẩn được trường chấp nhận.
6. Mẫu báo cáo gửi cơ quan chuyên trách, người ký và điều kiện duyệt/công bố cho phụ huynh.
7. Quy trình xác nhận dị ứng/lưu ý ăn uống và thời hạn lưu ảnh/hồ sơ của trẻ.

## 11. Căn cứ và giới hạn tham chiếu

- [Chương trình giáo dục mầm non, Thông tư 17/2009 được sửa đổi bởi Thông tư 28/2016 và 51/2020](https://vbpl.vn/TW/Pages/vbpq-toanvan.aspx?ItemID=147474&Keyword=): cần đối chiếu bản hiện hành khi chốt chỉ tiêu theo độ tuổi.
- [Công văn 988/BGDĐT-GDTC của Bộ GD&ĐT](https://moet.gov.vn/content/vanban/Lists/VBDH/Attachments/3346/cv-gui-so-gddt-tang-cuong-bao-dam-attp.pdf): dẫn Hướng dẫn tổ chức bữa ăn học đường cho mầm non và tiểu học tại QĐ 2195/QĐ-BGDĐT.
- [QĐ 1246/QĐ-BYT ngày 31/03/2017](https://attp.quangngai.gov.vn/docdetails.aspx?d=23696): tham chiếu cho kiểm thực ba bước và lưu mẫu; phạm vi biểu mẫu điện tử cần đối chiếu với quy trình thực tế của trường.
- [Hướng dẫn kèm QĐ 3958/QĐ-BYT](https://viendinhduong.vn/storage/app/uploads/public/2026/05/26/q_3958qbyt_huong_dan_dinh_duong_bua_an_hoc_uong.pdf) ghi phạm vi cho cơ sở giáo dục phổ thông và hướng cơ sở mầm non theo Chương trình giáo dục mầm non; do đó không dùng các ngưỡng của văn bản này như chuẩn bắt buộc trực tiếp cho MVP mầm non.

## Phụ lục: Đối chiếu góp ý của giảng viên

| Góp ý | Cách đưa vào proposal |
| --- | --- |
| Tạo lớp, import danh sách lớp/trẻ | Mục 5.1 và tiêu chí nhập tệp ở mục 7. |
| Điểm danh, phụ huynh báo vắng để hủy suất | Mục 5.2: mặc định dự kiến ăn, báo vắng ngày/khoảng ngày, giáo viên ghi ngoại lệ; mục 5.3 quy định bản chốt. |
| Gửi danh sách cho bếp | Mục 5.3: bản chốt theo ngày, phiên và lớp, có thời điểm/phiên bản. |
| Bếp tạo thực đơn, có giờ báo, nhập dinh dưỡng | Mục 5.4: bếp lập; người phụ trách rà soát; đề xuất hạn công bố và dữ liệu nguồn để tính. |
| Chế biến xong chụp hình món ăn | Mục 5.5: ảnh món thực tế, mốc chụp/tải và duyệt công bố. |
| Phụ huynh theo dõi thực đơn | Mục 5.6: chỉ xem thực đơn/món của trẻ liên kết và đã công bố. |
| Phụ huynh khai báo thông tin trẻ; thực đơn riêng là optional | Mục 5.6: lưu ý ăn uống có xác nhận; thực đơn cá nhân ở phần mở rộng. |
| Nhà trường lập báo cáo gửi cơ quan chuyên trách | Mục 5.7: xuất báo cáo/minh chứng để nhà trường rà soát và gửi theo quy trình được xác nhận. |

## 12. Tiến độ thực tế đến ngày 30/09/2026

| Hạng mục | Trạng thái |
| --- | --- |
| Đăng nhập email/SĐT, 4 vai trò, quản lý tài khoản/phạm vi | Đã triển khai. |
| Tạo lớp/trẻ thủ công, phân công giáo viên | Đã triển khai. |
| Tạo/liên kết Parent bằng SĐT, nhiều con/nhiều người giám hộ | Đã triển khai; Admin có thể bổ sung và sửa liên kết. |
| Báo/hủy vắng theo ngày/khoảng ngày | Đã triển khai, tối đa 90 ngày. |
| Ngoại lệ giáo viên trước chốt | API có; UI hiện chỉ ghi vắng, chưa có hoàn tác ngoại lệ. |
| Tạo phiên ăn, xem và chốt số suất theo lớp | Đã triển khai; chốt thủ công bởi Admin từ 07:30 UTC+7, một lần mỗi phiên. |
| Xem danh sách/chi tiết ngày ăn | Đã có phần đọc; món/ảnh có thể chưa có dữ liệu. |
| Import Excel và cấp tài khoản hàng loạt | Chưa triển khai; chờ mẫu tệp. |
| Gửi SMS/email, OTP | SMS/email làm sau; OTP không thuộc phạm vi đã thống nhất. |
| Mã trẻ, ghi danh/chuyển lớp/ngừng học/ghi danh lại | Đã triển khai, lưu lịch sử theo ngày hiệu lực; giữ liên kết phụ huynh. |
| Tìm kiếm/phân trang lớp, trẻ và danh mục phạm vi | Đã triển khai; giữ lựa chọn khi tìm kiếm/đổi trang. |
| Lịch ngày nghỉ, điều chỉnh sau chốt | Chưa triển khai workflow hoàn chỉnh. |
| Công thức/thực đơn, món thực tế/ảnh, báo cáo dinh dưỡng | Có model nền tảng; chưa có workflow hoàn chỉnh. |
| Quyền thanh tra | Có grant có thời hạn; chưa có quyền đọc nghiệp vụ hoàn chỉnh. |
| Mobile | Chỉ có thư mục giữ chỗ. |

Bản chốt lưu danh sách ID/tên trẻ nguồn theo lớp và không thay đổi khi hủy báo vắng hoặc thêm trẻ sau chốt. Đây là số suất dự kiến, không phải xác nhận có mặt thực tế. Bếp xem bản chốt trong hệ thống; chưa có thông báo gửi tự động.

Kết quả kiểm tra gần nhất: BE 17 kiểm thử tích hợp SQLite và 2 kiểm thử đồng thời PostgreSQL đạt; FE 5 kiểm thử logic đạt; build thành công. Test PostgreSQL dùng schema riêng và xóa schema sau test. Đã kiểm tra chuyển lớp cùng phiên bản, báo vắng trùng khoảng ngày và chốt suất đồng thời. Danh mục phạm vi đã thay giới hạn 200 mục bằng tìm kiếm/phân trang. Chưa kiểm tra trực quan toàn bộ UI hoặc chạy lại Sonar.

**Thứ tự tiếp theo:** kiểm thử nghiệp vụ lớp/trẻ với người dùng → quy trình ngoại lệ/điều chỉnh sau chốt và lịch ngày nghỉ → thực đơn/dinh dưỡng → món thực tế/ảnh và màn phụ huynh → báo cáo/truy vết → gửi SMS/email và hoàn thiện kiểm thử. Import triển khai khi có mẫu Excel. Không đánh dấu MVP hoàn thành từ các kết quả build/test hiện tại.

Ngày bắt đầu ghi danh bao gồm ngày đó; ngày kết thúc không bao gồm ngày đó. Sau 07:30 UTC+7, chuyển lớp/ngừng học áp dụng sớm nhất từ ngày mai, không sửa lùi ngày vào quá khứ. Số suất lấy lớp theo ngày ăn và chỉ tính dữ liệu ghi danh đã có trước giờ chốt; bản chốt đã lưu giữ nguyên. Dữ liệu trẻ cũ chưa có ngày ghi danh thật được chuyển đổi với ngày bắt đầu là ngày migration, có chú thích để đối chiếu hồ sơ; không suy diễn lịch sử lớp trước thời điểm này. Chưa có thao tác hủy lịch chuyển lớp đã lên kế hoạch.

**Cập nhật vai trò:** Hệ thống còn 4 vai trò ADMIN, TEACHER, KITCHEN_STAFF và PARENT. Bỏ NUTRITIONIST và ACCOUNTANT; phần công thức/dinh dưỡng thuộc trách nhiệm phối hợp của bếp và Admin, báo cáo do Admin quản lý. Nghiệp vụ tài chính chi tiết vẫn thuộc phần mở rộng.

### Bổ sung: Khôi phục mật khẩu qua nhà trường

Admin đối chiếu hồ sơ người dùng, cập nhật SĐT/email nếu cần trên tài khoản hiện có và cấp mật khẩu tạm mới. Hệ thống thu hồi phiên đăng nhập cũ, giữ nguyên quyền/liên kết với trẻ và lưu người xử lý, thời điểm, lý do. Mật khẩu lưu hash/salt; mật khẩu tạm chỉ hiển thị một lần để nhà trường chuyển cho người dùng. Chức năng này đã triển khai; khôi phục tự động qua SMS/email làm sau. Kiểm thử gần nhất sau bổ sung: BE 14/14, FE 5/5 đạt.

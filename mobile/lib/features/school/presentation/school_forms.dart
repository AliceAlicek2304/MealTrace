import '../domain/school_models.dart';
import '../domain/school_commands.dart';

enum FieldKind {
  text,
  email,
  phone,
  password,
  date,
  integer,
  choice,
  multiple,
  toggle,
  reference,
  references,
  lines,
}

class SchoolField {
  const SchoolField(
    this.key,
    this.label, {
    this.kind = FieldKind.text,
    this.required = true,
    this.options = const {},
    this.reference,
    this.collection,
    this.initial,
    this.maxLength = 150,
  });
  final String key;
  final String label;
  final FieldKind kind;
  final bool required;
  final Map<String, String> options;
  final SchoolOperation? reference;
  final String? collection;
  final Object? initial;
  final int maxLength;
}

const roleLabels = {
  'ADMIN': 'Ban giám hiệu / Admin',
  'TEACHER': 'Giáo viên',
  'KITCHEN_STAFF': 'Bếp & y tế',
  'PARENT': 'Phụ huynh',
};
const reasonField = SchoolField('reason', 'Lý do', maxLength: 500);
const studentProfileFields = [
  SchoolField(
    'dateOfBirth',
    'Ngày sinh',
    kind: FieldKind.date,
    required: false,
  ),
  SchoolField(
    'gender',
    'Giới tính',
    kind: FieldKind.choice,
    required: false,
    options: {'MALE': 'Nam', 'FEMALE': 'Nữ', 'OTHER': 'Khác'},
  ),
];
const classField = SchoolField(
  'classId',
  'Lớp',
  kind: FieldKind.reference,
  reference: SchoolOperation.assignedClasses,
);
const yearField = SchoolField(
  'schoolYear',
  'Năm học',
  kind: FieldKind.reference,
  reference: SchoolOperation.years,
);
const accountFields = [
  SchoolField('fullName', 'Họ và tên', maxLength: 120),
  SchoolField(
    'email',
    'Email',
    kind: FieldKind.email,
    required: false,
    maxLength: 254,
  ),
  SchoolField(
    'phoneNumber',
    'Số điện thoại',
    kind: FieldKind.phone,
    required: false,
    maxLength: 30,
  ),
  SchoolField(
    'roles',
    'Vai trò',
    kind: FieldKind.multiple,
    options: roleLabels,
    required: false,
  ),
  SchoolField(
    'status',
    'Trạng thái',
    kind: FieldKind.choice,
    options: {'ACTIVE': 'Hoạt động', 'SUSPENDED': 'Tạm khóa'},
    initial: 'ACTIVE',
  ),
  SchoolField(
    'classIds',
    'Lớp giáo viên phụ trách',
    kind: FieldKind.references,
    required: false,
    reference: SchoolOperation.scopes,
    collection: 'classes',
  ),
  SchoolField(
    'studentIds',
    'Trẻ liên kết với phụ huynh',
    kind: FieldKind.references,
    required: false,
    reference: SchoolOperation.scopes,
    collection: 'students',
  ),
  SchoolField(
    'inspectorAccessUntil',
    'Quyền thanh tra đến ngày',
    kind: FieldKind.date,
    required: false,
  ),
];

class SchoolForm {
  const SchoolForm(
    this.operation,
    this.title,
    this.fields, {
    this.help = 'Kiểm tra thông tin trước khi lưu.',
    this.bound = const [],
    this.confirm = false,
  });
  final SchoolOperation operation;
  final String title;
  final List<SchoolField> fields;
  final String help;

  /// Concurrency and identity values captured from the selected record.
  final List<String> bound;
  final bool confirm;
}

final schoolForms = <SchoolOperation, SchoolForm>{
  SchoolOperation.createParentLink: const SchoolForm(
    SchoolOperation.createParentLink,
    'Yêu cầu liên kết trẻ',
    [
      SchoolField(
        'schoolYear',
        'Năm học',
        kind: FieldKind.reference,
        reference: SchoolOperation.years,
      ),
      SchoolField(
        'classId',
        'Lớp của trẻ',
        kind: FieldKind.reference,
        reference: SchoolOperation.parentLinkClasses,
      ),
      SchoolField('studentName', 'Họ tên trẻ'),
      SchoolField(
        'relationship',
        'Quan hệ',
        kind: FieldKind.choice,
        options: {'FATHER': 'Cha', 'MOTHER': 'Mẹ', 'GUARDIAN': 'Người giám hộ'},
      ),
      SchoolField(
        'note',
        'Ghi chú để nhà trường đối chiếu',
        required: false,
        maxLength: 500,
      ),
    ],
    help:
        'Nhà trường đối chiếu và duyệt yêu cầu trước khi bạn được xem thông tin và báo vắng cho trẻ.',
  ),
  SchoolOperation.cancelParentLink: const SchoolForm(
    SchoolOperation.cancelParentLink,
    'Hủy yêu cầu liên kết',
    [],
    bound: ['revision'],
    confirm: true,
    help: 'Hủy yêu cầu đang chờ duyệt. Bạn có thể gửi lại sau.',
  ),
  SchoolOperation.reviewParentLink: const SchoolForm(
    SchoolOperation.reviewParentLink,
    'Duyệt / từ chối liên kết',
    [
      SchoolField(
        'approve',
        'Quyết định',
        kind: FieldKind.choice,
        options: {'true': 'Duyệt', 'false': 'Từ chối'},
        initial: 'false',
      ),
      SchoolField('reason', 'Kết quả đối chiếu / lý do', maxLength: 500),
    ],
    bound: ['revision'],
    confirm: true,
    help:
        'Đối chiếu danh tính và quan hệ phụ huynh với hồ sơ nhà trường trước khi cấp quyền truy cập trẻ.',
  ),
  SchoolOperation.bulkReviewParentLinks: const SchoolForm(
    SchoolOperation.bulkReviewParentLinks,
    'Xử lý danh sách đã chọn',
    [
      SchoolField(
        'approve',
        'Quyết định',
        kind: FieldKind.choice,
        options: {'true': 'Duyệt', 'false': 'Từ chối'},
        initial: 'false',
      ),
      SchoolField('reason', 'Kết quả đối chiếu / lý do', maxLength: 500),
    ],
    bound: ['classId', 'schoolYear', 'items'],
    confirm: true,
    help:
        'Chỉ xử lý các yêu cầu trong danh sách đã chọn. Đối chiếu trẻ, phụ huynh và SĐT trước khi xác nhận. Nếu danh sách đã thay đổi, tải lại và rà lại.',
  ),
  SchoolOperation.revokeParentLink: const SchoolForm(
    SchoolOperation.revokeParentLink,
    'Thu hồi liên kết sai',
    [reasonField],
    bound: ['revision'],
    confirm: true,
    help:
        'Gỡ quyền truy cập trẻ và thu hồi phiên đăng nhập phụ huynh. Giữ lịch sử duyệt; phụ huynh có thể gửi yêu cầu đúng để duyệt lại.',
  ),
  SchoolOperation.createUser: const SchoolForm(
    SchoolOperation.createUser,
    'Thêm tài khoản',
    accountFields,
  ),
  SchoolOperation.updateUser: const SchoolForm(
    SchoolOperation.updateUser,
    'Sửa tài khoản',
    accountFields,
  ),
  SchoolOperation.resetPassword: const SchoolForm(
    SchoolOperation.resetPassword,
    'Đặt lại mật khẩu',
    [reasonField],
    confirm: true,
    help:
        'Mật khẩu cũ và các phiên đăng nhập sẽ mất hiệu lực. Mật khẩu tạm chỉ hiển thị một lần.',
  ),
  SchoolOperation.changePassword: const SchoolForm(
    SchoolOperation.changePassword,
    'Đổi mật khẩu',
    [
      SchoolField(
        'currentPassword',
        'Mật khẩu hiện tại',
        kind: FieldKind.password,
        maxLength: 128,
      ),
      SchoolField(
        'newPassword',
        'Mật khẩu mới',
        kind: FieldKind.password,
        maxLength: 128,
      ),
    ],
    help:
        'Ít nhất 12 ký tự, có chữ số và ký tự đặc biệt. Đăng nhập lại sau khi đổi thành công.',
  ),
  SchoolOperation.createClass: const SchoolForm(
    SchoolOperation.createClass,
    'Tạo lớp',
    [SchoolField('name', 'Tên lớp', maxLength: 100), yearField],
  ),
  SchoolOperation.editClass: const SchoolForm(
    SchoolOperation.editClass,
    'Sửa lớp',
    [SchoolField('name', 'Tên lớp', maxLength: 100)],
  ),
  SchoolOperation.createStudent: const SchoolForm(
    SchoolOperation.createStudent,
    'Thêm trẻ',
    [
      SchoolField('fullName', 'Họ tên trẻ'),
      ...studentProfileFields,
      classField,
      SchoolField(
        'studentCode',
        'Mã trẻ (bỏ trống để tự tạo)',
        required: false,
        maxLength: 40,
      ),
      SchoolField('startDate', 'Ngày bắt đầu học', kind: FieldKind.date),
    ],
    help:
        'Có thể tạo trẻ khi chưa có SĐT hoặc tài khoản phụ huynh. Liên kết sau khi có thông tin.',
  ),
  SchoolOperation.editStudent: const SchoolForm(
    SchoolOperation.editStudent,
    'Sửa hồ sơ trẻ',
    [SchoolField('fullName', 'Họ tên trẻ'), ...studentProfileFields],
    bound: ['revision'],
  ),
  SchoolOperation.changeEnrollment: const SchoolForm(
    SchoolOperation.changeEnrollment,
    'Ghi danh / chuyển lớp',
    [
      SchoolField(
        'classId',
        'Lớp tiếp nhận (để trống để ngừng học)',
        required: false,
        kind: FieldKind.reference,
        reference: SchoolOperation.assignedClasses,
      ),
      SchoolField('effectiveDate', 'Ngày hiệu lực', kind: FieldKind.date),
      reasonField,
    ],
    bound: ['revision'],
    confirm: true,
    help:
        'Sau 07:30, thay đổi áp dụng từ ngày mai. Ngừng học giữ nguyên liên kết phụ huynh.',
  ),
  SchoolOperation.linkParent: const SchoolForm(
    SchoolOperation.linkParent,
    'Liên kết phụ huynh',
    [
      SchoolField(
        'phoneNumber',
        'SĐT phụ huynh',
        kind: FieldKind.phone,
        maxLength: 30,
      ),
      SchoolField(
        'fullName',
        'Họ tên nếu tạo mới',
        required: false,
        maxLength: 120,
      ),
      SchoolField(
        'sendRegistrationNotification',
        'Gửi hướng dẫn qua WhatsApp',
        kind: FieldKind.toggle,
        initial: false,
        required: false,
      ),
    ],
    help:
        'Tài khoản đã tồn tại được liên kết ngay. Tài khoản mới cần họ tên. Hồ sơ vẫn được lưu khi gửi tin thất bại.',
  ),
  SchoolOperation.saveYear: const SchoolForm(
    SchoolOperation.saveYear,
    'Thiết lập năm học',
    [
      SchoolField('code', 'Niên khóa (ví dụ 2026-2027)'),
      SchoolField('startDate', 'Ngày bắt đầu', kind: FieldKind.date),
      SchoolField('endDate', 'Ngày kết thúc', kind: FieldKind.date),
    ],
    bound: ['sourceYearCode'],
    confirm: true,
    help: 'Kiểm tra ngày thực tế của trường. Các lớp dùng chung mốc năm học.',
  ),
  SchoolOperation.reportAbsence: const SchoolForm(
    SchoolOperation.reportAbsence,
    'Đăng ký không ăn',
    [
      SchoolField(
        'studentId',
        'Trẻ',
        kind: FieldKind.reference,
        reference: SchoolOperation.children,
      ),
      SchoolField('fromDate', 'Từ ngày', kind: FieldKind.date),
      SchoolField('toDate', 'Đến ngày', kind: FieldKind.date),
      reasonField,
    ],
    help:
        'Tính cả ngày bắt đầu và kết thúc. Không thay đổi ghi danh hoặc suất đã chốt.',
  ),
  SchoolOperation.replaceAbsence: const SchoolForm(
    SchoolOperation.replaceAbsence,
    'Sửa khoảng không ăn',
    [
      SchoolField('fromDate', 'Từ ngày', kind: FieldKind.date),
      SchoolField('toDate', 'Đến ngày', kind: FieldKind.date),
      reasonField,
    ],
    bound: ['studentId'],
    help: 'Bản cũ giữ trong lịch sử. Suất đã qua giờ chốt không thay đổi.',
  ),
  SchoolOperation.cancelAbsence: const SchoolForm(
    SchoolOperation.cancelAbsence,
    'Hủy đăng ký / ăn lại',
    [],
    confirm: true,
    help:
        'Hủy khoảng không ăn đã chọn? Lịch sử và suất đã chốt được giữ nguyên.',
  ),
  SchoolOperation.createMealDay:
      const SchoolForm(SchoolOperation.createMealDay, 'Tạo phiên ăn', [
        SchoolField('date', 'Ngày ăn', kind: FieldKind.date),
        SchoolField('mealType', 'Bữa ăn', initial: 'Bữa trưa'),
        yearField,
      ]),
  SchoolOperation.settle: const SchoolForm(
    SchoolOperation.settle,
    'Chốt suất',
    [],
    confirm: true,
    help:
        'Chốt bản suất gửi bếp? Sau chốt, thay đổi cần lập phiếu điều chỉnh và Admin duyệt.',
  ),
  SchoolOperation.recordException: const SchoolForm(
    SchoolOperation.recordException,
    'Điều chỉnh trước chốt',
    [
      SchoolField(
        'action',
        'Dự kiến suất',
        kind: FieldKind.choice,
        options: {
          'EAT': 'Có suất',
          'ABSENT': 'Không có suất',
          'DEFAULT': 'Khôi phục mặc định',
        },
        initial: 'ABSENT',
      ),
      reasonField,
    ],
    bound: ['studentId', 'expectedEventId'],
    help: 'Lưu lịch sử thay đổi. Qua giờ chốt cần dùng điều chỉnh sau chốt.',
  ),
  SchoolOperation.requestAmendment: const SchoolForm(
    SchoolOperation.requestAmendment,
    'Yêu cầu điều chỉnh sau chốt',
    [
      SchoolField(
        'willEat',
        'Tăng suất (tắt để giảm)',
        kind: FieldKind.toggle,
        initial: true,
        required: false,
      ),
      SchoolField(
        'studentIds',
        'Chọn trẻ thay đổi',
        kind: FieldKind.references,
        reference: SchoolOperation.amendments,
        collection: 'candidates',
        required: false,
      ),
      SchoolField(
        'quantity',
        'Số suất riêng cho bếp (khi không chọn trẻ)',
        kind: FieldKind.integer,
        required: false,
      ),
      reasonField,
    ],
    bound: ['classId', 'baseSettlementId'],
    confirm: true,
    help:
        'Bản gốc giữ nguyên. Suất hiện hành chỉ đổi sau khi Admin duyệt. Chọn nhiều trẻ hoặc nhập số suất riêng cho bếp.',
  ),
  SchoolOperation.reviewAmendment: const SchoolForm(
    SchoolOperation.reviewAmendment,
    'Duyệt phiếu điều chỉnh',
    [
      SchoolField(
        'approve',
        'Quyết định',
        kind: FieldKind.choice,
        options: {'true': 'Duyệt', 'false': 'Từ chối'},
        initial: 'true',
      ),
      reasonField,
    ],
    confirm: true,
    help:
        'Duyệt tạo bản suất mới áp dụng cho bếp. Đối chiếu bản trước/sau trước khi xác nhận.',
  ),
  SchoolOperation.schedule: const SchoolForm(
    SchoolOperation.schedule,
    'Thiết lập lịch tuần',
    [
      SchoolField(
        'weekdays',
        'Ngày trong tuần',
        kind: FieldKind.multiple,
        required: false,
        initial: ['1', '2', '3', '4', '5'],
        options: {
          '1': 'Thứ Hai',
          '2': 'Thứ Ba',
          '3': 'Thứ Tư',
          '4': 'Thứ Năm',
          '5': 'Thứ Sáu',
          '6': 'Thứ Bảy',
          '0': 'Chủ nhật',
        },
      ),
      SchoolField(
        'mealTypes',
        'Tên bữa ăn (mỗi dòng một bữa)',
        kind: FieldKind.lines,
        initial: ['Bữa trưa'],
      ),
      reasonField,
    ],
    bound: ['expectedRevision'],
  ),
  SchoolOperation.calendarDay: const SchoolForm(
    SchoolOperation.calendarDay,
    'Sửa ngày ăn',
    [
      SchoolField(
        'mode',
        'Lịch ngày',
        kind: FieldKind.choice,
        options: {
          'OPEN': 'Có tổ chức ăn',
          'CLOSED': 'Ngày nghỉ',
          'DEFAULT': 'Trở về lịch tuần',
        },
        initial: 'OPEN',
      ),
      SchoolField(
        'mealTypes',
        'Tên bữa ăn (mỗi dòng một bữa)',
        kind: FieldKind.lines,
        required: false,
        initial: ['Bữa trưa'],
      ),
      reasonField,
    ],
    bound: ['expectedRevision'],
  ),
  SchoolOperation.previewSessions: const SchoolForm(
    SchoolOperation.previewSessions,
    'Xem trước tạo phiên',
    [
      SchoolField('from', 'Từ ngày', kind: FieldKind.date),
      SchoolField('to', 'Đến ngày', kind: FieldKind.date),
    ],
    bound: ['expectedRevision'],
    help: 'Xem các phiên sẽ tạo trước khi xác nhận. Tạo phiên chưa chốt suất.',
  ),
  SchoolOperation.generateSessions: const SchoolForm(
    SchoolOperation.generateSessions,
    'Xác nhận tạo phiên',
    [],
    bound: ['from', 'to', 'expectedRevision', 'previewToken'],
    confirm: true,
    help:
        'Áp dụng đúng kết quả xem trước. Lịch thay đổi sẽ yêu cầu xem trước lại.',
  ),
};

/// Builds DTO fields only; never sends arbitrary record or hidden response fields.
Map<String, Object?> formInput(
  SchoolForm form,
  Map<String, Object?> values,
  Map<String, Object?> context,
) {
  final input = <String, Object?>{
    for (final key in form.bound) key: context[key],
  };
  for (final field in form.fields) {
    input[field.key] = values[field.key];
  }
  return prepareSchoolInput(form.operation, input);
}

String? validateForm(
  SchoolForm form,
  Map<String, Object?> input,
  Map<String, Object?> context,
) {
  for (final field in form.fields) {
    final item = input[field.key];
    if (field.required &&
        (item == null || item == '' || item is List && item.isEmpty)) {
      return 'Vui lòng nhập ${field.label.toLowerCase()}.';
    }
    if (field.kind == FieldKind.integer &&
        item != null &&
        (item is! int || item < 1)) {
      return '${field.label} phải là số nguyên dương.';
    }
  }
  return validateSchoolCommand(form.operation, input, context, form.bound);
}

import 'school_models.dart';

// Pure business command normalization; no Flutter, JSON or transport imports.
Map<String, Object?> prepareSchoolInput(
  SchoolOperation operation,
  Map<String, Object?> fields,
) {
  final input = Map<String, Object?>.from(fields);
  if (operation == SchoolOperation.editStudent) input['updateProfile'] = true;
  if (operation == SchoolOperation.createParentLink) input.remove('schoolYear');
  if (operation == SchoolOperation.saveYear) input.remove('code');
  if (operation == SchoolOperation.createUser ||
      operation == SchoolOperation.updateUser) {
    final roles = (input['roles'] as List<Object?>?) ?? [];
    if (!roles.contains('TEACHER')) input['classIds'] = <String>[];
    if (!roles.contains('PARENT')) input['studentIds'] = <String>[];
  }
  if (operation == SchoolOperation.schedule) {
    input['weekdays'] = (input['weekdays'] as List<Object?>)
        .map((x) => int.parse(x.toString()))
        .toList();
  }
  if (operation == SchoolOperation.calendarDay && input['mode'] != 'OPEN') {
    input['mealTypes'] = <String>[];
  }
  if (operation == SchoolOperation.reviewAmendment ||
      operation == SchoolOperation.reviewParentLink ||
      operation == SchoolOperation.bulkReviewParentLinks) {
    input['approve'] = input['approve'] == 'true';
  }
  if (operation == SchoolOperation.requestAmendment) {
    final ids = (input['studentIds'] as List<Object?>?) ?? [];
    if (ids.isNotEmpty) input['quantity'] = ids.length;
  }
  return input;
}

String? validateSchoolCommand(
  SchoolOperation operation,
  Map<String, Object?> input,
  Map<String, Object?> context,
  List<String> requiredBound,
) {
  String value(String key) => input[key]?.toString() ?? '';
  if (operation == SchoolOperation.bulkReviewParentLinks) {
    final selection = {...context, ...input};
    final items = selection['items'];
    if (((selection['classId']?.toString() ?? '').isEmpty &&
            (selection['schoolYear']?.toString() ?? '').isEmpty) ||
        items is! List ||
        items.isEmpty ||
        items.length > 100) {
      return 'Chọn năm học hoặc lớp và từ 1 đến 100 yêu cầu.';
    }
  }
  if (operation == SchoolOperation.requestAmendment &&
      ((input['studentIds'] as List<Object?>).length > 200 ||
          (input['quantity'] is int && (input['quantity'] as int) > 200))) {
    return 'Một phiếu điều chỉnh tối đa 200 trẻ hoặc 200 suất.';
  }
  if (operation == SchoolOperation.changeEnrollment &&
      context['earliestChangeDate'] is String &&
      value(
            'effectiveDate',
          ).compareTo(context['earliestChangeDate'] as String) <
          0) {
    return 'Ngày hiệu lực phải từ ${context['earliestChangeDate']}.';
  }
  for (final pair in [
    ('startDate', 'endDate'),
    ('fromDate', 'toDate'),
    ('from', 'to'),
  ]) {
    if (value(pair.$1).isNotEmpty &&
        value(pair.$2).isNotEmpty &&
        value(pair.$2).compareTo(value(pair.$1)) < 0) {
      return 'Ngày kết thúc phải từ ngày bắt đầu trở đi.';
    }
  }
  if (operation == SchoolOperation.saveYear &&
      !RegExp(r'^\d{4}-\d{4}$').hasMatch(value('code'))) {
    return 'Niên khóa cần dạng 2026-2027.';
  }
  if (operation == SchoolOperation.changePassword) {
    final password = value('newPassword');
    if (password.length < 12 ||
        !RegExp(r'\d').hasMatch(password) ||
        !RegExp(r'[^a-zA-Z0-9]').hasMatch(password)) {
      return 'Mật khẩu cần ít nhất 12 ký tự, có chữ số và ký tự đặc biệt.';
    }
    if (password == value('currentPassword')) {
      return 'Mật khẩu mới phải khác mật khẩu hiện tại.';
    }
  }
  if (operation == SchoolOperation.createUser ||
      operation == SchoolOperation.updateUser) {
    if (value('email').isEmpty && value('phoneNumber').isEmpty) {
      return 'Nhập email hoặc số điện thoại.';
    }
    if ((input['roles'] as List<Object?>).isEmpty &&
        value('inspectorAccessUntil').isEmpty) {
      return 'Chọn vai trò hoặc thời hạn thanh tra.';
    }
  }
  if (operation == SchoolOperation.requestAmendment &&
      (input['studentIds'] as List<Object?>).isEmpty &&
      input['quantity'] == null) {
    return 'Chọn trẻ hoặc nhập số suất riêng cho bếp.';
  }
  if (operation == SchoolOperation.reportAbsence &&
      value('fromDate').compareTo(schoolToday()) < 0) {
    return 'Ngày bắt đầu phải từ hôm nay.';
  }
  if ((operation == SchoolOperation.createStudent ||
          operation == SchoolOperation.editStudent) &&
      value('dateOfBirth').isNotEmpty &&
      value('dateOfBirth').compareTo(schoolToday()) > 0) {
    return 'Ngày sinh không được ở tương lai.';
  }
  if (operation == SchoolOperation.createStudent &&
      value('startDate').compareTo(schoolToday()) < 0) {
    return 'Ngày bắt đầu học phải từ hôm nay.';
  }
  for (final key in [
    'revision',
    'expectedRevision',
    'baseSettlementId',
    'previewToken',
  ]) {
    if (requiredBound.contains(key) && context[key] == null) {
      return 'Dữ liệu chưa đủ hoặc vừa thay đổi. Đóng form và tải lại.';
    }
  }
  return null;
}

String? validateAbsenceYear(Map<String, Object?> values, SchoolRecord? child) {
  if (child == null ||
      child.text('yearStartDate').isEmpty ||
      child.text('yearEndDate').isEmpty) {
    return 'Trẻ chưa có mốc năm học. Liên hệ nhà trường.';
  }
  if (values['fromDate'].toString().compareTo(child.text('yearStartDate')) <
          0 ||
      values['toDate'].toString().compareTo(child.text('yearEndDate')) > 0) {
    return 'Khoảng ngày phải nằm trong năm học của trẻ.';
  }
  return null;
}

String absencePeriodEnd(String from, String period, String yearEnd) {
  final date = DateTime.tryParse(from);
  if (date == null) return from;
  if (period == 'year') return yearEnd;
  final end = period == 'week'
      ? date.add(const Duration(days: 6))
      : DateTime(
          date.year,
          date.month + 1,
          date.day.clamp(1, DateTime(date.year, date.month + 2, 0).day),
        ).subtract(const Duration(days: 1));
  final formatted = end.toIso8601String().substring(0, 10);
  return formatted.compareTo(yearEnd) > 0 ? yearEnd : formatted;
}

enum SchoolOperation {
  studentImportHistory,
  studentImportBatch,
  previewStudentImport,
  confirmStudentImport,
  parentLinkClasses,
  reviewLinkClasses,
  bulkReviewParentLinks,
  revokeParentLink,
  parentLinks,
  createParentLink,
  cancelParentLink,
  reviewableParentLinks,
  reviewParentLink,
  users,
  scopes,
  createUser,
  updateUser,
  resetPassword,
  changePassword,
  classes,
  assignedClasses,
  classStudents,
  createClass,
  editClass,
  students,
  scopedStudents,
  createStudent,
  editStudent,
  enrollments,
  changeEnrollment,
  linkParent,
  notificationSettings,
  years,
  yearConfiguration,
  nextYear,
  saveYear,
  children,
  absences,
  reportAbsence,
  replaceAbsence,
  cancelAbsence,
  workflowDays,
  createMealDay,
  portions,
  settle,
  decisions,
  exceptionHistory,
  recordException,
  amendments,
  requestAmendment,
  amendmentDetail,
  reviewAmendment,
  calendar,
  schedule,
  calendarDay,
  previewSessions,
  generateSessions,
  calendarHistory,
  meals,
  mealDetail,
}

/// Immutable structured business response. JSON decoding belongs to data.
class SchoolRecord {
  SchoolRecord(Map<String, Object?> fields)
    : fields = Map.unmodifiable(
        fields.map((key, value) => MapEntry(key, _freeze(value))),
      );
  final Map<String, Object?> fields;
  static Object? _freeze(Object? value) {
    if (value is Map<String, Object?>) return SchoolRecord(value);
    if (value is List) {
      return List<Object?>.unmodifiable(
        value.map((Object? item) => _freeze(item)),
      );
    }
    if (value == null ||
        value is String ||
        value is num ||
        value is bool ||
        value is SchoolRecord) {
      return value;
    }
    throw const FormatException('Invalid business data');
  }

  Object? operator [](String key) => fields[key];
  String text(String key) => fields[key]?.toString() ?? '';
  int number(String key, [int fallback = 0]) =>
      fields[key] is num ? (fields[key] as num).toInt() : fallback;
  bool flag(String key) => fields[key] == true;
  SchoolRecord? record(String key) =>
      fields[key] is SchoolRecord ? fields[key] as SchoolRecord : null;
  List<Object?> values(String key) =>
      fields[key] is List<Object?> ? fields[key] as List<Object?> : const [];
  List<SchoolRecord> records(String key) =>
      values(key).whereType<SchoolRecord>().toList(growable: false);
  String get title {
    for (final key in [
      'fullName',
      'studentName',
      'className',
      'name',
      'code',
      'date',
      'mealType',
    ]) {
      if (text(key).isNotEmpty) return text(key);
    }
    return 'Thông tin';
  }
}

class SchoolResult {
  SchoolResult({required this.summary, required List<SchoolRecord> records})
    : records = List.unmodifiable(records);
  final SchoolRecord summary;
  final List<SchoolRecord> records;
  int get total => summary.number('total', records.length);
  int get pageSize => summary.number('pageSize', 25);
}

class SchoolFailure implements Exception {
  const SchoolFailure(this.message, {this.status});
  final String message;
  final int? status;
}

abstract interface class SchoolRepository {
  Future<SchoolResult> execute(
    SchoolOperation operation, {
    Map<String, Object?> context = const {},
    Map<String, Object?> input = const {},
  });
  void dispose();
}

const adminRoles = ['ADMIN'];
const staffRoles = ['ADMIN', 'TEACHER'];
const portionRoles = ['ADMIN', 'TEACHER', 'KITCHEN_STAFF'];
const kitchenRoles = ['ADMIN', 'KITCHEN_STAFF'];

List<String> rolesFor(SchoolOperation op) => switch (op) {
  SchoolOperation.changePassword || SchoolOperation.years => const [],
  SchoolOperation.parentLinkClasses ||
  SchoolOperation.parentLinks ||
  SchoolOperation.createParentLink ||
  SchoolOperation.cancelParentLink ||
  SchoolOperation.children ||
  SchoolOperation.absences ||
  SchoolOperation.reportAbsence ||
  SchoolOperation.replaceAbsence ||
  SchoolOperation.cancelAbsence => const ['PARENT'],
  SchoolOperation.assignedClasses ||
  SchoolOperation.workflowDays ||
  SchoolOperation.portions ||
  SchoolOperation.amendments ||
  SchoolOperation.amendmentDetail => portionRoles,
  SchoolOperation.reviewLinkClasses ||
  SchoolOperation.bulkReviewParentLinks ||
  SchoolOperation.revokeParentLink ||
  SchoolOperation.reviewableParentLinks ||
  SchoolOperation.reviewParentLink ||
  SchoolOperation.scopedStudents ||
  SchoolOperation.createStudent ||
  SchoolOperation.classStudents ||
  SchoolOperation.linkParent ||
  SchoolOperation.notificationSettings ||
  SchoolOperation.decisions ||
  SchoolOperation.exceptionHistory ||
  SchoolOperation.recordException ||
  SchoolOperation.requestAmendment => staffRoles,
  SchoolOperation.meals || SchoolOperation.mealDetail => kitchenRoles,
  _ => adminRoles,
};

bool canExecute(SchoolOperation op, List<String> roles) =>
    rolesFor(op).isEmpty || rolesFor(op).any(roles.contains);

String schoolToday() => DateTime.now()
    .toUtc()
    .add(const Duration(hours: 7))
    .toIso8601String()
    .substring(0, 10);

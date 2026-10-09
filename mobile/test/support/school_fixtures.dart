import 'package:mealtrace_mobile/features/school/domain/school_models.dart';

const schoolId = '11111111-1111-4111-8111-111111111111';
const otherId = '22222222-2222-4222-8222-222222222222';
Map<String, Object?> validSchoolValues() => {
  'id': schoolId,
  'dayId': schoolId,
  'studentId': schoolId,
  'requestId': schoolId,
  'classId': schoolId,
  'baseSettlementId': schoolId,
  'expectedEventId': null,
  'fullName': 'Nguyễn An',
  'name': 'Lớp M1',
  'phoneNumber': '0349079940',
  'email': 'an@example.test',
  'roles': <String>['TEACHER', 'PARENT'],
  'classIds': <String>[schoolId],
  'studentIds': <String>[schoolId],
  'status': 'ACTIVE',
  'inspectorAccessUntil': null,
  'reason': 'Đã đối chiếu hồ sơ',
  'revision': 3,
  'expectedRevision': 3,
  'code': '2026-2027',
  'schoolYear': '2026-2027',
  'sourceYearCode': null,
  'startDate': schoolToday(),
  'endDate': '2099-05-31',
  'effectiveDate': schoolToday(),
  'fromDate': schoolToday(),
  'toDate': schoolToday(),
  'from': schoolToday(),
  'to': schoolToday(),
  'date': schoolToday(),
  'mealType': 'Bữa trưa',
  'studentCode': null,
  'sendRegistrationNotification': false,
  'willEat': true,
  'quantity': null,
  'approve': 'true',
  'action': 'EAT',
  'weekdays': <String>['1', '2', '3', '4', '5'],
  'mealTypes': <String>['Bữa trưa'],
  'mode': 'OPEN',
  'previewToken': 'preview-test',
  'currentPassword': 'OldPassword!123',
  'newPassword': 'NewPassword!123',
};

SchoolResult schoolFixture(SchoolOperation operation) {
  final record = {
    ...validSchoolValues(),
    'studentCode': 'HS-001',
    'studentName': 'Nguyễn An',
    'className': 'M1',
    'isActive': true,
    'isSettled': true,
    'isCancelled': false,
    'willEat': false,
    'parents': [
      {'id': otherId, 'fullName': 'Nguyễn Bình', 'phoneNumber': '0349079940'},
    ],
    'studentCount': 30,
    'yearStartDate': '2026-01-01',
    'yearEndDate': '2099-05-31',
    'count': 29,
    'version': 1,
    'status': operation == SchoolOperation.amendments ? 'PENDING' : 'ACTIVE',
    'latestEventId': null,
    'cutoffAt': '2099-10-08T07:30:00+07:00',
    'isOpen': true,
    'locked': false,
  };
  final snapshot = {
    'id': schoolId,
    'count': 29,
    'version': 1,
    'students': [
      {'studentId': schoolId, 'studentName': 'Nguyễn An'},
    ],
  };
  final summary = SchoolRecord({
    ...validSchoolValues(),
    'total': 1,
    'pageSize': 25,
    'canEdit': true,
    'canRequest': true,
    'cutoffAt': '2099-10-08T07:30:00+07:00',
    'isSettled': true,
    'isCancelled': false,
    'current': snapshot,
    'original': snapshot,
    'before': snapshot,
    'after': snapshot,
    'classes': [record],
    'candidates': [record],
    'candidateTotal': 1,
    'students': [record],
    'classTotal': 1,
    'studentTotal': 1,
    'enabled': true,
    'channel': 'WhatsApp',
    'templateOnly': false,
    'createCount': 1,
    'restoreCount': 0,
    'existingCount': 0,
  });
  final keys = switch (operation) {
    SchoolOperation.students || SchoolOperation.scopedStudents => [
      'id',
      'fullName',
      'studentCode',
      'revision',
      'isActive',
      'className',
      'parents',
    ],
    SchoolOperation.classes || SchoolOperation.assignedClasses => [
      'id',
      'name',
      'schoolYear',
      'studentCount',
    ],
    SchoolOperation.yearConfiguration ||
    SchoolOperation.years => ['code', 'startDate', 'endDate'],
    SchoolOperation.users => [
      'id',
      'fullName',
      'email',
      'phoneNumber',
      'roles',
      'status',
      'classIds',
      'studentIds',
      'inspectorAccessUntil',
    ],
    SchoolOperation.workflowDays => [
      'id',
      'date',
      'mealType',
      'schoolYear',
      'cutoffAt',
      'isSettled',
      'isCancelled',
    ],
    SchoolOperation.absences => [
      'id',
      'studentId',
      'studentName',
      'fromDate',
      'toDate',
      'reason',
      'cancelledAt',
      'schoolYear',
    ],
    SchoolOperation.children => [
      'studentId',
      'fullName',
      'className',
      'schoolYear',
      'yearStartDate',
      'yearEndDate',
    ],
    SchoolOperation.calendar => ['date', 'isOpen', 'mealTypes', 'locked'],
    _ => record.keys.toList(),
  };
  return SchoolResult(
    summary: summary,
    records: [
      SchoolRecord({for (final key in keys) key: record[key]}),
    ],
  );
}

class FakeSchoolRepository implements SchoolRepository {
  final calls =
      <
        ({
          SchoolOperation operation,
          Map<String, Object?> context,
          Map<String, Object?> input,
        })
      >[];
  Future<SchoolResult> Function(
    SchoolOperation,
    Map<String, Object?>,
    Map<String, Object?>,
  )?
  handler;
  @override
  Future<SchoolResult> execute(
    SchoolOperation operation, {
    Map<String, Object?> context = const {},
    Map<String, Object?> input = const {},
  }) async {
    calls.add((operation: operation, context: context, input: input));
    return handler != null
        ? handler!(operation, context, input)
        : schoolFixture(operation);
  }

  @override
  void dispose() {}
}

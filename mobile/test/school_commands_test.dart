import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/domain/school_commands.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';

void main() {
  test('Absence shortcuts stay within the academic year', () {
    expect(absencePeriodEnd('2026-10-08', 'week', '2027-05-31'), '2026-10-14');
    expect(absencePeriodEnd('2026-05-29', 'week', '2026-05-31'), '2026-05-31');
    expect(absencePeriodEnd('2028-01-31', 'month', '2028-05-31'), '2028-02-28');
    expect(absencePeriodEnd('2027-01-31', 'month', '2027-05-31'), '2027-02-27');
    expect(absencePeriodEnd('2026-10-08', 'year', '2027-05-31'), '2027-05-31');
  });
  test(
    'Parent absence requires known year bounds and rejects dates outside them',
    () {
      final child = SchoolRecord({
        'yearStartDate': '2026-09-01',
        'yearEndDate': '2027-05-31',
      });
      expect(
        validateAbsenceYear({
          'fromDate': '2026-10-08',
          'toDate': '2027-05-31',
        }, child),
        isNull,
      );
      expect(
        validateAbsenceYear({
          'fromDate': '2026-08-31',
          'toDate': '2026-10-08',
        }, child),
        isNotNull,
      );
      expect(
        validateAbsenceYear({
          'fromDate': '2026-10-08',
          'toDate': '2027-06-01',
        }, child),
        isNotNull,
      );
      expect(validateAbsenceYear({}, null), isNotNull);
    },
  );
  for (final payload in [
    '{}',
    '[]',
    '{"items":[],"total":-1}',
    '{"items":"wrong","total":0}',
  ]) {
    test('Student search rejects malformed response shape $payload', () async {
      final repository = SchoolRepositoryImpl(
        MockClient((_) async => http.Response(payload, 200)),
        'https://example.test/api',
        () => 'test-session',
      );
      addTearDown(repository.dispose);
      await expectLater(
        repository.execute(SchoolOperation.students),
        throwsA(isA<SchoolFailure>()),
      );
    });
  }
}

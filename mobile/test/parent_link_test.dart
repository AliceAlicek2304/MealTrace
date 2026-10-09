import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';

void main() {
  test(
    'Parent link form sends identity evidence as strings without parent or child database ids',
    () {
      final form = schoolForms[SchoolOperation.createParentLink]!;
      final values = {
        'classId': '11111111-1111-4111-8111-111111111111',
        'schoolYear': '2026-2027',
        'studentName': 'Nguyễn An',
        'relationship': 'MOTHER',
        'note': 'Đối chiếu hồ sơ',
        'parentId': 'forged',
        'status': 'APPROVED',
      };
      final input = formInput(form, values, {});
      expect(input, {
        'classId': '11111111-1111-4111-8111-111111111111',
        'studentName': 'Nguyễn An',
        'relationship': 'MOTHER',
        'note': 'Đối chiếu hồ sơ',
      });
      expect(validateForm(form, values, {}), isNull);
      expect(validateForm(form, {...values, 'classId': ''}, {}), isNotNull);
      expect(canExecute(SchoolOperation.createParentLink, ['PARENT']), isTrue);
      expect(canExecute(SchoolOperation.reviewParentLink, ['PARENT']), isFalse);
      expect(canExecute(SchoolOperation.reviewParentLink, ['TEACHER']), isTrue);
      expect(
        canExecute(SchoolOperation.reviewableParentLinks, ['KITCHEN_STAFF']),
        isFalse,
      );
    },
  );
  test(
    'Review sends a boolean decision and the captured revision to the correct request',
    () async {
      http.Request? sent;
      final repo = SchoolRepositoryImpl(
        MockClient((request) async {
          sent = request;
          return http.Response('', 204);
        }),
        'https://example.test/api',
        () => 'session',
      );
      addTearDown(repo.dispose);
      final form = schoolForms[SchoolOperation.reviewParentLink]!;
      final context = {'id': 'request-1', 'revision': 2};
      final input = formInput(form, {
        'approve': 'true',
        'reason': 'Đã đối chiếu',
      }, context);
      expect(input, {'approve': true, 'reason': 'Đã đối chiếu', 'revision': 2});
      expect(validateForm(form, {...input, 'reason': ''}, context), isNotNull);
      await repo.execute(
        SchoolOperation.reviewParentLink,
        context: context,
        input: input,
      );
      expect(sent!.url.path, '/api/student-link-requests/request-1/review');
      expect(jsonDecode(sent!.body), input);
    },
  );
}

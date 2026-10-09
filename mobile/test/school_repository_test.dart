import 'dart:async';
import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';
import 'support/school_fixtures.dart';

void main() {
  for (final operation in SchoolOperation.values) {
    test(
      '$operation sends authenticated requests with structured results',
      () async {
        http.Request? sent;
        final repo = SchoolRepositoryImpl(
          MockClient((request) async {
            sent = request;
            return http.Response(
              jsonEncode(
                arrayOperations.contains(operation)
                    ? [
                        {'fullName': 'Nguyễn An'},
                      ]
                    : {
                        'items': [
                          {'fullName': 'Nguyễn An'},
                        ],
                        'total': 1,
                        'rows': [
                          {'row': 4, 'fullName': 'Nguyễn An', 'error': null},
                        ],
                        'canImport': true,
                        'classes': [
                          {'className': 'M1'},
                        ],
                        'days': [
                          {'date': schoolToday()},
                        ],
                      },
              ),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
          'https://example.test/api',
          () => 'session-test',
        );
        addTearDown(repo.dispose);
        final result = await repo.execute(
          operation,
          context: validSchoolValues(),
          input:
              operation == SchoolOperation.previewStudentImport ||
                  operation == SchoolOperation.confirmStudentImport
              ? {
                  'fileName': 'test.xlsx',
                  'fileBytes': <int>[1, 2, 3],
                  'classId': schoolId,
                  'startDate': schoolToday(),
                }
              : const {'search': 'Nguyễn & An', 'page': 2},
        );
        expect(sent!.headers['Authorization'], 'Bearer session-test');
        expect(sent!.url.host, 'example.test');
        expect(sent!.url.path, startsWith('/api/'));
        expect(sent!.url.toString(), isNot(contains('{')));
        expect(result.records.length, 1);
        expect(sent!.url.queryParameters, isNot(contains('phoneNumber')));
        if (schoolRoutes[operation]!.method == 'GET') {
          expect(sent!.body, isEmpty);
        }
      },
    );
  }
  for (final status in [400, 401, 403, 409, 429, 500]) {
    test(
      'HTTP $status gives an actionable failure without retrying writes',
      () async {
        var count = 0;
        final repo = SchoolRepositoryImpl(
          MockClient((_) async {
            count++;
            return http.Response(
              '{"message":"Kiểm tra ngày hiệu lực"}',
              status,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
          'https://example.test/api',
          () => 'session-test',
        );
        addTearDown(repo.dispose);
        await expectLater(
          repo.execute(
            SchoolOperation.createClass,
            input: {'name': 'M1', 'schoolYear': '2026-2027'},
          ),
          throwsA(
            isA<SchoolFailure>().having((x) => x.status, 'status', status),
          ),
        );
        expect(count, 1);
      },
    );
  }
  test(
    'Invalid JSON is rejected and sensitive response contents are not exposed',
    () async {
      final repo = SchoolRepositoryImpl(
        MockClient((_) async => http.Response('not-json private-token', 200)),
        'https://example.test/api',
        () => 'session-test',
      );
      addTearDown(repo.dispose);
      await expectLater(
        repo.execute(SchoolOperation.users),
        throwsA(
          isA<SchoolFailure>().having(
            (x) => x.message,
            'message',
            isNot(contains('private-token')),
          ),
        ),
      );
    },
  );
  test('Late business response cannot invalidate a newer session', () async {
    var token = 'first';
    final response = Completer<http.Response>();
    final repo = SchoolRepositoryImpl(
      MockClient((_) => response.future),
      'https://example.test/api',
      () => token,
    );
    addTearDown(repo.dispose);
    final pending = repo.execute(SchoolOperation.users);
    token = 'second';
    response.complete(http.Response('{"items":[]}', 200));
    await expectLater(
      pending,
      throwsA(isA<SchoolFailure>().having((x) => x.status, 'status', isNull)),
    );
  });
  test(
    'Changed filters ignore out-of-order results and clear stale records',
    () async {
      final repo = FakeSchoolRepository();
      final first = Completer<SchoolResult>(),
          second = Completer<SchoolResult>();
      repo.handler = (_, _, input) =>
          input['page'] == 1 ? first.future : second.future;
      final controller = SchoolController(repo, ['ADMIN'], () async {});
      addTearDown(controller.dispose);
      final old = controller.load(SchoolOperation.users, {}, {'page': 1});
      final recent = controller.load(SchoolOperation.users, {}, {'page': 2});
      second.complete(
        SchoolResult(summary: SchoolRecord({'total': 0}), records: []),
      );
      await recent;
      first.complete(schoolFixture(SchoolOperation.users));
      await old;
      expect(controller.result!.records, isEmpty);
    },
  );
  test('Role boundaries reject forbidden actions before any request', () async {
    final repo = FakeSchoolRepository();
    final controller = SchoolController(repo, ['PARENT'], () async {});
    addTearDown(controller.dispose);
    await expectLater(
      controller.execute(SchoolOperation.settle),
      throwsA(isA<SchoolFailure>()),
    );
    expect(repo.calls, isEmpty);
    await controller.execute(SchoolOperation.children);
    expect(repo.calls.single.operation, SchoolOperation.children);
  });
  test(
    'Unauthorized response clears session; forbidden response preserves it',
    () async {
      var invalidations = 0;
      final repo = FakeSchoolRepository();
      final controller = SchoolController(repo, ['ADMIN'], () async {
        invalidations++;
      });
      addTearDown(controller.dispose);
      for (final status in [403, 401]) {
        repo.handler = (_, _, _) async =>
            throw SchoolFailure('failure', status: status);
        await expectLater(
          controller.execute(SchoolOperation.users),
          throwsA(isA<SchoolFailure>()),
        );
      }
      expect(invalidations, 1);
    },
  );
  for (final form in schoolForms.values) {
    test(
      '${form.operation} builds only its DTO fields and preserves concurrency',
      () {
        final values = validSchoolValues();
        expect(validateForm(form, values, values), isNull);
        final input = formInput(form, values, values);
        expect(
          input.keys.toSet(),
          {
            ...form.bound,
            ...form.fields.map((field) => field.key),
            if (form.operation == SchoolOperation.editStudent) 'updateProfile',
          }.difference(
            form.operation == SchoolOperation.saveYear
                ? {'code'}
                : form.operation == SchoolOperation.createParentLink
                ? {'schoolYear'}
                : {},
          ),
        );
        if (form.bound.contains('expectedRevision')) {
          expect(input['expectedRevision'], 3);
        }
        if (form.bound.contains('baseSettlementId')) {
          expect(input['baseSettlementId'], schoolId);
        }
      },
    );
  }
  test(
    'Calendar generation preserves reviewed dates, version and preview token',
    () {
      final input = formInput(
        schoolForms[SchoolOperation.generateSessions]!,
        {},
        validSchoolValues(),
      );
      expect(input, {
        'from': schoolToday(),
        'to': schoolToday(),
        'expectedRevision': 3,
        'previewToken': 'preview-test',
      });
    },
  );
  test(
    'Account roles remove unrelated scope and calendar DTO uses numeric weekdays',
    () {
      final values = validSchoolValues()..['roles'] = <String>['ADMIN'];
      final input = formInput(
        schoolForms[SchoolOperation.createUser]!,
        values,
        {},
      );
      expect(input['classIds'], isEmpty);
      expect(input['studentIds'], isEmpty);
      expect(
        formInput(
          schoolForms[SchoolOperation.schedule]!,
          validSchoolValues(),
          validSchoolValues(),
        )['weekdays'],
        [1, 2, 3, 4, 5],
      );
    },
  );
  test(
    'Missing version, reversed dates, weak password and empty amendment fail validation',
    () {
      expect(
        validateForm(
          schoolForms[SchoolOperation.editStudent]!,
          validSchoolValues(),
          {},
        ),
        isNotNull,
      );
      expect(
        validateForm(schoolForms[SchoolOperation.reportAbsence]!, {
          ...validSchoolValues(),
          'toDate': '2000-01-01',
        }, {}),
        isNotNull,
      );
      expect(
        validateForm(schoolForms[SchoolOperation.changePassword]!, {
          ...validSchoolValues(),
          'newPassword': 'short',
        }, {}),
        isNotNull,
      );
      expect(
        validateForm(schoolForms[SchoolOperation.requestAmendment]!, {
          ...validSchoolValues(),
          'studentIds': <String>[],
          'quantity': null,
        }, validSchoolValues()),
        isNotNull,
      );
    },
  );
}

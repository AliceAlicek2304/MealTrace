import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/app/theme.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/audit_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'support/school_fixtures.dart';

void main() {
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'Audit shows compact rows and fetches authoritative details at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 850);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final repo = FakeSchoolRepository();
        repo.handler = (op, context, input) async => SchoolResult(
          summary: SchoolRecord(
            op == SchoolOperation.auditDetail
                ? {
                    'reason': 'Đối chiếu hồ sơ',
                    'after': {'name': 'Tên đã cập nhật'},
                  }
                : {'total': 1},
          ),
          records: op == SchoolOperation.auditLogs
              ? [
                  SchoolRecord({
                    'id': 'student:example',
                    'action': 'Cập nhật trẻ',
                    'entityType': 'Student',
                    'occurredAt': '2026-11-14T01:00:00Z',
                  }),
                ]
              : [],
        );
        final controller = SchoolController(repo, ['ADMIN'], () async {});
        addTearDown(controller.dispose);
        await tester.pumpWidget(
          MaterialApp(
            theme: mealTraceTheme,
            home: AuditPage(controller: controller),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('Student · 14/11/2026 08:00'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.tap(find.text('Cập nhật trẻ'));
        await tester.pumpAndSettle();
        expect(repo.calls.last.operation, SchoolOperation.auditDetail);
        expect(repo.calls.last.context['id'], 'student:example');
        await tester.tap(find.text('Lý do'));
        await tester.pumpAndSettle();
        expect(find.text('Đối chiếu hồ sơ'), findsOneWidget);
        expect(
          repo.calls.where(
            (x) => x.operation == SchoolOperation.sendTestNotification,
          ),
          isEmpty,
        );
        expect(tester.takeException(), isNull);
      },
    );
  }
  test('Audit remains restricted to Admin before any HTTP request', () async {
    for (final role in ['PARENT', 'TEACHER', 'KITCHEN_STAFF']) {
      final repo = FakeSchoolRepository();
      final controller = SchoolController(repo, [role], () async {});
      for (final op in [
        SchoolOperation.auditLogs,
        SchoolOperation.auditDetail,
        SchoolOperation.auditEntity,
        SchoolOperation.auditExport,
      ]) {
        await expectLater(
          controller.execute(op),
          throwsA(isA<SchoolFailure>().having((e) => e.status, 'status', 403)),
        );
      }
      expect(repo.calls, isEmpty);
      controller.dispose();
    }
  });
}

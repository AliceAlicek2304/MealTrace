import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/app/theme.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/parent_link_review_page.dart';
import 'support/school_fixtures.dart';

void main() {
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'Teacher reviews checked class requests at $width without overflow',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final repo = FakeSchoolRepository();
        repo.handler = (op, context, input) async {
          if (op == SchoolOperation.reviewLinkClasses) {
            return SchoolResult(
              summary: SchoolRecord({}),
              records: [
                SchoolRecord({
                  'id': schoolId,
                  'name': 'A3',
                  'schoolYear': '2026-2027',
                }),
              ],
            );
          }
          if (op == SchoolOperation.reviewableParentLinks) {
            return SchoolResult(
              summary: SchoolRecord({'total': 2}),
              records: [
                for (final (id, name) in [
                  (schoolId, 'Nguyễn An'),
                  (otherId, 'Trần Bình'),
                ])
                  SchoolRecord({
                    'id': id,
                    'studentName': name,
                    'parentName': 'Mẹ $name',
                    'parentPhone': '0901234567',
                    'relationship': 'MOTHER',
                    'status': 'PENDING',
                    'revision': 2,
                    'className': 'A3',
                  }),
              ],
            );
          }
          return SchoolResult(summary: SchoolRecord({}), records: []);
        };
        await tester.pumpWidget(
          MaterialApp(
            theme: mealTraceTheme,
            home: ParentLinkReviewPage(
              repository: repo,
              roles: ['TEACHER'],
              onUnauthorized: () async {},
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Lớp phụ trách'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('A3 · 2026-2027').last);
        await tester.pumpAndSettle();
        expect(repo.calls.last.input['classId'], schoolId);
        await tester.tap(find.text('Chọn yêu cầu trên trang'));
        await tester.pumpAndSettle();
        final first = find.byType(CheckboxListTile).first;
        await tester.ensureVisible(first);
        await tester.tap(first);
        await tester.pumpAndSettle();
        final action = find.widgetWithText(
          FilledButton,
          'Xử lý 1 yêu cầu đã chọn',
        );
        await tester.ensureVisible(action);
        await tester.tap(action);
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byType(TextFormField).first,
          'Đã rà hồ sơ lớp',
        );
        final submit = find.widgetWithText(
          FilledButton,
          'Xử lý danh sách đã chọn',
        );
        await tester.ensureVisible(submit);
        await tester.tap(submit);
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'Xác nhận'));
        await tester.pumpAndSettle();
        final request = repo.calls.singleWhere(
          (x) => x.operation == SchoolOperation.bulkReviewParentLinks,
        );
        expect(request.input, {
          'classId': schoolId,
          'schoolYear': null,
          'items': [
            {'id': otherId, 'revision': 2},
          ],
          'approve': false,
          'reason': 'Đã rà hồ sơ lớp',
        });
        expect(tester.takeException(), isNull);
      },
    );
  }
}

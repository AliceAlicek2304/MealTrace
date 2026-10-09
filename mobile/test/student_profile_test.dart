import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_form_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_widgets.dart';
import 'support/school_fixtures.dart';

void main() {
  for (final selection in {'Nữ': 'FEMALE', 'Chưa có': null}.entries) {
    testWidgets(
      'Editing a profile clears birth, preserves revision and sets gender to ${selection.key}',
      (tester) async {
        final repo = FakeSchoolRepository();
        final controller = SchoolController(repo, ['ADMIN'], () async {});
        addTearDown(controller.dispose);
        await tester.pumpWidget(
          MaterialApp(
            home: SchoolFormPage(
              form: schoolForms[SchoolOperation.editStudent]!,
              controller: controller,
              contextValues: {
                ...validSchoolValues(),
                'dateOfBirth': '2021-03-04',
                'gender': 'MALE',
              },
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.byTooltip('Xóa ngày sinh'));
        await tester.pumpAndSettle();
        await tester.tap(find.byType(DropdownButtonFormField<String>));
        await tester.pumpAndSettle();
        await tester.tap(find.text(selection.key).last);
        await tester.pumpAndSettle();
        await tester.ensureVisible(
          find.widgetWithText(FilledButton, 'Sửa hồ sơ trẻ'),
        );
        await tester.tap(find.widgetWithText(FilledButton, 'Sửa hồ sơ trẻ'));
        await tester.pumpAndSettle();
        expect(repo.calls.single.input['dateOfBirth'], isNull);
        expect(repo.calls.single.input['gender'], selection.value);
        expect(repo.calls.single.input['revision'], 3);
        expect(repo.calls.single.input['updateProfile'], true);
      },
    );
  }

  testWidgets(
    'Notification result explains the outcome without exposing provider diagnostics',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: RecordDetails(
              record: SchoolRecord({
                'notification': {
                  'status': 'UNKNOWN',
                  'message': 'provider-internal-secret-diagnostic',
                  'providerMessageId': 'internal-id',
                },
              }),
            ),
          ),
        ),
      );
      expect(
        find.textContaining('chưa xác định được kết quả gửi tin'),
        findsOneWidget,
      );
      expect(
        find.textContaining('provider-internal-secret-diagnostic'),
        findsNothing,
      );
      expect(find.textContaining('internal-id'), findsNothing);
    },
  );
}

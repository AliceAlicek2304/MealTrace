import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/app/theme.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/kitchen_settlements_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'support/school_fixtures.dart';

void main() {
  testWidgets(
    'Admin reads the snapshot and history without reception controls',
    (tester) async {
      final repo = FakeSchoolRepository();
      repo.handler = (operation, context, input) async => SchoolResult(
        summary: SchoolRecord(
          operation == SchoolOperation.kitchenDiff
              ? {
                  'snapshotToken': 'a' * 64,
                  'appliedTotal': 30,
                  'originalTotal': 30,
                  'isAcknowledged': false,
                  'classDiffs': <Object?>[],
                }
              : {},
        ),
        records: [],
      );
      await tester.pumpWidget(
        MaterialApp(
          theme: mealTraceTheme,
          home: KitchenSettlementDetailPage(
            repository: repo,
            roles: ['ADMIN'],
            onUnauthorized: () async {},
            day: SchoolRecord({
              'mealDayId': schoolId,
              'date': '2026-10-20',
              'sessionType': 'Bữa trưa',
            }),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('30 suất'), findsOneWidget);
      expect(find.text('Lịch sử tiếp nhận · 0'), findsOneWidget);
      expect(find.byType(CheckboxListTile), findsNothing);
      expect(find.byType(TextField), findsNothing);
      expect(find.text('Xác nhận tiếp nhận'), findsNothing);
      final controller = SchoolController(repo, ['ADMIN'], () async {});
      addTearDown(controller.dispose);
      await expectLater(
        controller.execute(SchoolOperation.acknowledgeKitchen),
        throwsA(
          isA<SchoolFailure>().having((error) => error.status, 'status', 403),
        ),
      );
      expect(
        repo.calls.any(
          (call) => call.operation == SchoolOperation.acknowledgeKitchen,
        ),
        isFalse,
      );
    },
  );
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'Kitchen confirms a reviewed snapshot and sees history at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        var saved = false;
        final token = 'a' * 64;
        final repo = FakeSchoolRepository();
        repo.handler = (operation, context, input) async {
          if (operation == SchoolOperation.acknowledgeKitchen) {
            expect(context, {'id': schoolId});
            expect(input['snapshotToken'], token);
            saved = true;
          }
          if (operation == SchoolOperation.kitchenSettlements) {
            return SchoolResult(
              summary: SchoolRecord({'total': 1}),
              records: [
                SchoolRecord({
                  'mealDayId': schoolId,
                  'date': '2026-10-19',
                  'sessionType': 'Bữa trưa',
                  'appliedPortions': 32,
                  'originalPortions': 30,
                  'isAcknowledged': saved,
                }),
              ],
            );
          }
          if (operation == SchoolOperation.kitchenDiff) {
            return SchoolResult(
              summary: SchoolRecord({
                'snapshotToken': token,
                'appliedTotal': 32,
                'originalTotal': 30,
                'totalDiff': 2,
                'isAcknowledged': saved,
                'classDiffs': [
                  {
                    'classId': otherId,
                    'className': 'A3',
                    'originalCount': 30,
                    'appliedCount': 32,
                    'currentVersion': 2,
                    'kitchenAdjustment': 2,
                    'hasCompleteRoster': false,
                  },
                ],
              }),
              records: [],
            );
          }
          if (operation == SchoolOperation.kitchenHistory && saved) {
            return SchoolResult(
              summary: SchoolRecord({}),
              records: [
                SchoolRecord({
                  'portionsCount': 32,
                  'acknowledgedByName': 'Bếp A',
                  'acknowledgedAt': '2026-10-19T01:00:00Z',
                  'snapshotToken': token,
                }),
              ],
            );
          }
          return SchoolResult(summary: SchoolRecord({}), records: []);
        };
        await tester.pumpWidget(
          MaterialApp(
            theme: mealTraceTheme,
            home: KitchenSettlementsPage(
              repository: repo,
              roles: ['KITCHEN_STAFF'],
              onUnauthorized: () async {},
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Bữa trưa · 2026-10-19'));
        await tester.pumpAndSettle();
        expect(
          find.text('Chưa đủ dữ liệu lịch sử để đối chiếu trẻ'),
          findsOneWidget,
        );
        final button = find.widgetWithText(FilledButton, 'Xác nhận tiếp nhận');
        expect(tester.widget<FilledButton>(button).onPressed, isNull);
        await tester.ensureVisible(find.byType(CheckboxListTile));
        await tester.tap(find.byType(CheckboxListTile));
        await tester.pumpAndSettle();
        await tester.ensureVisible(button);
        tester.view.viewInsets = const FakeViewPadding(bottom: 300);
        addTearDown(tester.view.resetViewInsets);
        await tester.pumpAndSettle();
        expect(tester.getRect(button).bottom, lessThanOrEqualTo(600));
        await tester.tap(button);
        await tester.pumpAndSettle();
        tester.view.resetViewInsets();
        await tester.pumpAndSettle();
        expect(
          find.text('Bếp đã tiếp nhận bản suất hiện hành.'),
          findsOneWidget,
        );
        expect(
          repo.calls
              .where(
                (call) => call.operation == SchoolOperation.acknowledgeKitchen,
              )
              .length,
          1,
        );
        await Scrollable.ensureVisible(
          tester.element(find.text('Lịch sử tiếp nhận · 1')),
          alignment: .3,
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Lịch sử tiếp nhận · 1'));
        await tester.pumpAndSettle();
        expect(find.text('32 suất · Bếp A'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
  test('Kitchen endpoints are denied to teachers and parents', () {
    expect(
      canExecute(SchoolOperation.acknowledgeKitchen, [
        'ADMIN',
        'KITCHEN_STAFF',
      ]),
      isFalse,
    );
    for (final operation in [
      SchoolOperation.kitchenSettlements,
      SchoolOperation.kitchenDiff,
      SchoolOperation.kitchenHistory,
      SchoolOperation.acknowledgeKitchen,
    ]) {
      expect(
        canExecute(operation, ['ADMIN']),
        operation != SchoolOperation.acknowledgeKitchen,
      );
      expect(canExecute(operation, ['KITCHEN_STAFF']), isTrue);
      expect(canExecute(operation, ['TEACHER']), isFalse);
      expect(canExecute(operation, ['PARENT']), isFalse);
    }
  });
}

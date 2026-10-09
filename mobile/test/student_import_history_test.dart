import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/student_import_history_page.dart';

class HistoryRepository implements SchoolRepository {
  final calls = <SchoolOperation>[];
  final batch = SchoolRecord({
    'id': 'batch-1',
    'className': 'A3',
    'schoolYear': '2026-2027',
    'created': 1,
    'fileName': 'Danh sách trẻ thử nghiệm.xlsx',
    'importedAt': '2026-10-09T03:00:00Z',
    'importedByName': 'Admin thử',
  });
  @override
  Future<SchoolResult> execute(
    SchoolOperation operation, {
    Map<String, Object?> context = const {},
    Map<String, Object?> input = const {},
  }) async {
    calls.add(operation);
    return switch (operation) {
      SchoolOperation.assignedClasses => SchoolResult(
        summary: SchoolRecord({}),
        records: [],
      ),
      SchoolOperation.studentImportHistory => SchoolResult(
        summary: SchoolRecord({'total': 1}),
        records: [batch],
      ),
      SchoolOperation.studentImportBatch => SchoolResult(
        summary: SchoolRecord({
          'batch': batch.fields,
          'rows': [
            {
              'row': 4,
              'fullName': 'Nguyễn An',
              'dateOfBirth': '2021-03-04',
              'gender': 'FEMALE',
            },
          ],
        }),
        records: [],
      ),
      _ => throw StateError('Unexpected operation'),
    };
  }

  @override
  void dispose() {}
}

void main() {
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'Import history at $width opens original child details without overflow or writes',
      (tester) async {
        tester.view.physicalSize = Size(width, 850);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final repo = HistoryRepository();
        await tester.pumpWidget(
          MaterialApp(
            home: StudentImportHistoryPage(
              repository: repo,
              roles: ['ADMIN'],
              onUnauthorized: () async {},
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('A3 · 1 trẻ'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Nguyễn An'));
        await tester.pumpAndSettle();
        expect(find.text('04/03/2021'), findsOneWidget);
        expect(find.text('Nữ'), findsOneWidget);
        expect(tester.takeException(), isNull);
        expect(repo.calls, [
          SchoolOperation.assignedClasses,
          SchoolOperation.studentImportHistory,
          SchoolOperation.studentImportBatch,
        ]);
      },
    );
  }
}

import 'dart:convert';
import 'dart:typed_data';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/student_import_page.dart';
import 'support/school_fixtures.dart';

class MemoryXlsx extends XFile {
  MemoryXlsx() : super('test.xlsx');
  @override
  String get name => 'test.xlsx';
  @override
  Future<int> length() async => 3;
  @override
  Future<Uint8List> readAsBytes() async => Uint8List.fromList([1, 2, 3]);
}

class ImportFake implements SchoolRepository {
  final calls = <SchoolOperation>[];
  @override
  Future<SchoolResult> execute(
    SchoolOperation operation, {
    Map<String, Object?> context = const {},
    Map<String, Object?> input = const {},
  }) async {
    calls.add(operation);
    if (operation == SchoolOperation.assignedClasses) {
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
    expect(input['classId'], schoolId);
    expect(input['fileName'], 'test.xlsx');
    expect(input['fileBytes'], [1, 2, 3]);
    return SchoolResult(
      summary: SchoolRecord({
        'canImport': true,
        'sheetName': 'Danh sách',
        'ignoredColumns': ['SĐT'],
        'created': 1,
      }),
      records: operation == SchoolOperation.previewStudentImport
          ? [
              SchoolRecord({
                'row': 4,
                'fullName': 'Nguyễn An',
                'dateOfBirth': '2021-12-03',
                'gender': 'FEMALE',
                'parentPhoneNumber': '0900000001',
                'error': null,
              }),
            ]
          : [],
    );
  }

  @override
  void dispose() {}
}

void main() {
  test(
    'multipart sends file bytes and selected class/date with authorization, never JSON or parent fields',
    () async {
      final repo = SchoolRepositoryImpl(
        MockClient((request) async {
          expect(request.headers['Authorization'], 'Bearer session');
          expect(
            request.headers['content-type'],
            startsWith('multipart/form-data;'),
          );
          final body = utf8.decode(request.bodyBytes);
          expect(body, contains('name="file"; filename="test.xlsx"'));
          expect(body, contains('xlsx-test-payload'));
          expect(body, contains(schoolId));
          expect(body, isNot(contains('phoneNumber')));
          return http.Response(
            '{"canImport":true,"rows":[{"row":4,"fullName":"An","error":null}]}',
            200,
          );
        }),
        'https://example.test/api',
        () => 'session',
      );
      addTearDown(repo.dispose);
      final result = await repo.execute(
        SchoolOperation.previewStudentImport,
        input: {
          'classId': schoolId,
          'startDate': schoolToday(),
          'fileName': 'test.xlsx',
          'fileBytes': utf8.encode('xlsx-test-payload'),
        },
      );
      expect(result.summary.flag('canImport'), true);
      expect(result.records.single.text('fullName'), 'An');
      expect(
        canExecute(SchoolOperation.previewStudentImport, ['TEACHER']),
        false,
      );
      expect(
        canExecute(SchoolOperation.confirmStudentImport, ['PARENT']),
        false,
      );
    },
  );
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'admin previews file and confirms import at $width without overflow',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final repo = ImportFake();
        await tester.pumpWidget(
          MaterialApp(
            home: StudentImportPage(
              repository: repo,
              roles: ['ADMIN'],
              onUnauthorized: () async {},
              pickFile: () async => MemoryXlsx(),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('Lớp nhận trẻ'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('A3 · 2026-2027').last);
        await tester.pumpAndSettle();
        await tester.tap(find.text('Chọn file XLSX'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Xem trước danh sách'));
        await tester.pumpAndSettle();
        expect(find.text('Nguyễn An'), findsOneWidget);
        expect(find.text('Ngày sinh: 03/12/2021'), findsNothing);
        final detailLink = find.text('Xem chi tiết ›');
        await tester.ensureVisible(detailLink);
        await tester.tap(detailLink);
        await tester.pumpAndSettle();
        expect(find.text('Ngày sinh: 03/12/2021'), findsOneWidget);
        expect(find.text('Giới tính: Nữ'), findsOneWidget);
        await tester.tap(find.text('Quay lại danh sách'));
        await tester.pumpAndSettle();
        expect(
          repo.calls,
          isNot(contains(SchoolOperation.confirmStudentImport)),
        );
        final commit = find.widgetWithText(FilledButton, 'Nhập 1 trẻ');
        await tester.ensureVisible(commit);
        await tester.tap(commit);
        await tester.pumpAndSettle();
        await tester.tap(find.text('Xác nhận'));
        await tester.pumpAndSettle();
        expect(
          repo.calls.where((x) => x == SchoolOperation.confirmStudentImport),
          hasLength(1),
        );
        expect(find.text('Đã nhập 1 trẻ.'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
}

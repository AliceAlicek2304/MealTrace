import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/app/mealtrace_app.dart';
import 'package:mealtrace_mobile/app/theme.dart';
import 'package:mealtrace_mobile/features/auth/data/auth_api.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_form_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_widgets.dart';
import 'support/auth_fixtures.dart';
import 'support/school_fixtures.dart';

void size(WidgetTester tester, double width, {double height = 780}) {
  tester.view.physicalSize = Size(width, height);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

void main() {
  final screens = [
    SchoolOperation.users,
    SchoolOperation.classes,
    SchoolOperation.students,
    SchoolOperation.scopedStudents,
    SchoolOperation.yearConfiguration,
    SchoolOperation.calendar,
    SchoolOperation.absences,
    SchoolOperation.children,
    SchoolOperation.workflowDays,
    SchoolOperation.portions,
    SchoolOperation.decisions,
    SchoolOperation.exceptionHistory,
    SchoolOperation.enrollments,
    SchoolOperation.amendments,
    SchoolOperation.amendmentDetail,
    SchoolOperation.calendarHistory,
    SchoolOperation.meals,
    SchoolOperation.mealDetail,
  ];
  for (final width in [320.0, 390.0, 768.0]) {
    for (final operation in screens) {
      testWidgets('$operation shows record cards without overflow at $width', (
        tester,
      ) async {
        size(tester, width);
        final repo = FakeSchoolRepository();
        await tester.pumpWidget(
          MaterialApp(
            theme: mealTraceTheme,
            home: SchoolPage(
              operation: operation,
              title: 'Danh sách',
              repository: repo,
              roles: ['ADMIN', 'TEACHER', 'PARENT', 'KITCHEN_STAFF'],
              onUnauthorized: () async {},
              contextValues: validSchoolValues(),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(repo.calls.first.operation, operation);
        expect(find.byType(Card), findsWidgets);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      });
    }
  }
  for (final form in schoolForms.values) {
    testWidgets('${form.operation} submits its native form once', (
      tester,
    ) async {
      size(tester, 320);
      final repo = FakeSchoolRepository();
      final controller = SchoolController(repo, [
        'ADMIN',
        'TEACHER',
        'PARENT',
        'KITCHEN_STAFF',
      ], () async {});
      addTearDown(controller.dispose);
      await tester.pumpWidget(
        MaterialApp(
          theme: mealTraceTheme,
          home: SchoolFormPage(
            form: form,
            controller: controller,
            contextValues: validSchoolValues(),
          ),
        ),
      );
      await tester.pumpAndSettle();
      if (form.operation == SchoolOperation.reviewAmendment) {
        await tester.ensureVisible(find.byType(CheckboxListTile).first);
        await tester.tap(find.byType(CheckboxListTile).first);
        await tester.pumpAndSettle();
      }
      final button = find.widgetWithText(FilledButton, form.title).last;
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();
      if (form.confirm) {
        await tester.tap(find.widgetWithText(FilledButton, 'Xác nhận'));
        await tester.pumpAndSettle();
      }
      expect(
        repo.calls.where((call) => call.operation == form.operation).length,
        1,
      );
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    });
  }
  for (final role in ['ADMIN', 'TEACHER', 'KITCHEN_STAFF', 'PARENT']) {
    testWidgets(
      '$role sees only its allowed home shortcuts and function sheet',
      (tester) async {
        size(tester, 390);
        final session = sessionJson();
        session['user'] = {
          ...userJson,
          'roles': [role],
        };
        final auth = createController(
          AuthApi(
            MockClient(
              (request) async => jsonResponse(
                request.url.path.endsWith('/me')
                    ? session['user'] as Map<String, Object?>
                    : session,
              ),
            ),
            'https://example.test/api',
          ),
          MemoryStore(),
        );
        await auth.restore();
        await auth.login('0349079940', 'password');
        await tester.pumpWidget(
          MealTraceApp(
            controller: auth,
            schoolRepository: FakeSchoolRepository(),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('Xin chào, Nguyễn An'), findsOneWidget);
        expect(
          find.text('Tài khoản'),
          role == 'ADMIN' ? findsOneWidget : findsNothing,
        );
        expect(
          find.text('Báo vắng / Không ăn'),
          role == 'PARENT' ? findsOneWidget : findsNothing,
        );
        expect(find.byType(NavigationBar), findsOneWidget);
        await tester.tap(find.text('Chức năng'));
        await tester.pumpAndSettle();
        expect(find.byType(BottomSheet), findsOneWidget);
        expect(
          find.text('Tài khoản'),
          role == 'ADMIN' ? findsWidgets : findsNothing,
        );
        expect(
          find.text('Báo vắng / Không ăn'),
          role == 'PARENT' ? findsWidgets : findsNothing,
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
        auth.dispose();
      },
    );
  }
  testWidgets(
    'Parent registration is available with no phone and notifications stay opt-in',
    (tester) async {
      final repo = FakeSchoolRepository();
      final controller = SchoolController(repo, ['TEACHER'], () async {});
      addTearDown(controller.dispose);
      await tester.pumpWidget(
        MaterialApp(
          home: SchoolFormPage(
            form: schoolForms[SchoolOperation.createStudent]!,
            controller: controller,
            contextValues: {...validSchoolValues(), 'phoneNumber': null},
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('chưa có SĐT'), findsOneWidget);
      await tester.ensureVisible(find.widgetWithText(FilledButton, 'Thêm trẻ'));
      await tester.tap(find.widgetWithText(FilledButton, 'Thêm trẻ'));
      await tester.pumpAndSettle();
      expect(repo.calls.single.input.containsKey('phoneNumber'), false);
    },
  );
  testWidgets('Temporary password disappears when its result dialog closes', (
    tester,
  ) async {
    final repo = FakeSchoolRepository();
    repo.handler = (_, _, _) async => SchoolResult(
      summary: SchoolRecord({
        'temporaryPassword': 'OnceOnly!123',
        'fullName': 'Nguyễn An',
      }),
      records: [],
    );
    final controller = SchoolController(repo, ['ADMIN'], () async {});
    addTearDown(controller.dispose);
    await tester.pumpWidget(
      MaterialApp(
        home: SchoolFormPage(
          form: schoolForms[SchoolOperation.createUser]!,
          controller: controller,
          contextValues: validSchoolValues(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.ensureVisible(
      find.widgetWithText(FilledButton, 'Thêm tài khoản'),
    );
    await tester.tap(find.widgetWithText(FilledButton, 'Thêm tài khoản'));
    await tester.pumpAndSettle();
    expect(find.text('OnceOnly!123'), findsOneWidget);
    await tester.tap(find.text('Đã lưu, đóng'));
    await tester.pumpAndSettle();
    expect(find.text('OnceOnly!123'), findsNothing);
  });
  testWidgets(
    'Duplicate submit and navigation are blocked during a pending write',
    (tester) async {
      final response = Completer<SchoolResult>();
      final repo = FakeSchoolRepository()
        ..handler = (_, _, _) => response.future;
      final controller = SchoolController(repo, ['ADMIN'], () async {});
      addTearDown(controller.dispose);
      await tester.pumpWidget(
        MaterialApp(
          home: SchoolFormPage(
            form: schoolForms[SchoolOperation.createClass]!,
            controller: controller,
            contextValues: validSchoolValues(),
          ),
        ),
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Tạo lớp'));
      await tester.pump();
      expect(find.widgetWithText(FilledButton, 'Đang xử lý…'), findsOneWidget);
      expect(repo.calls.length, 1);
      expect(tester.widget<PopScope>(find.byType(PopScope)).canPop, false);
      response.complete(schoolFixture(SchoolOperation.createClass));
      await tester.pumpAndSettle();
    },
  );
  testWidgets('Multi-picker keeps selections when changing pages', (
    tester,
  ) async {
    final repo = FakeSchoolRepository();
    repo.handler = (_, _, input) async => SchoolResult(
      summary: SchoolRecord({
        'classes': [
          {
            'id': input['classPage'] == 1 ? schoolId : otherId,
            'name': input['classPage'] == 1 ? 'M1' : 'M2',
          },
        ],
        'classTotal': 2,
        'pageSize': 1,
      }),
      records: [],
    );
    final controller = SchoolController(repo, ['ADMIN'], () async {});
    addTearDown(controller.dispose);
    await tester.pumpWidget(
      MaterialApp(
        home: ReferencePicker(
          field: accountFields.firstWhere((field) => field.key == 'classIds'),
          controller: controller,
          contextValues: const {},
          selected: const [],
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('M1'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Trang sau'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('M2'));
    await tester.pumpAndSettle();
    expect(find.text('Chọn 2 mục'), findsOneWidget);
    await tester.tap(find.byTooltip('Trang trước'));
    await tester.pumpAndSettle();
    expect(
      tester.widget<CheckboxListTile>(find.byType(CheckboxListTile)).value,
      true,
    );
  });
  testWidgets(
    'Preview generation shows plan and reuses the exact review token',
    (tester) async {
      final repo = FakeSchoolRepository();
      final controller = SchoolController(repo, ['ADMIN'], () async {});
      addTearDown(controller.dispose);
      await tester.pumpWidget(
        MaterialApp(
          home: SchoolFormPage(
            form: schoolForms[SchoolOperation.previewSessions]!,
            controller: controller,
            contextValues: validSchoolValues(),
          ),
        ),
      );
      await tester.ensureVisible(
        find.widgetWithText(FilledButton, 'Xem trước tạo phiên'),
      );
      await tester.tap(
        find.widgetWithText(FilledButton, 'Xem trước tạo phiên'),
      );
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.widgetWithText(FilledButton, 'Xác nhận tạo phiên'),
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Xác nhận tạo phiên'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Xác nhận tạo phiên'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Xác nhận'));
      await tester.pumpAndSettle();
      final call = repo.calls.last;
      expect(call.operation, SchoolOperation.generateSessions);
      expect(call.input['previewToken'], 'preview-test');
      expect(call.input['expectedRevision'], 3);
      expect(call.input['from'], schoolToday());
    },
  );
  testWidgets('Expired session removes protected routes as well as the home', (
    tester,
  ) async {
    final session = sessionJson()
      ..['user'] = {
        ...userJson,
        'roles': ['ADMIN'],
      };
    final auth = createController(
      AuthApi(
        MockClient(
          (request) async => jsonResponse(
            request.url.path.endsWith('/me')
                ? session['user'] as Map<String, Object?>
                : session,
          ),
        ),
        'https://example.test/api',
      ),
      MemoryStore(),
    );
    await auth.restore();
    await auth.login('0349079940', 'password');
    await tester.pumpWidget(
      MealTraceApp(controller: auth, schoolRepository: FakeSchoolRepository()),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sổ suất'));
    await tester.pumpAndSettle();
    expect(find.text('Danh sách'), findsNothing);
    await auth.invalidateSession();
    await tester.pumpAndSettle();
    expect(find.text('Đăng nhập'), findsOneWidget);
    expect(find.text('Sổ suất'), findsNothing);
    await tester.pumpWidget(const SizedBox());
    auth.dispose();
  });
  testWidgets('Capture student cards at phone width for visual inspection', (
    tester,
  ) async {
    size(tester, 390);
    await tester.runAsync(() async {
      final icons = File(
        'C:/Flutter/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
      );
      if (await icons.exists()) {
        await (FontLoader('MaterialIcons')..addFont(
              icons.readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
            ))
            .load();
      }
      final file = File('C:/Windows/Fonts/segoeui.ttf');
      if (await file.exists()) {
        final loader = FontLoader('MealTraceVisualTest')
          ..addFont(
            file.readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
          );
        await loader.load();
      }
    });
    final capture = GlobalKey();
    await tester.pumpWidget(
      RepaintBoundary(
        key: capture,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: mealTraceTheme.copyWith(
            textTheme: mealTraceTheme.textTheme.apply(
              fontFamily: 'MealTraceVisualTest',
            ),
          ),
          home: SchoolPage(
            operation: SchoolOperation.students,
            title: 'Lớp và trẻ',
            repository: FakeSchoolRepository(),
            roles: ['ADMIN'],
            onUnauthorized: () async {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.runAsync(() async {
      final boundary =
          capture.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await File(
        '../be/artifacts/mobile-students-390.png',
      ).parent.create(recursive: true);
      await File(
        '../be/artifacts/mobile-students-390.png',
      ).writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
    expect(tester.takeException(), isNull);
  });
}

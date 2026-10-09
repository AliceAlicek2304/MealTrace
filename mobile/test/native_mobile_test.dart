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
import 'package:mealtrace_mobile/features/auth/presentation/auth_controller.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_form_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_page.dart';
import 'support/auth_fixtures.dart';
import 'support/school_fixtures.dart';

Future<AuthController> mountApp(
  WidgetTester tester,
  FakeSchoolRepository repository,
  String role, {
  GlobalKey? capture,
  double textScale = 1,
  Map<String, Object?>? currentUser,
}) async {
  tester.platformDispatcher.textScaleFactorTestValue = textScale;
  addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
  final session = sessionJson()
    ..['user'] = {
      ...userJson,
      'roles': [role],
    };
  final auth = createController(
    AuthApi(
      MockClient(
        (request) async => jsonResponse(
          request.url.path.endsWith('/me')
              ? currentUser ?? session['user'] as Map<String, Object?>
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
    RepaintBoundary(
      key: capture,
      child: MealTraceApp(controller: auth, schoolRepository: repository),
    ),
  );
  await tester.pumpAndSettle();
  return auth;
}

void main() {
  testWidgets(
    'Returning from background preserves an unsaved form after valid revalidation',
    (tester) async {
      final auth = await mountApp(tester, FakeSchoolRepository(), 'ADMIN');
      await tester.tap(find.text('Trẻ'));
      await tester.pumpAndSettle();
      await tester.tap(find.byType(FloatingActionButton));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextFormField).first, 'Bé đang nhập');
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
      await tester.pumpAndSettle();
      expect(find.byType(SchoolFormPage), findsOneWidget);
      expect(
        tester
            .widget<TextFormField>(find.byType(TextFormField).first)
            .controller!
            .text,
        'Bé đang nhập',
      );
      await tester.pumpWidget(const SizedBox());
      auth.dispose();
    },
  );
  testWidgets('Changed permissions on resume reset staff screens', (
    tester,
  ) async {
    final currentUser = <String, Object?>{
      ...userJson,
      'roles': ['ADMIN'],
    };
    final auth = await mountApp(
      tester,
      FakeSchoolRepository(),
      'ADMIN',
      currentUser: currentUser,
    );
    await tester.tap(find.text('Trẻ'));
    await tester.pumpAndSettle();
    expect(find.byType(SchoolPage), findsOneWidget);
    currentUser['roles'] = ['PARENT'];
    await auth.restore(preserveSessionView: true);
    await tester.pumpAndSettle();
    expect(find.byType(SchoolPage), findsNothing);
    expect(find.text('Báo vắng / Không ăn'), findsOneWidget);
    expect(find.text('Tài khoản'), findsNothing);
    await tester.pumpWidget(const SizedBox());
    auth.dispose();
  });
  for (final role in ['ADMIN', 'TEACHER', 'KITCHEN_STAFF', 'PARENT']) {
    for (final width in [320.0, 390.0, 768.0]) {
      testWidgets('$role native home fits $width with larger text', (
        tester,
      ) async {
        tester.view.physicalSize = Size(width, 640);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final auth = await mountApp(
          tester,
          FakeSchoolRepository(),
          role,
          textScale: 1.5,
        );
        expect(
          MediaQuery.textScalerOf(
            tester.element(find.text('Xin chào, Nguyễn An')),
          ).scale(10),
          15,
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
        auth.dispose();
      });
    }
  }
  for (final role in ['ADMIN', 'PARENT']) {
    testWidgets('Capture native $role home for visual inspection', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(390, 780);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.runAsync(() async {
        for (final font in {
          'Roboto': 'C:/Windows/Fonts/segoeui.ttf',
          'MaterialIcons':
              'C:/Flutter/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
        }.entries) {
          final file = File(font.value);
          if (await file.exists()) {
            await (FontLoader(font.key)..addFont(
                  file.readAsBytes().then(
                    (bytes) => ByteData.sublistView(bytes),
                  ),
                ))
                .load();
          }
        }
      });
      final capture = GlobalKey();
      final auth = await mountApp(
        tester,
        FakeSchoolRepository(),
        role,
        capture: capture,
      );
      await tester.runAsync(() async {
        final boundary =
            capture.currentContext!.findRenderObject()!
                as RenderRepaintBoundary;
        final image = await boundary.toImage();
        final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
        final file = File(
          '../be/artifacts/mobile-native-${role.toLowerCase()}-390.png',
        );
        await file.parent.create(recursive: true);
        await file.writeAsBytes(bytes!.buffer.asUint8List());
        image.dispose();
      });
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      auth.dispose();
    });
  }
  testWidgets(
    'Work tab loads lazily, keeps state and Android Back returns home',
    (tester) async {
      final repo = FakeSchoolRepository();
      final auth = await mountApp(tester, repo, 'ADMIN');
      expect(repo.calls, isEmpty);
      await tester.tap(find.text('Suất ăn'));
      await tester.pumpAndSettle();
      expect(repo.calls.single.operation, SchoolOperation.workflowDays);
      await tester.tap(find.text('Trang chủ'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Suất ăn'));
      await tester.pumpAndSettle();
      expect(repo.calls.length, 1);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(
        tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex,
        0,
      );
      await tester.pumpWidget(const SizedBox());
      auth.dispose();
    },
  );

  testWidgets('Parent work tab opens absences and hides staff functions', (
    tester,
  ) async {
    final repo = FakeSchoolRepository();
    final auth = await mountApp(tester, repo, 'PARENT');
    await tester.tap(find.text('Không ăn'));
    await tester.pumpAndSettle();
    expect(repo.calls.single.operation, SchoolOperation.absences);
    await tester.tap(find.text('Chức năng'));
    await tester.pumpAndSettle();
    expect(find.text('Tài khoản'), findsNothing);
    expect(find.text('Lớp'), findsNothing);
    await tester.pumpWidget(const SizedBox());
    auth.dispose();
  });

  testWidgets(
    'Search waits for typing to pause and cancels work when disposed',
    (tester) async {
      final repo = FakeSchoolRepository();
      await tester.pumpWidget(
        MaterialApp(
          theme: mealTraceTheme,
          home: SchoolPage(
            operation: SchoolOperation.students,
            title: 'Trẻ',
            repository: repo,
            roles: ['ADMIN'],
            onUnauthorized: () async {},
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField).first, 'Nguyễn');
      await tester.pump(const Duration(milliseconds: 200));
      await tester.enterText(find.byType(TextField).first, 'Nguyễn An');
      await tester.pump(const Duration(milliseconds: 349));
      expect(repo.calls.length, 1);
      await tester.pump(const Duration(milliseconds: 1));
      await tester.pumpAndSettle();
      expect(repo.calls.last.input['search'], 'Nguyễn An');
      expect(repo.calls.length, 2);
      await tester.enterText(find.byType(TextField).first, 'Pending');
      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 400));
      expect(repo.calls.length, 2);
    },
  );

  testWidgets('Form save stays above the keyboard on a small phone', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 640);
    tester.view.devicePixelRatio = 1;
    tester.view.viewInsets = const FakeViewPadding(bottom: 280);
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.view.resetViewInsets);
    final controller = SchoolController(FakeSchoolRepository(), [
      'ADMIN',
    ], () async {});
    addTearDown(controller.dispose);
    await tester.pumpWidget(
      MaterialApp(
        theme: mealTraceTheme,
        home: SchoolFormPage(
          form: schoolForms[SchoolOperation.createUser]!,
          controller: controller,
          contextValues: validSchoolValues(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final save = find.widgetWithText(FilledButton, 'Thêm tài khoản');
    expect(tester.getRect(save).bottom, lessThanOrEqualTo(360));
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });
}

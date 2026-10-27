import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/app/theme.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/domain/kitchen_commands.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/kitchen_catalog_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/kitchen_editor_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/recipe_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/report_lookup_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/notification_test_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_page.dart';
import 'support/school_fixtures.dart';
import 'native_mobile_test.dart' show mountApp;

SchoolRecord ingredient() => SchoolRecord({
  'id': schoolId,
  'name': 'Gạo tẻ',
  'unit': 'g',
  'nutrition': {
    'version': 1,
    'ediblePercent': 100,
    'energyKcalPer100G': 360,
    'proteinGPer100G': 7,
  },
});
SchoolRecord nutrition() => SchoolRecord({
  'perServing': {'kcal': 360, 'proteinG': 7},
  'lines': [
    {
      'ingredientId': schoolId,
      'ingredientName': 'Gạo tẻ',
      'gramsPerPortion': 100,
      'contribution': {'kcal': 360, 'proteinG': 7},
    },
  ],
  'inputs': {
    'recipeVersion': 1,
    'validAt': '2026-10-20T01:00:00Z',
    'knownAt': '2026-10-20T01:00:00Z',
  },
  'roundingRule': {'kcalDecimals': 0, 'proteinDecimals': 1},
});
Future<void> mount(
  WidgetTester tester,
  Widget page, {
  double width = 390,
  GlobalKey? capture,
}) async {
  tester.view.physicalSize = Size(width, 850);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(const SizedBox());
  await tester.pumpWidget(
    RepaintBoundary(
      key: capture,
      child: MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: mealTraceTheme,
        home: page,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> enter(WidgetTester tester, String label, String value) async {
  final field = find.widgetWithText(TextFormField, label);
  await tester.ensureVisible(field);
  await tester.enterText(field, value);
  await tester.pump();
}

Future<void> tap(WidgetTester tester, Finder finder) async {
  if (finder.evaluate().isEmpty) {
    await tester.scrollUntilVisible(
      finder,
      200,
      scrollable: find
          .byWidgetPredicate(
            (widget) =>
                widget is Scrollable &&
                widget.axisDirection == AxisDirection.down,
          )
          .last,
    );
  }
  await Scrollable.ensureVisible(tester.element(finder), alignment: .25);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Account row opens the authoritative account detail API', (
    tester,
  ) async {
    final repo = FakeSchoolRepository();
    repo.handler = (op, context, input) async => SchoolResult(
      summary: op == SchoolOperation.users
          ? SchoolRecord({'total': 1, 'pageSize': 20})
          : SchoolRecord({
              'id': schoolId,
              'fullName': 'Tài khoản chi tiết',
              'roles': ['TEACHER'],
              'status': 'ACTIVE',
              'email': 'teacher@example.test',
            }),
      records: op == SchoolOperation.users
          ? [
              SchoolRecord({'id': schoolId, 'fullName': 'Tài khoản danh sách'}),
            ]
          : [],
    );
    await mount(
      tester,
      SchoolPage(
        operation: SchoolOperation.users,
        title: 'Tài khoản',
        repository: repo,
        roles: ['ADMIN'],
        onUnauthorized: () async {},
      ),
    );
    await tap(tester, find.byTooltip('Thao tác'));
    await tap(tester, find.text('Chi tiết tài khoản'));
    expect(find.text('Tài khoản chi tiết'), findsOneWidget);
    expect(repo.calls.last.operation, SchoolOperation.userDetail);
    expect(repo.calls.last.context['id'], schoolId);
  });
  test(
    'Missing nutrient data produces a useful Vietnamese message without leaking diagnostics',
    () async {
      final repo = SchoolRepositoryImpl(
        MockClient(
          (_) async => http.Response(
            jsonEncode({
              'code': 'NUTRIENT_DATA_MISSING',
              'detail': 'Internal English detail',
              'missingIngredients': [
                {'name': 'Gạo tẻ'},
              ],
            }),
            422,
            headers: {'content-type': 'application/json; charset=utf-8'},
          ),
        ),
        'https://example.test/api',
        () => 'token',
      );
      addTearDown(repo.dispose);
      await expectLater(
        repo.execute(
          SchoolOperation.nutrition,
          context: {'id': schoolId, 'v': 1},
        ),
        throwsA(
          isA<SchoolFailure>()
              .having((error) => error.message, 'message', contains('Gạo tẻ'))
              .having(
                (error) => error.message,
                'message',
                isNot(contains('Internal English detail')),
              ),
        ),
      );
    },
  );
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'Recipe result and report remain readable with larger text at $width',
      (tester) async {
        tester.platformDispatcher.textScaleFactorTestValue = 1.3;
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        final repo = FakeSchoolRepository();
        repo.handler = (op, context, input) async => SchoolResult(
          summary: op == SchoolOperation.nutrition
              ? nutrition()
              : SchoolRecord({
                  'id': schoolId,
                  'source': {'name': 'Bữa trưa'},
                  'energyKcalPerPortion': 360,
                }),
          records: [],
        );
        await mount(
          tester,
          RecipePage(
            repository: repo,
            roles: ['ADMIN'],
            onUnauthorized: () async {},
          ),
          width: width,
        );
        await enter(tester, 'Mã công thức', schoolId);
        await tap(tester, find.text('Xem dinh dưỡng'));
        await tap(tester, find.text('Căn cứ tính toán'));
        expect(tester.takeException(), isNull);
        await mount(
          tester,
          ReportLookupPage(
            repository: repo,
            roles: ['ADMIN'],
            onUnauthorized: () async {},
          ),
          width: width,
        );
        await enter(tester, 'Mã báo cáo', schoolId);
        await tap(tester, find.text('Xem nguồn báo cáo'));
        await tap(tester, find.text('Nguồn dữ liệu đã lưu'));
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets(
    'Ingredient search resets pagination and empty errors allow retry',
    (tester) async {
      var fail = true;
      final repo = FakeSchoolRepository();
      repo.handler = (_, context, input) async {
        if (fail) throw const SchoolFailure('Không kết nối được máy chủ.');
        return SchoolResult(
          summary: SchoolRecord({
            'total': input['search'] == 'không có' ? 0 : 21,
          }),
          records: input['search'] == 'không có' ? [] : [ingredient()],
        );
      };
      await mount(
        tester,
        KitchenCatalogPage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      );
      expect(find.text('Không kết nối được máy chủ.'), findsOneWidget);
      fail = false;
      await tap(tester, find.text('Thử lại'));
      await tap(tester, find.byTooltip('Trang sau'));
      expect(repo.calls.last.input['page'], 2);
      await tester.enterText(find.byType(TextField), 'không có');
      await tester.pump(const Duration(milliseconds: 350));
      await tester.pumpAndSettle();
      expect(repo.calls.last.input['page'], 1);
      expect(repo.calls.last.input['search'], 'không có');
      expect(find.text('Chưa có nguyên liệu phù hợp.'), findsOneWidget);
    },
  );
  test(
    'Kitchen numbers accept Vietnamese decimals and reject non-finite values',
    () {
      expect(kitchenNumber('12,5'), 12.5);
      expect(kitchenNumber('12.5'), 12.5);
      for (final text in ['', 'NaN', 'Infinity', '1,2,3']) {
        expect(kitchenNumber(text), isNull);
      }
      expect(isRecordId(schoolId), isTrue);
      expect(isRecordId('00000000-0000-0000-0000-000000000000'), isFalse);
    },
  );
  test(
    'Recipe validation rejects missing, repeated, tiny and negative quantities',
    () {
      final valid = {
        'name': 'Cơm',
        'servingBasis': 30,
        'lines': [recipeLine(ingredient(), '3000,5')],
      };
      expect(validateRecipe(valid, creating: true), isNull);
      for (final input in [
        {...valid, 'name': ''},
        {...valid, 'servingBasis': 0},
        {...valid, 'lines': <Object?>[]},
        {
          ...valid,
          'lines': [
            recipeLine(ingredient(), '1'),
            recipeLine(ingredient(), '2'),
          ],
        },
        {
          ...valid,
          'lines': [recipeLine(ingredient(), '-2')],
        },
        {
          ...valid,
          'lines': [recipeLine(ingredient(), '0.00001')],
        },
      ]) {
        expect(validateRecipe(input, creating: true), isNotNull);
      }
      expect(
        validateRecipe({...valid, 'expectedVersion': 2}, creating: false),
        isNull,
      );
      expect(
        validateRecipe({...valid, 'expectedVersion': null}, creating: false),
        isNotNull,
      );
    },
  );
  test(
    'Nutrient validation checks range boundaries and preserves zero values',
    () {
      final valid = {
        'validFrom': '2026-10-20T01:00:00Z',
        'ediblePercent': 100,
        'energyKcal': 900,
        'proteinG': 0,
        'pricePerKg': null,
      };
      expect(validateIngredientVersion(valid), isNull);
      for (final input in [
        {...valid, 'ediblePercent': 101},
        {...valid, 'energyKcal': -1},
        {...valid, 'proteinG': 101},
        {...valid, 'pricePerKg': -1},
        {...valid, 'validFrom': ''},
      ]) {
        expect(validateIngredientVersion(input), isNotNull);
      }
    },
  );
  test(
    'New kitchen operations, reports and notification writes enforce role boundaries',
    () async {
      final repo = FakeSchoolRepository();
      for (final role in ['PARENT', 'TEACHER']) {
        final controller = SchoolController(repo, [role], () async {});
        for (final op in [
          SchoolOperation.ingredients,
          SchoolOperation.createIngredient,
          SchoolOperation.ingredientVersion,
          SchoolOperation.createRecipe,
          SchoolOperation.recipeVersion,
          SchoolOperation.nutrition,
          SchoolOperation.reportLineage,
          SchoolOperation.sendTestNotification,
          SchoolOperation.userDetail,
        ]) {
          await expectLater(
            controller.execute(op),
            throwsA(
              isA<SchoolFailure>().having((e) => e.status, 'status', 403),
            ),
          );
        }
        controller.dispose();
      }
      expect(canExecute(SchoolOperation.nutrition, ['KITCHEN_STAFF']), isTrue);
      expect(
        canExecute(SchoolOperation.reportLineage, ['KITCHEN_STAFF']),
        isFalse,
      );
      expect(
        canExecute(SchoolOperation.sendTestNotification, ['KITCHEN_STAFF']),
        isFalse,
      );
      expect(repo.calls, isEmpty);
    },
  );
  test(
    'HTTP adapters preserve batch quantity, expected version and offset timestamps',
    () async {
      final sent = <http.Request>[];
      final repo = SchoolRepositoryImpl(
        MockClient((request) async {
          sent.add(request);
          return http.Response('{}', 200);
        }),
        'https://example.test/api',
        () => 'token',
      );
      addTearDown(repo.dispose);
      await repo.execute(
        SchoolOperation.createRecipe,
        input: {
          'name': 'Cơm',
          'servingBasis': 30,
          'lines': [recipeLine(ingredient(), '3000,5')],
        },
      );
      await repo.execute(
        SchoolOperation.recipeVersion,
        context: {'id': otherId},
        input: {
          'expectedVersion': 2,
          'servingBasis': 30,
          'lines': [recipeLine(ingredient(), '3000')],
        },
      );
      await repo.execute(
        SchoolOperation.nutrition,
        context: {'id': otherId, 'v': 3},
        input: {
          'validAt': '2026-10-20T08:00:00+07:00',
          'knownAt': '2026-10-20T01:00:00Z',
        },
      );
      expect(jsonDecode(sent[0].body)['lines'][0]['quantity'], 3000.5);
      expect(jsonDecode(sent[1].body)['expectedVersion'], 2);
      expect(
        sent[2].url.path,
        '/api/kitchen/recipes/$otherId/versions/3/nutrition',
      );
      expect(
        sent[2].url.queryParameters['validAt'],
        '2026-10-20T08:00:00+07:00',
      );
      expect(sent[2].url.queryParameters['knownAt'], '2026-10-20T01:00:00Z');
    },
  );
  test(
    'Report source decoding remains in data layer and tolerates malformed snapshots',
    () async {
      for (final source in ['{"name":"Bữa trưa","count":30}', 'not json']) {
        final repo = SchoolRepositoryImpl(
          MockClient(
            (_) async => http.Response(
              jsonEncode({'id': schoolId, 'sourceJson': source}),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            ),
          ),
          'https://example.test/api',
          () => 'token',
        );
        final result = await repo.execute(
          SchoolOperation.reportLineage,
          context: {'id': schoolId},
        );
        expect(result.summary.text('id'), schoolId);
        expect(
          result.summary['source'] is SchoolRecord,
          source.startsWith('{'),
        );
        repo.dispose();
      }
    },
  );
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets('Ingredient cards and nutrient form fit $width with keyboard', (
      tester,
    ) async {
      final repo = FakeSchoolRepository();
      repo.handler = (op, context, input) async => SchoolResult(
        summary: SchoolRecord({'total': 1, 'pageSize': 20}),
        records: [ingredient()],
      );
      await mount(
        tester,
        KitchenCatalogPage(
          repository: repo,
          roles: ['KITCHEN_STAFF'],
          onUnauthorized: () async {},
        ),
        width: width,
      );
      expect(find.text('Gạo tẻ'), findsOneWidget);
      await tap(tester, find.text('Cập nhật dinh dưỡng'));
      await enter(tester, 'Chất đạm (g / 100)', '7,5');
      tester.view.viewInsets = const FakeViewPadding(bottom: 300);
      addTearDown(tester.view.resetViewInsets);
      await tester.pumpAndSettle();
      final save = find.widgetWithText(FilledButton, 'Lưu');
      expect(tester.getRect(save).bottom, lessThanOrEqualTo(550));
      expect(tester.takeException(), isNull);
    });
  }
  testWidgets(
    'Ingredient create and nutrient version send only after explicit confirmation',
    (tester) async {
      final repo = FakeSchoolRepository();
      repo.handler = (op, context, input) async => SchoolResult(
        summary: op == SchoolOperation.ingredients
            ? SchoolRecord({'total': 1})
            : ingredient(),
        records: op == SchoolOperation.ingredients ? [ingredient()] : [],
      );
      await mount(
        tester,
        KitchenCatalogPage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      );
      await tap(tester, find.text('Thêm nguyên liệu'));
      await enter(tester, 'Tên nguyên liệu', 'Gạo tẻ mới');
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu'));
      expect(
        repo.calls.any((c) => c.operation == SchoolOperation.createIngredient),
        isFalse,
      );
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu').last);
      expect(
        repo.calls
            .singleWhere((c) => c.operation == SchoolOperation.createIngredient)
            .input,
        {'name': 'Gạo tẻ mới', 'unit': 'g'},
      );
      await tap(tester, find.text('Cập nhật dinh dưỡng'));
      await enter(tester, 'Năng lượng (kcal / 100)', '360,5');
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu'));
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu').last);
      final version = repo.calls.singleWhere(
        (c) => c.operation == SchoolOperation.ingredientVersion,
      );
      expect(version.context, {'id': schoolId});
      expect(version.input['energyKcal'], 360.5);
      expect(version.input['pricePerKg'], isNull);
      expect(
        DateTime.parse(version.input['validFrom'] as String).isUtc,
        isTrue,
      );
    },
  );
  testWidgets(
    'Recipe creation uses ingredient picker and opens nutrition for the created version',
    (tester) async {
      final repo = FakeSchoolRepository();
      repo.handler = (op, context, input) async => SchoolResult(
        summary: op == SchoolOperation.ingredients
            ? SchoolRecord({'total': 1})
            : op == SchoolOperation.createRecipe
            ? SchoolRecord({'recipeId': otherId, 'version': 1})
            : nutrition(),
        records: op == SchoolOperation.ingredients ? [ingredient()] : [],
      );
      await mount(
        tester,
        RecipePage(
          repository: repo,
          roles: ['KITCHEN_STAFF'],
          onUnauthorized: () async {},
        ),
      );
      await tap(tester, find.text('Tạo công thức'));
      await enter(tester, 'Tên công thức', 'Cơm trắng');
      await enter(tester, 'Số suất của công thức', '30');
      await tap(tester, find.text('Chọn nguyên liệu'));
      await tap(tester, find.text('Gạo tẻ'));
      await enter(tester, 'Khối lượng (g)', '3000,5');
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu'));
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu').last);
      final call = repo.calls.singleWhere(
        (c) => c.operation == SchoolOperation.createRecipe,
      );
      expect(call.input, {
        'name': 'Cơm trắng',
        'servingBasis': 30,
        'lines': [
          {'ingredientId': schoolId, 'quantity': 3000.5, 'unit': 'g'},
        ],
      });
      expect(find.text('Dinh dưỡng mỗi suất'), findsOneWidget);
      expect(repo.calls.last.context, {'id': otherId, 'v': 1});
    },
  );
  testWidgets(
    'Recipe version conflict keeps draft and requires an explicit retry',
    (tester) async {
      final repo = FakeSchoolRepository();
      repo.handler = (op, context, input) async {
        if (op == SchoolOperation.recipeVersion) {
          throw const SchoolFailure('Bản công thức đã thay đổi.', status: 409);
        }
        return SchoolResult(
          summary: SchoolRecord({'total': 1}),
          records: [ingredient()],
        );
      };
      await mount(
        tester,
        KitchenEditorPage(
          operation: SchoolOperation.recipeVersion,
          record: SchoolRecord({'id': otherId}),
          expectedVersion: 2,
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      );
      await tap(tester, find.text('Chọn nguyên liệu'));
      await tap(tester, find.text('Gạo tẻ'));
      await enter(tester, 'Khối lượng (g)', '100');
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu'));
      await tap(tester, find.widgetWithText(FilledButton, 'Lưu').last);
      expect(find.text('Bản công thức đã thay đổi.'), findsOneWidget);
      expect(find.text('100'), findsOneWidget);
      final call = repo.calls.singleWhere(
        (c) => c.operation == SchoolOperation.recipeVersion,
      );
      expect(call.input['expectedVersion'], 2);
      expect(call.context, {'id': otherId});
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Editing lookup identity clears old nutrition and disables creating a version from stale results',
    (tester) async {
      final repo = FakeSchoolRepository();
      repo.handler = (_, context, input) async =>
          SchoolResult(summary: nutrition(), records: []);
      await mount(
        tester,
        RecipePage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      );
      await enter(tester, 'Mã công thức', otherId);
      await tap(tester, find.text('Xem dinh dưỡng'));
      expect(find.text('Thêm bản công thức'), findsOneWidget);
      await enter(tester, 'Mã công thức', schoolId);
      expect(find.text('Thêm bản công thức'), findsNothing);
      expect(find.text('Dinh dưỡng mỗi suất'), findsNothing);
    },
  );
  testWidgets(
    'Lookup handles missing recipe and retry without fabricating totals',
    (tester) async {
      var failed = true;
      final repo = FakeSchoolRepository();
      repo.handler = (_, context, input) async {
        if (failed) {
          throw const SchoolFailure('Không tìm thấy công thức.', status: 404);
        }
        return SchoolResult(summary: nutrition(), records: []);
      };
      await mount(
        tester,
        RecipePage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      );
      await enter(tester, 'Mã công thức', schoolId);
      await tap(tester, find.text('Xem dinh dưỡng'));
      expect(find.text('Không tìm thấy công thức.'), findsOneWidget);
      expect(find.text('Dinh dưỡng mỗi suất'), findsNothing);
      failed = false;
      await tap(tester, find.text('Thử lại'));
      expect(find.text('Dinh dưỡng mỗi suất'), findsOneWidget);
    },
  );
  testWidgets('Report lookup validates id and displays snapshot provenance', (
    tester,
  ) async {
    final repo = FakeSchoolRepository();
    repo.handler = (_, context, input) async => SchoolResult(
      summary: SchoolRecord({
        'id': schoolId,
        'mealDayId': otherId,
        'settlementId': otherId,
        'energyKcalPerPortion': 360,
        'source': {'name': 'Bữa trưa'},
      }),
      records: [],
    );
    await mount(
      tester,
      ReportLookupPage(
        repository: repo,
        roles: ['ADMIN'],
        onUnauthorized: () async {},
      ),
    );
    await tap(tester, find.text('Xem nguồn báo cáo'));
    expect(repo.calls, isEmpty);
    await enter(tester, 'Mã báo cáo', schoolId);
    await tap(tester, find.text('Xem nguồn báo cáo'));
    expect(repo.calls.single.context, {'id': schoolId});
    expect(find.text('360'), findsOneWidget);
    await tap(tester, find.text('Nguồn dữ liệu đã lưu'));
    await tap(tester, find.text('Tên'));
    expect(find.text('Bữa trưa'), findsOneWidget);
  });
  testWidgets(
    'Notification test never sends on load, cancel or repeated taps',
    (tester) async {
      final pending = Completer<SchoolResult>();
      final repo = FakeSchoolRepository();
      repo.handler = (op, context, input) async =>
          op == SchoolOperation.sendTestNotification
          ? pending.future
          : SchoolResult(summary: SchoolRecord({'enabled': true}), records: []);
      await mount(
        tester,
        NotificationTestPage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      );
      expect(
        repo.calls.where(
          (c) => c.operation == SchoolOperation.sendTestNotification,
        ),
        isEmpty,
      );
      await tap(tester, find.text('Gửi tin thử'));
      await tap(tester, find.text('Quay lại'));
      expect(
        repo.calls.where(
          (c) => c.operation == SchoolOperation.sendTestNotification,
        ),
        isEmpty,
      );
      await tap(tester, find.text('Gửi tin thử'));
      await tester.tap(find.text('Gửi tin'));
      await tester.pump();
      final button = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Đang gửi…'),
      );
      expect(button.onPressed, isNull);
      expect(
        repo.calls
            .where((c) => c.operation == SchoolOperation.sendTestNotification)
            .length,
        1,
      );
      pending.complete(
        SchoolResult(
          summary: SchoolRecord({'status': 'ACCEPTED'}),
          records: [],
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.textContaining('Kiểm tra tin nhắn trên điện thoại nhận.'),
        findsOneWidget,
      );
    },
  );
  for (final role in ['ADMIN', 'KITCHEN_STAFF', 'TEACHER', 'PARENT']) {
    testWidgets('New functions are discoverable only for allowed role $role', (
      tester,
    ) async {
      final auth = await mountApp(tester, FakeSchoolRepository(), role);
      await tap(tester, find.text('Xem tất cả chức năng'));
      if (role == 'ADMIN' || role == 'KITCHEN_STAFF') {
        await tester.scrollUntilVisible(
          find.descendant(
            of: find.byType(BottomSheet),
            matching: find.text(
              role == 'ADMIN' ? 'Kiểm tra gửi tin' : 'Công thức & dinh dưỡng',
            ),
          ),
          250,
          scrollable: find.byType(Scrollable).last,
        );
        await tester.pumpAndSettle();
      }
      expect(
        find.text('Nguyên liệu'),
        role == 'ADMIN' || role == 'KITCHEN_STAFF'
            ? findsWidgets
            : findsNothing,
      );
      expect(
        find.text('Công thức & dinh dưỡng'),
        role == 'ADMIN' || role == 'KITCHEN_STAFF'
            ? findsWidgets
            : findsNothing,
      );
      expect(
        find.text('Truy vết báo cáo'),
        role == 'ADMIN' ? findsOneWidget : findsNothing,
      );
      expect(
        find.text('Kiểm tra gửi tin'),
        role == 'ADMIN' ? findsOneWidget : findsNothing,
      );
      await tester.pumpWidget(const SizedBox());
      auth.dispose();
    });
  }
  testWidgets('Capture ingredient layout for visual review', (tester) async {
    debugDisableShadows = false;
    addTearDown(() => debugDisableShadows = true);
    await tester.runAsync(() async {
      final icons = File(
        'C:/Flutter/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
      );
      await (FontLoader('MaterialIcons')
            ..addFont(icons.readAsBytes().then((b) => b.buffer.asByteData())))
          .load();
      final font = FontLoader('MealTraceCatalogVisual')
        ..addFont(
          File(
            'C:/Windows/Fonts/arial.ttf',
          ).readAsBytes().then((b) => b.buffer.asByteData()),
        );
      await font.load();
      await (FontLoader('Roboto')..addFont(
            File(
              'C:/Windows/Fonts/segoeui.ttf',
            ).readAsBytes().then((b) => b.buffer.asByteData()),
          ))
          .load();
    });
    final capture = GlobalKey();
    final repo = FakeSchoolRepository();
    repo.handler = (_, context, input) async => SchoolResult(
      summary: SchoolRecord({'total': 1}),
      records: [ingredient()],
    );
    await mount(
      tester,
      Theme(
        data: mealTraceTheme.copyWith(
          textTheme: mealTraceTheme.textTheme.apply(
            fontFamily: 'MealTraceCatalogVisual',
          ),
        ),
        child: KitchenCatalogPage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      ),
      capture: capture,
    );
    await tester.runAsync(() async {
      final boundary =
          capture.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await File(
        '../be/artifacts/mobile-ingredients-390.png',
      ).writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
    repo.handler = (_, context, input) async =>
        SchoolResult(summary: nutrition(), records: []);
    await mount(
      tester,
      Theme(
        data: mealTraceTheme.copyWith(
          textTheme: mealTraceTheme.textTheme.apply(
            fontFamily: 'MealTraceCatalogVisual',
          ),
        ),
        child: RecipePage(
          repository: repo,
          roles: ['ADMIN'],
          onUnauthorized: () async {},
        ),
      ),
      capture: capture,
    );
    await enter(tester, 'Mã công thức', otherId);
    await tap(tester, find.text('Xem dinh dưỡng'));
    await tester.runAsync(() async {
      final boundary =
          capture.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await File(
        '../be/artifacts/mobile-nutrition-390.png',
      ).writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
    debugDisableShadows = true;
    expect(tester.takeException(), isNull);
  });
}

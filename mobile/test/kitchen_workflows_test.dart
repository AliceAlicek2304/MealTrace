import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/kitchen_menu_insights.dart';
import 'package:mealtrace_mobile/features/school/presentation/kitchen_menus_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/kitchen_sources_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/parent_menus_page.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_controller.dart';

const date = '2026-11-27';
const dish = {
  'recipeId': 'recipe-a',
  'recipeVersionId': 'recipe-v2',
  'recipeName': 'Canh bí đỏ',
  'recipeVersion': 2,
  'plannedPortions': null,
};

Map<String, Object?> menu(String state, {int version = 2}) => {
  'id': 'menu-a',
  'mealDayId': 'lunch-a',
  'date': date,
  'mealType': 'Lunch',
  'publishedVersion': state == 'published' ? version : null,
  'publishedVersionId': state == 'published' ? 'published-v2' : null,
  'latest': {
    'version': version,
    'state': state,
    'dishes': [dish],
  },
};

Map<String, Object?> page(List<Object?> records) => {
  'items': records,
  'total': records.length,
  'pageSize': 20,
};

http.Response json(Object data, {int status = 200}) => http.Response(
  jsonEncode(data),
  status,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

typedef ViewBuilder = Widget Function(SchoolController controller);

Future<void> mount(
  WidgetTester tester,
  ViewBuilder build,
  Future<http.Response> Function(http.Request request) respond, {
  List<String> roles = const ['KITCHEN_STAFF'],
  double width = 390,
  double textScale = 1,
}) async {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = Size(width, 760);
  addTearDown(tester.view.resetDevicePixelRatio);
  addTearDown(tester.view.resetPhysicalSize);
  final repository = SchoolRepositoryImpl(
    MockClient(respond),
    'https://example.test/api',
    () => 'fixture-token',
  );
  final controller = SchoolController(repository, roles, () async {});
  addTearDown(controller.dispose);
  addTearDown(repository.dispose);
  await tester.pumpWidget(
    MaterialApp(
      home: build(controller),
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(
          context,
        ).copyWith(textScaler: TextScaler.linear(textScale)),
        child: child!,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> tap(WidgetTester tester, Finder finder) async {
  if (finder.evaluate().isEmpty) {
    await tester.scrollUntilVisible(
      finder,
      180,
      scrollable: find.byType(Scrollable).first,
    );
  } else {
    await tester.ensureVisible(finder);
  }
  await tester.pump();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Finder tile(String text) => find.widgetWithText(ListTile, text);

Future<void> tapMenuAction(
  WidgetTester tester,
  String label, {
  String menu = 'Thao tác thực đơn',
}) async {
  if (find.text(label).evaluate().isEmpty) {
    await tap(tester, find.byTooltip(menu));
  }
  await tap(tester, find.text(label));
}

void main() {
  testWidgets(
    'receipt uses the ingredient unit and selected supplier revision at 320 px',
    (tester) async {
      Map<String, Object?>? posted;
      await mount(
        tester,
        (controller) => KitchenSourcesPage(controller: controller),
        (request) async {
          switch (request.url.path) {
            case '/api/kitchen/ingredient-source-entries':
              if (request.method == 'POST') {
                posted = jsonDecode(request.body) as Map<String, Object?>;
                return json({'id': 'receipt-1'}, status: 201);
              }
              return json(page([]));
            case '/api/kitchen/ingredients':
              return json(
                page([
                  {'id': 'pumpkin', 'name': 'Bí đỏ', 'unit': 'g'},
                ]),
              );
            case '/api/kitchen/suppliers':
              return json(
                page([
                  {
                    'id': 'supplier-a',
                    'versionId': 'supplier-v3',
                    'version': 3,
                    'name': 'Nhà vườn A',
                  },
                ]),
              );
          }
          throw StateError(request.url.toString());
        },
        width: 320,
        textScale: 1.4,
      );
      await tap(tester, find.text('Thêm phiếu nguồn'));
      await tap(tester, tile('Nguyên liệu'));
      await tap(tester, tile('Bí đỏ'));
      await tap(tester, tile('Nhà cung cấp'));
      await tap(tester, tile('Nhà vườn A'));
      final quantity = find.widgetWithText(TextFormField, 'Số lượng (g)');
      await tester.ensureVisible(quantity);
      await tester.enterText(quantity, '600,5');
      await tap(tester, find.text('Lưu'));
      expect(posted, {
        'ingredientId': 'pumpkin',
        'supplierVersionId': 'supplier-v3',
        'referenceDate': schoolToday(),
        'quantity': 600.5,
        'unit': 'g',
        'deliveryRef': null,
        'lotRef': null,
      });
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'kitchen creates and submits a menu with a selected recipe version',
    (tester) async {
      String? state;
      final writes = <(String, Object?)>[];
      await mount(
        tester,
        (controller) => KitchenMenusPage(controller: controller),
        (request) async {
          final path = request.url.path;
          if (request.method == 'POST') {
            writes.add((path, jsonDecode(request.body)));
            state = path.endsWith('/submit') ? 'submitted' : 'draft';
            return json({'id': 'menu-a'});
          }
          return switch (path) {
            '/api/kitchen/menus' => json(
              page([if (state != null) menu(state!)]),
            ),
            '/api/kitchen/menus/menu-a' => json(menu(state!)),
            '/api/meal-days/search' => json(
              page([
                {
                  'id': 'lunch-a',
                  'mealType': 'Lunch',
                  'date': date,
                  'isCancelled': false,
                },
                {
                  'id': 'snack-x',
                  'mealType': 'Snack',
                  'date': date,
                  'isCancelled': true,
                },
              ]),
            ),
            '/api/kitchen/recipes' => json(
              page([
                {'recipeId': 'recipe-a', 'name': 'Canh bí đỏ'},
              ]),
            ),
            '/api/kitchen/recipes/recipe-a/versions' => json({
              'items': [
                {'versionId': 'recipe-v2', 'version': 2, 'lines': <Object?>[]},
              ],
            }),
            _ => throw StateError(path),
          };
        },
      );
      await tap(tester, find.text('Tạo thực đơn'));
      await tap(tester, tile('Phiên ăn'));
      expect(find.text('Bữa phụ'), findsNothing);
      await tap(tester, tile('Bữa trưa'));
      await tap(tester, find.text('Thêm món'));
      await tap(tester, tile('Canh bí đỏ'));
      await tap(tester, tile('Bản 2'));
      await tap(tester, find.text('Lưu bản nháp'));
      expect(writes.first.$2, {
        'mealDayId': 'lunch-a',
        'reason': null,
        'dishes': [
          {'recipeVersionId': 'recipe-v2', 'plannedPortions': null},
        ],
      });
      await tap(tester, tile('Bữa trưa · 27/11/2026'));
      await tapMenuAction(tester, 'Gửi duyệt');
      await tap(tester, find.text('Gửi duyệt'));
      expect(writes.last.$1, '/api/kitchen/menus/menu-a/submit');
      expect(writes.last.$2, {'expectedVersion': 2});
      expect(find.text('Chờ duyệt'), findsOneWidget);
      expect(find.text('Duyệt'), findsNothing);
      expect(find.text('Từ chối'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'admin must enter a rejection reason and sends the viewed version',
    (tester) async {
      var state = 'submitted';
      Map<String, Object?>? posted;
      await mount(
        tester,
        (controller) => KitchenMenusPage(controller: controller),
        (request) async {
          switch (request.url.path) {
            case '/api/kitchen/menus':
              return json(page([menu(state)]));
            case '/api/kitchen/menus/menu-a':
              return json(menu(state));
            case '/api/admin/menus/menu-a/review':
              posted = jsonDecode(request.body) as Map<String, Object?>;
              state = 'rejected';
              return json({});
          }
          throw StateError(request.url.toString());
        },
        roles: ['ADMIN'],
        width: 320,
        textScale: 1.4,
      );
      await tap(tester, tile('Bữa trưa · 27/11/2026'));
      await tapMenuAction(tester, 'Từ chối');
      await tap(tester, find.text('Từ chối'));
      expect(posted, isNull);
      expect(find.text('Vui lòng nhập Lý do từ chối.'), findsOneWidget);
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Lý do từ chối'),
        'Thiếu món chính',
      );
      await tap(tester, find.text('Từ chối'));
      expect(posted, {
        'expectedVersion': 2,
        'decision': 'reject',
        'reason': 'Thiếu món chính',
      });
      expect(find.text('Bị từ chối'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'estimate blocks reuse of a rejected snapshot and reads the saved result',
    (tester) async {
      String token = 'snapshot-a';
      final posted = <Object?>[];
      final estimate = {
        'id': 'estimate-a',
        'createdAt': '2026-11-27T01:00:00Z',
        'lines': [
          {
            'ingredientName': 'Bí đỏ',
            'quantity': 3000,
            'unit': 'g',
            'edibleGrams': 2500,
          },
        ],
      };
      await mount(
        tester,
        (controller) => KitchenMenuEstimate(
          controller: controller,
          menu: SchoolRecord(menu('published')),
        ),
        (request) async {
          switch (request.url.path) {
            case '/api/kitchen/settlements/lunch-a/diff':
              return json({
                'appliedTotal': 30,
                'snapshotToken': token,
                'classDiffs': [
                  {'classId': 'class-a', 'className': 'A1', 'appliedCount': 30},
                ],
              });
            case '/api/kitchen/ingredient-estimates':
              posted.add(jsonDecode(request.body));
              return posted.length == 1
                  ? json({'code': 'STALE_SETTLEMENT'}, status: 409)
                  : json(estimate, status: 201);
            case '/api/kitchen/ingredient-estimates/estimate-a':
              return json(estimate);
          }
          throw StateError(request.url.toString());
        },
      );
      await tap(tester, find.byType(CheckboxListTile));
      await tapMenuAction(tester, 'Tạo dự trù', menu: 'Thao tác');
      expect(
        tester
            .widget<CheckboxListTile>(find.byType(CheckboxListTile))
            .onChanged,
        isNull,
      );
      token = 'snapshot-b';
      await tapMenuAction(tester, 'Tải lại suất', menu: 'Thao tác');
      await tap(tester, find.byType(CheckboxListTile));
      await tapMenuAction(tester, 'Tạo dự trù', menu: 'Thao tác');
      expect(posted.last, {
        'menuVersionIds': ['published-v2'],
        'mealDayIds': ['lunch-a'],
        'classIds': <String>[],
        'snapshotTokens': {'lunch-a': 'snapshot-b'},
      });
      expect(find.text('Kết quả dự trù'), findsOneWidget);
      expect(find.text('3000 g'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'admin publishes only the approved menu and sees the saved version',
    (tester) async {
      String state = 'approved';
      Object? posted;
      await mount(
        tester,
        (controller) => KitchenMenusPage(controller: controller),
        (request) async {
          switch (request.url.path) {
            case '/api/kitchen/menus':
              return json(page([menu(state)]));
            case '/api/kitchen/menus/menu-a':
              return json(menu(state));
            case '/api/admin/menus/menu-a/publish':
              posted = jsonDecode(request.body);
              state = 'published';
              return json({});
          }
          throw StateError(request.url.toString());
        },
        roles: ['ADMIN'],
      );
      await tap(tester, tile('Bữa trưa · 27/11/2026'));
      await tapMenuAction(tester, 'Công bố');
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Nhóm tuổi (tùy chọn)'),
        '4–5 tuổi',
      );
      await tap(tester, find.text('Công bố'));
      expect(posted, {'expectedVersion': 2, 'ageGroup': '4–5 tuổi'});
      expect(find.text('Đã công bố'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('published nutrition remains distinct from recomputed data', (
    tester,
  ) async {
    final reads = <Map<String, String>>[];
    const frozen = {
      'total': {'kcal': 240, 'proteinG': 8, 'fatG': 6, 'carbG': 37},
      'ageComparisonStatus': 'not_requested',
    };
    await mount(
      tester,
      (controller) => KitchenMenuNutrition(
        controller: controller,
        id: 'menu-a',
        version: 2,
      ),
      (request) async {
        expect(request.url.path, '/api/kitchen/menus/menu-a/nutrition');
        reads.add(request.url.queryParameters);
        if (request.url.queryParameters['mode'] == 'pinned') {
          return json({'payload': frozen});
        }
        return json({
          'payload': {
            'total': {'kcal': 260, 'proteinG': 8, 'fatG': 6, 'carbG': 37},
            'ageGroup': '4–5 tuổi',
            'ageComparisonStatus': 'reference_missing',
          },
          'asPublished': frozen,
          'deltaFromPublished': {
            'kcal': 20,
            'proteinG': 0,
            'fatG': 0,
            'carbG': 0,
          },
        });
      },
      width: 768,
    );
    expect(find.text('240 kcal'), findsOneWidget);
    await tap(tester, find.text('Tính lại'));
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Nhóm tuổi (tùy chọn)'),
      '4–5 tuổi',
    );
    await tap(
      tester,
      find.descendant(of: find.byType(Dialog), matching: find.text('Tính lại')),
    );
    expect(find.text('260 kcal'), findsOneWidget);
    expect(
      find.text('Chưa có khoảng tham chiếu cho nhóm tuổi này.'),
      findsOneWidget,
    );
    await tap(tester, find.text('Đối chiếu bản đã công bố'));
    expect(find.text('240 kcal'), findsOneWidget);
    expect(reads, [
      {'version': '2', 'mode': 'pinned'},
      {'version': '2', 'mode': 'recompute', 'ageGroup': '4–5 tuổi'},
    ]);
    expect(tester.takeException(), isNull);
  });

  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'parent opens the linked child and missing nutrition stays unknown at ${width.toInt()} px',
      (tester) async {
        final requested = <String>[];
        await mount(
          tester,
          (controller) => ParentMenusPage(controller: controller),
          (request) async {
            requested.add(request.url.path);
            switch (request.url.path) {
              case '/api/parent/students':
                return json([
                  {'studentId': 'child-a', 'fullName': 'An'},
                  {'studentId': 'child-b', 'fullName': 'Minh'},
                ]);
              case '/api/parent/students/child-a/menus':
                return json(page([]));
              case '/api/parent/students/child-b/menus':
                expect(
                  request.url.queryParameters,
                  containsPair('from', schoolToday()),
                );
                return json(
                  page([
                    {
                      'date': date,
                      'mealType': 'Lunch',
                      'dishes': [dish],
                      'nutrition': {
                        'kcal': 120,
                        'proteinG': 8,
                        'fatG': null,
                        'carbG': null,
                      },
                    },
                  ]),
                );
            }
            throw StateError(request.url.toString());
          },
          roles: ['PARENT'],
          width: width,
          textScale: 1.4,
        );
        expect(
          find.text('Chưa có thực đơn đã công bố trong khoảng ngày này.'),
          findsOneWidget,
        );
        await tap(tester, find.byType(DropdownButtonFormField<String>));
        await tap(tester, find.text('Minh').last);
        await tap(tester, tile('Bữa trưa · 27/11/2026'));
        expect(find.text('Canh bí đỏ'), findsOneWidget);
        expect(find.text('Chưa có'), findsNWidgets(2));
        expect(requested, [
          '/api/parent/students',
          '/api/parent/students/child-a/menus',
          '/api/parent/students/child-b/menus',
        ]);
        expect(tester.takeException(), isNull);
      },
    );
  }
}

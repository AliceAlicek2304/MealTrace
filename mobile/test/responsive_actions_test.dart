import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/app/theme.dart';
import 'package:mealtrace_mobile/core/presentation/meal_widgets.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_widgets.dart';

void main() {
  for (final width in [320.0, 390.0, 768.0]) {
    for (final scale in [1.0, 1.5]) {
      testWidgets(
        'Action group stays together and exposes every operation at $width / $scale',
        (tester) async {
          tester.view.physicalSize = Size(width, 850);
          tester.view.devicePixelRatio = 1;
          tester.platformDispatcher.textScaleFactorTestValue = scale;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
          String? selected;
          final labels = [
            'Thiết lập lịch tuần',
            'Tạo phiên hàng loạt',
            'Lịch sử thay đổi',
          ];
          await tester.pumpWidget(
            MaterialApp(
              theme: mealTraceTheme,
              home: Scaffold(
                body: Padding(
                  padding: const EdgeInsets.all(16),
                  child: MealActions(
                    actions: [
                      for (final label in labels)
                        MealAction(label, () => selected = label),
                    ],
                  ),
                ),
              ),
            ),
          );
          await tester.pumpAndSettle();
          if (find.byType(PopupMenuButton<int>).evaluate().isNotEmpty) {
            await tester.tap(find.byTooltip('Thao tác'));
            await tester.pumpAndSettle();
            expect(find.text(labels.first), findsOneWidget);
            expect(find.text(labels[1]), findsOneWidget);
            await tester.tap(find.text(labels.last));
            await tester.pumpAndSettle();
          } else {
            final tops = [
              for (final label in labels)
                tester.getTopLeft(find.text(label)).dy,
            ];
            expect(tops.every((top) => (top - tops.first).abs() < 1), isTrue);
            await tester.tap(find.text(labels.last));
          }
          expect(selected, labels.last);
          expect(tester.takeException(), isNull);
        },
      );
    }
  }
  testWidgets(
    'Compact record keeps only key details and can expand the full record',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: mealTraceTheme,
          home: Scaffold(
            body: RecordDetails(
              compact: true,
              record: SchoolRecord({
                'date': '2026-10-28',
                'mealType': 'Bữa trưa',
                'className': 'A3',
                'count': 30,
                'schoolYear': '2026-2027',
                'reason': 'Lý do cần đối chiếu đầy đủ',
              }),
            ),
          ),
        ),
      );
      expect(find.textContaining('Lý do cần đối chiếu đầy đủ'), findsNothing);
      expect(find.textContaining('2026-2027'), findsNothing);
      await tester.tap(find.text('Thông tin thêm'));
      await tester.pumpAndSettle();
      expect(find.text('Lý do cần đối chiếu đầy đủ'), findsOneWidget);
      expect(find.text('2026-2027'), findsOneWidget);
      expect(find.textContaining('30'), findsOneWidget);
    },
  );
  testWidgets('Capture compact mobile actions and record for visual review', (
    tester,
  ) async {
    await tester.runAsync(() async {
      final font = File('C:/Windows/Fonts/segoeui.ttf');
      await (FontLoader('Roboto')..addFont(
            font.readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
          ))
          .load();
      final icon = File(
        'C:/Flutter/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
      );
      await (FontLoader('MaterialIcons')..addFont(
            icon.readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
          ))
          .load();
    });
    tester.view.physicalSize = const Size(390, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final capture = GlobalKey();
    await tester.pumpWidget(
      RepaintBoundary(
        key: capture,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: mealTraceTheme,
          home: Scaffold(
            appBar: AppBar(title: const Text('Lịch bữa ăn')),
            body: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                MealActions(
                  actions: [
                    MealAction('Thiết lập lịch tuần', () {}),
                    MealAction('Tạo phiên hàng loạt', () {}),
                    MealAction('Lịch sử thay đổi', () {}),
                  ],
                ),
                const SizedBox(height: 16),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          'Bữa trưa · 28/10/2026',
                          style: mealTraceTheme.textTheme.titleMedium,
                        ),
                        const SizedBox(height: 12),
                        RecordDetails(
                          compact: true,
                          record: SchoolRecord({
                            'date': '2026-10-28',
                            'mealType': 'Bữa trưa',
                            'className': 'A3',
                            'count': 30,
                            'schoolYear': '2026-2027',
                            'reason': 'Lịch phục vụ theo năm học',
                          }),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    Future<void> save(String name) => tester.runAsync(() async {
      final boundary =
          capture.currentContext!.findRenderObject()! as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      final file = File('../be/artifacts/branch-sync-20261028/$name.png');
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
    await save('mobile-compact-actions-390');
    await tester.tap(find.byTooltip('Thao tác'));
    await tester.pumpAndSettle();
    await save('mobile-compact-menu-390');
    await tester.tap(find.text('Thiết lập lịch tuần'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Thông tin thêm'));
    await tester.pumpAndSettle();
    await save('mobile-compact-details-390');
    expect(tester.takeException(), isNull);
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/testing.dart';
import 'package:http/http.dart' as http;
import 'package:mealtrace_mobile/app/mealtrace_app.dart';

import 'package:mealtrace_mobile/features/auth/data/auth_api.dart';
import 'support/auth_fixtures.dart'
    show MemoryStore, sessionJson, jsonResponse, createController;

void main() {
  testWidgets(
    'Small-screen login validates input, opens account and logs out',
    (tester) async {
      tester.view.physicalSize = const Size(320, 700);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final auth = createController(
        AuthApi(
          MockClient(
            (request) async => request.url.path.endsWith('/login')
                ? jsonResponse(sessionJson())
                : http.Response('', 204),
          ),
          'https://example.test/api',
        ),
        MemoryStore(),
      );
      await tester.pumpWidget(MealTraceApp(controller: auth));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Đăng nhập'));
      await tester.pumpAndSettle();
      expect(find.text('Nhập SĐT hoặc email.'), findsOneWidget);
      await tester.enterText(find.byType(TextFormField).at(0), '0349079940');
      await tester.enterText(find.byType(TextFormField).at(1), 'password');
      await tester.ensureVisible(find.text('Đăng nhập'));
      await tester.tap(find.text('Đăng nhập'));
      await tester.pumpAndSettle();
      expect(find.text('Xin chào, Nguyễn An'), findsOneWidget);
      await tester.tap(find.text('Đăng xuất'));
      await tester.pumpAndSettle();
      expect(find.text('Đăng nhập'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      auth.dispose();
    },
  );
}

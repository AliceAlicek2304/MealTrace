import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:http/http.dart' as http;
import 'package:mealtrace_mobile/app/mealtrace_app.dart';
import 'package:mealtrace_mobile/core/config/app_config.dart';
import 'package:mealtrace_mobile/features/auth/data/session_store.dart';

// Run only against a disposable school DB. Test data is retained for inspection.
void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  const identifier = String.fromEnvironment('E2E_ADMIN_IDENTIFIER');
  const password = String.fromEnvironment('E2E_ADMIN_PASSWORD');
  const className = String.fromEnvironment('E2E_CLASS_NAME');
  const isolated = bool.fromEnvironment('E2E_ISOLATED_DATABASE');

  testWidgets(
    'Admin signs in, creates a child, edits profile and verifies persisted data through the API',
    (tester) async {
      expect(
        isolated &&
            identifier.isNotEmpty &&
            password.isNotEmpty &&
            className.isNotEmpty,
        isTrue,
        reason:
            'Provide an isolated test DB, admin credentials and an existing class through dart-define.',
      );
      await SecureSessionStore().clear();
      final client = http.Client();
      addTearDown(client.close);
      addTearDown(() async => SecureSessionStore().clear());
      final name = 'Trẻ E2E ${DateTime.now().microsecondsSinceEpoch}';
      await tester.pumpWidget(const MealTraceApp());
      Future<void> waitFor(Finder finder) async {
        for (var attempt = 0; attempt < 100; attempt++) {
          await tester.pump(const Duration(milliseconds: 200));
          if (finder.evaluate().isNotEmpty) return;
        }
        fail('Timed out waiting for $finder');
      }

      Future<void> tapText(String label) async {
        final finder = find.text(label).last;
        await waitFor(finder);
        await tester.ensureVisible(finder);
        await tester.tap(finder);
        await tester.pumpAndSettle();
      }

      Finder field(String label) => find.byWidgetPredicate(
        (widget) =>
            widget is TextField && widget.decoration?.labelText == label,
      );
      await waitFor(field('Số điện thoại hoặc email'));
      await tester.enterText(field('Số điện thoại hoặc email'), identifier);
      await tester.enterText(field('Mật khẩu'), password);
      await tapText('Đăng nhập');
      await waitFor(find.text('Chức năng'));
      await tapText('Chức năng');
      await tapText('Trẻ');
      await waitFor(find.byType(FloatingActionButton));
      await tester.tap(find.byType(FloatingActionButton));
      await tester.pumpAndSettle();
      await tester.enterText(field('Họ tên trẻ *'), name);
      await tapText('Lớp * · Chưa chọn');
      await tapText(className);
      // Select the current day through the real date picker, then clear it when editing.
      await tester.ensureVisible(find.byTooltip('Chọn ngày sinh'));
      await tester.tap(find.byTooltip('Chọn ngày sinh'));
      await tester.pumpAndSettle();
      await tapText('OK');
      await tester.tap(find.byType(DropdownButtonFormField<String>).first);
      await tester.pumpAndSettle();
      await tapText('Nam');
      await tapText('Thêm trẻ');
      await waitFor(find.byType(FloatingActionButton));
      final search = find.byType(TextField).first;
      await tester.enterText(search, name);
      await tester.pump(const Duration(milliseconds: 400));
      await waitFor(find.text(name));
      final card = find
          .ancestor(of: find.text(name).first, matching: find.byType(Card))
          .first;
      await tester.tap(
        find.descendant(of: card, matching: find.byType(PopupMenuButton<int>)),
      );
      await tester.pumpAndSettle();
      await tapText('Sửa hồ sơ');
      await tester.ensureVisible(find.byTooltip('Xóa ngày sinh'));
      await tester.tap(find.byTooltip('Xóa ngày sinh'));
      await tester.pumpAndSettle();
      await tester.tap(find.byType(DropdownButtonFormField<String>).first);
      await tester.pumpAndSettle();
      await tapText('Nữ');
      await tapText('Sửa hồ sơ trẻ');
      await waitFor(find.byType(FloatingActionButton));

      // Independent API read proves the UI saved the profile, beyond an optimistic UI update.
      final login = await client.post(
        Uri.parse('${AppConfig.apiBaseUrl}/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'identifier': identifier, 'password': password}),
      );
      expect(login.statusCode, 200);
      final token =
          (jsonDecode(login.body) as Map<String, dynamic>)['accessToken'];
      final response = await client.get(
        Uri.parse(
          '${AppConfig.apiBaseUrl}/admin/students',
        ).replace(queryParameters: {'search': name}),
        headers: {'Authorization': 'Bearer $token'},
      );
      expect(response.statusCode, 200);
      final rows =
          (jsonDecode(response.body) as Map<String, dynamic>)['items']
              as List<dynamic>;
      expect(rows, hasLength(1));
      expect(rows.single['gender'], 'FEMALE');
      expect(rows.single['dateOfBirth'], isNull);
      expect(tester.takeException(), isNull);
    },
  );
}

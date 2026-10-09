import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/current_user.dart';
import 'package:mobile/screens/home_page.dart';
import 'package:mobile/services/api.dart';

void main() {
  String? authorization;
  String? requestPath;

  setUp(() {
    authorization = null;
    requestPath = null;
  });

  Api createApi() => Api(
    'http://example.test',
    transport: (uri, method, headers, body) async {
      authorization = headers[HttpHeaders.authorizationHeader];
      requestPath = uri.path;
      if (uri.path == '/api/auth/login') {
        return const ApiResponse(
          HttpStatus.ok,
          '{"accessToken":"test-access-token","user":{"id":"user-id","fullName":"Test User","email":"test@example.com","roles":["ADMIN"]}}',
        );
      }
      if (uri.path == '/api/meal-days/workflow') {
        return const ApiResponse(
          HttpStatus.ok,
          '{"items":[],"total":0,"page":1,"pageSize":20}',
        );
      }
      return const ApiResponse(HttpStatus.noContent, '');
    },
  )..token = 'test-access-token';

  test('sends the access token as a Bearer authorization header', () async {
    final api = createApi();

    await api.request('GET', '/auth/me');

    expect(authorization, 'Bearer test-access-token');
  });

  for (final role in ['ADMIN', 'TEACHER']) {
    testWidgets('$role can load the workflow list response', (tester) async {
      final api = createApi();
      await tester.pumpWidget(
        MaterialApp(
          home: HomePage(
            api: api,
            user: CurrentUser.fromJson({
              'id': 'user-id',
              'fullName': 'Test User',
              'email': 'test@example.com',
              'roles': [role],
            }),
            onLogout: () {},
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(requestPath, '/api/meal-days/workflow');
      expect(authorization, 'Bearer test-access-token');
      expect(
        find.text('Không tải được dữ liệu. Hãy thử tải lại.'),
        findsNothing,
      );
      await tester.tap(find.text('Số suất'));
      await tester.pumpAndSettle();
      expect(find.text('Chưa có phiên ăn'), findsOneWidget);
    });
  }

  testWidgets('logout menu invokes the logout action', (tester) async {
    var loggedOut = false;
    await tester.pumpWidget(
      MaterialApp(
        home: HomePage(
          api: createApi(),
          user: CurrentUser.fromJson({'roles': []}),
          onLogout: () => loggedOut = true,
        ),
      ),
    );
    await tester.tap(find.byType(PopupMenuButton<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Đăng xuất'));
    await tester.pumpAndSettle();

    expect(loggedOut, isTrue);
    expect(requestPath, '/api/auth/logout');
    expect(authorization, 'Bearer test-access-token');
  });
}

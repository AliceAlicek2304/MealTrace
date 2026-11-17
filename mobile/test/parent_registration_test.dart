import 'dart:async';
import 'dart:convert';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/auth/data/auth_api.dart';
import 'package:mealtrace_mobile/features/auth/data/auth_repository_impl.dart';
import 'package:mealtrace_mobile/features/auth/domain/parent_registration.dart';
import 'package:mealtrace_mobile/features/auth/presentation/auth_controller.dart';
import 'package:mealtrace_mobile/features/auth/presentation/login_page.dart';
import 'package:mealtrace_mobile/features/auth/presentation/register_page.dart';
import 'support/auth_fixtures.dart';
import 'package:mealtrace_mobile/app/theme.dart';

// Ephemeral input for mocked signup, not a real account credential.
final input = ParentRegistration(
  ' Phụ huynh ',
  '+84 901234567',
  '${base64Url.encode(List<int>.generate(16, (_) => Random.secure().nextInt(256)))}Aa1!',
  challengeId: 'challenge-1',
  otpCode: '123456',
);
void main() {
  testWidgets('Invalid phone does not start a resend cooldown', (tester) async {
    var requests = 0;
    final auth = createController(
      AuthApi(
        MockClient((_) async {
          requests++;
          return http.Response('', 400);
        }),
        'https://example.test/api',
      ),
      MemoryStore(),
    );
    await auth.restore();
    await tester.pumpWidget(MaterialApp(home: RegisterPage(auth: auth)));
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Số điện thoại'),
      '123',
    );
    final send = find.text('Gửi OTP qua WhatsApp');
    await tester.ensureVisible(send);
    await tester.tap(send);
    await tester.pumpAndSettle();
    expect(requests, 0);
    expect(find.textContaining('Gửi lại sau'), findsNothing);
    expect(
      tester.widget<OutlinedButton>(find.byType(OutlinedButton)).onPressed,
      isNotNull,
    );
    await tester.pumpWidget(const SizedBox());
    auth.dispose();
  });
  testWidgets('OTP delivery has its own progress label and focuses the code', (
    tester,
  ) async {
    final reply = Completer<http.Response>();
    final auth = createController(
      AuthApi(MockClient((_) => reply.future), 'https://example.test/api'),
      MemoryStore(),
    );
    await auth.restore();
    await tester.pumpWidget(MaterialApp(home: RegisterPage(auth: auth)));
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Số điện thoại'),
      input.phoneNumber,
    );
    final send = find.text('Gửi OTP qua WhatsApp');
    await tester.ensureVisible(send);
    await tester.tap(send);
    await tester.pump();
    expect(find.text('Đang gửi OTP…'), findsOneWidget);
    expect(find.text('Đang đăng ký…'), findsNothing);
    reply.complete(
      jsonResponse({
        'challengeId': 'challenge-1',
        'expiresAt': DateTime.now()
            .add(const Duration(minutes: 5))
            .toIso8601String(),
        'resendAt': DateTime.now()
            .add(const Duration(seconds: 90))
            .toIso8601String(),
        'message': 'OTP đã gửi.',
      }),
    );
    await tester.pumpAndSettle();
    final field = tester.widget<TextField>(
      find.descendant(
        of: find.widgetWithText(TextFormField, 'Mã OTP WhatsApp'),
        matching: find.byType(TextField),
      ),
    );
    expect(field.focusNode!.hasFocus, isTrue);
    expect(find.textContaining('Gửi lại sau 9'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    auth.dispose();
  });
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets(
      'Signup remains usable with large text and keyboard at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 700);
        tester.view.devicePixelRatio = 1;
        tester.view.viewInsets = const FakeViewPadding(bottom: 280);
        tester.platformDispatcher.textScaleFactorTestValue = 1.5;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        addTearDown(tester.view.resetViewInsets);
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        final auth = createController(
          AuthApi(
            MockClient(
              (_) async => jsonResponse({
                'challengeId': 'keyboard-code',
                'expiresAt': DateTime.now()
                    .add(const Duration(minutes: 5))
                    .toIso8601String(),
                'resendAt': DateTime.now()
                    .add(const Duration(seconds: 60))
                    .toIso8601String(),
                'message': 'Mã xác nhận đã được gửi.',
              }),
            ),
            'https://example.test/api',
          ),
          MemoryStore(),
        );
        await auth.restore();
        await tester.pumpWidget(
          MaterialApp(
            theme: mealTraceTheme,
            home: RegisterPage(auth: auth),
          ),
        );
        final phone = find.widgetWithText(TextFormField, 'Số điện thoại');
        await tester.scrollUntilVisible(
          phone,
          140,
          scrollable: find.byWidgetPredicate(
            (widget) =>
                widget is Scrollable &&
                widget.axisDirection == AxisDirection.down,
          ),
        );
        await tester.enterText(phone, input.phoneNumber);
        final send = find.text('Gửi OTP qua WhatsApp');
        await tester.ensureVisible(send);
        await tester.pumpAndSettle();
        await tester.tap(send);
        await tester.pumpAndSettle();
        final otp = find.widgetWithText(TextFormField, 'Mã OTP WhatsApp');
        await tester.ensureVisible(otp);
        await tester.pumpAndSettle();
        await tester.enterText(otp, '123456');
        expect(tester.getRect(otp).bottom, lessThanOrEqualTo(420));
        expect(
          tester
              .getRect(
                find.widgetWithText(FilledButton, 'Tạo tài khoản phụ huynh'),
              )
              .bottom,
          lessThanOrEqualTo(420),
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
        auth.dispose();
      },
    );
  }
  test(
    'OTP request sends phone only and leaves session storage empty',
    () async {
      final store = MemoryStore();
      final repository = AuthRepositoryImpl(
        AuthApi(
          MockClient((request) async {
            expect(request.url.path, '/api/auth/register/otp');
            expect(jsonDecode(request.body), {'phoneNumber': '0901234567'});
            expect(request.headers.containsKey('Authorization'), false);
            return jsonResponse({
              'challengeId': 'challenge-1',
              'expiresAt': DateTime.now()
                  .add(const Duration(minutes: 5))
                  .toIso8601String(),
              'resendAt': DateTime.now()
                  .add(const Duration(seconds: 60))
                  .toIso8601String(),
              'message': 'OTP đã gửi.',
            });
          }),
          'https://example.com/api',
        ),
        store,
      );
      final challenge = await repository.requestParentOtp('0901234567');
      expect(challenge.id, 'challenge-1');
      expect(store.value, isNull);
      repository.dispose();
    },
  );
  test('Signup without OTP proof never contacts the server', () async {
    var requests = 0;
    final auth = createController(
      AuthApi(
        MockClient((_) async {
          requests++;
          return http.Response('', 204);
        }),
        'https://example.com/api',
      ),
      MemoryStore(),
    );
    await auth.restore();
    expect(
      await auth.registerParent(
        ParentRegistration(input.fullName, input.phoneNumber, input.password),
      ),
      false,
    );
    expect(requests, 0);
    expect(auth.message, contains('OTP'));
    auth.dispose();
  });
  test('OTP delivery errors keep the parent signed out', () async {
    final auth = createController(
      AuthApi(
        MockClient(
          (_) async => http.Response(
            jsonEncode({'message': 'Chưa gửi được OTP WhatsApp.'}),
            400,
            headers: {'content-type': 'application/json; charset=utf-8'},
          ),
        ),
        'https://example.com/api',
      ),
      MemoryStore(),
    );
    await auth.restore();
    expect(await auth.requestParentOtp(input.phoneNumber), isNull);
    expect(auth.message, 'Chưa gửi được OTP WhatsApp.');
    expect(auth.status, AuthStatus.signedOut);
    auth.dispose();
  });
  test('Signup projects only public fields and stores no session', () async {
    final store = MemoryStore();
    final repository = AuthRepositoryImpl(
      AuthApi(
        MockClient((request) async {
          expect(request.url.path, '/api/auth/register');
          expect(request.headers.containsKey('Authorization'), false);
          expect(jsonDecode(request.body), {
            'fullName': 'Phụ huynh',
            'phoneNumber': '0901234567',
            'password': input.password,
            'challengeId': input.challengeId,
            'otpCode': input.otpCode,
          });
          return http.Response('', 204);
        }),
        'https://example.com/api',
      ),
      store,
    );
    await repository.registerParent(input);
    expect(store.value, isNull);
    expect(repository.accessToken, isNull);
    repository.dispose();
  });
  test(
    'Pending signup rejects repeated submission and never signs in',
    () async {
      final response = Completer<http.Response>();
      var requests = 0;
      final auth = createController(
        AuthApi(
          MockClient((_) {
            requests++;
            return response.future;
          }),
          'https://example.com/api',
        ),
        MemoryStore(),
      );
      await auth.restore();
      final first = auth.registerParent(input);
      expect(await auth.registerParent(input), false);
      expect(auth.busy, true);
      response.complete(http.Response('', 204));
      expect(await first, true);
      expect(requests, 1);
      expect(auth.status, AuthStatus.signedOut);
      expect(auth.user, isNull);
      auth.dispose();
    },
  );
  test('Duplicate phone is shown and credentials remain unsaved', () async {
    final store = MemoryStore();
    final auth = createController(
      AuthApi(
        MockClient(
          (_) async => http.Response(
            jsonEncode({'message': 'SĐT đã có tài khoản.'}),
            409,
            headers: {'content-type': 'application/json; charset=utf-8'},
          ),
        ),
        'https://example.com/api',
      ),
      store,
    );
    await auth.restore();
    expect(await auth.registerParent(input), false);
    expect(auth.message, 'SĐT đã có tài khoản.');
    expect(store.value, isNull);
    auth.dispose();
  });
  for (final width in [320.0, 390.0, 768.0]) {
    testWidgets('Signup returns normalized phone to login at width $width', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 850);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final auth = createController(
        AuthApi(
          MockClient(
            (request) async => request.url.path.endsWith('/otp')
                ? jsonResponse({
                    'challengeId': 'challenge-1',
                    'expiresAt': DateTime.now()
                        .add(const Duration(minutes: 5))
                        .toIso8601String(),
                    'resendAt': DateTime.now()
                        .add(const Duration(seconds: 60))
                        .toIso8601String(),
                    'message': 'OTP đã gửi.',
                  })
                : http.Response('', 204),
          ),
          'https://example.com/api',
        ),
        MemoryStore(),
      );
      await auth.restore();
      await tester.pumpWidget(MaterialApp(home: LoginPage(auth: auth)));
      final signup = find.text('Đăng ký tài khoản phụ huynh');
      await tester.ensureVisible(signup);
      await tester.pumpAndSettle();
      await tester.tap(signup);
      await tester.pumpAndSettle();
      Future<void> fill(String label, String value) async {
        final field = find.widgetWithText(TextFormField, label);
        if (field.evaluate().isEmpty) {
          await tester.scrollUntilVisible(
            field,
            160,
            scrollable: find
                .byWidgetPredicate(
                  (widget) =>
                      widget is Scrollable &&
                      widget.axisDirection == AxisDirection.down,
                )
                .last,
          );
        }
        await tester.ensureVisible(field);
        await tester.pumpAndSettle();
        await tester.enterText(field, value);
        await tester.pump();
      }

      await fill('Họ tên', input.fullName);
      await fill('Số điện thoại', input.phoneNumber);
      final send = find.text('Gửi OTP qua WhatsApp');
      await tester.ensureVisible(send);
      await tester.pumpAndSettle();
      await tester.tap(send);
      await tester.pumpAndSettle();
      await fill('Mã OTP WhatsApp', input.otpCode!);
      await fill('Mật khẩu', input.password);
      await fill('Nhập lại mật khẩu', input.password);
      await tester.ensureVisible(find.text('Tạo tài khoản phụ huynh'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Tạo tài khoản phụ huynh'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Đăng ký thành công'), findsOneWidget);
      expect(find.text('0901234567'), findsOneWidget);
      expect(auth.status, AuthStatus.signedOut);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      auth.dispose();
    });
  }
}

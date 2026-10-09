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
import 'support/auth_fixtures.dart';

// Ephemeral input for mocked signup, not a real account credential.
final input = ParentRegistration(
  ' Phụ huynh ',
  '+84 901234567',
  '${base64Url.encode(List<int>.generate(16, (_) => Random.secure().nextInt(256)))}Aa1!',
  challengeId: 'challenge-1',
  otpCode: '123456',
);
void main() {
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
      await tester.tap(signup);
      await tester.pumpAndSettle();
      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), input.fullName);
      await tester.enterText(fields.at(1), input.phoneNumber);
      final send = find.text('Gửi OTP qua WhatsApp');
      await tester.ensureVisible(send);
      await tester.tap(send);
      await tester.pumpAndSettle();
      await tester.enterText(fields.at(2), input.otpCode!);
      await tester.enterText(fields.at(3), input.password);
      await tester.enterText(fields.at(4), input.password);
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

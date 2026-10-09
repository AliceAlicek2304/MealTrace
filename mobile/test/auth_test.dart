import 'dart:async';
import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mealtrace_mobile/features/auth/presentation/auth_controller.dart';
import 'package:mealtrace_mobile/features/auth/data/auth_api.dart';
import 'support/auth_fixtures.dart';

void main() {
  testWidgets(
    'Expiry pending during failed logout is handled once logout finishes',
    (tester) async {
      final store = OnceBlockedClearStore();
      final auth = createController(
        AuthApi(
          MockClient(
            (_) async => jsonResponse({
              ...sessionJson(),
              'expiresAt': DateTime.now()
                  .add(const Duration(milliseconds: 100))
                  .toIso8601String(),
            }),
          ),
          'https://example.test/api',
        ),
        store,
      );
      addTearDown(auth.dispose);
      await auth.restore();
      await auth.login('0349079940', 'password');
      final logout = auth.logout();
      await tester.pump(const Duration(seconds: 1));
      store.firstClear.completeError(StateError('storage temporarily locked'));
      await logout;
      await tester.pump();
      expect(auth.status, AuthStatus.signedOut);
      expect(auth.user, isNull);
      expect(store.value, isNull);
    },
  );
  test(
    'Disposing during login ignores late response and does not save credentials',
    () async {
      final response = Completer<http.Response>();
      final store = MemoryStore();
      final auth = createController(
        AuthApi(MockClient((_) => response.future), 'https://example.test/api'),
        store,
      );
      await auth.restore();
      final login = auth.login('0349079940', 'password');
      auth.dispose();
      response.complete(jsonResponse(sessionJson()));
      await login;
      expect(store.value, isNull);
      expect(auth.user, isNull);
    },
  );

  test('Repeated submit sends only one login request while busy', () async {
    final response = Completer<http.Response>();
    var count = 0;
    final auth = createController(
      AuthApi(
        MockClient((_) {
          count++;
          return response.future;
        }),
        'https://example.test/api',
      ),
      MemoryStore(),
    );
    addTearDown(auth.dispose);
    await auth.restore();
    final first = auth.login('0349079940', 'password');
    await auth.login('0349079940', 'password');
    response.complete(jsonResponse(sessionJson()));
    await first;
    expect(count, 1);
    expect(auth.status, AuthStatus.signedIn);
  });

  test(
    'Malformed login response is rejected without storing a token',
    () async {
      final store = MemoryStore();
      final auth = createController(
        AuthApi(
          MockClient((_) async => jsonResponse({'accessToken': 'partial'})),
          'https://example.test/api',
        ),
        store,
      );
      addTearDown(auth.dispose);
      await auth.restore();
      await auth.login('0349079940', 'password');
      expect(store.value, isNull);
      expect(auth.status, AuthStatus.signedOut);
      expect(auth.message, contains('không hợp lệ'));
    },
  );
  testWidgets(
    'Expiry hides account before slow secure-storage cleanup completes',
    (tester) async {
      final store = DelayedClearStore();
      final auth = createController(
        AuthApi(
          MockClient(
            (_) async => jsonResponse({
              ...sessionJson(),
              'expiresAt': DateTime.now()
                  .add(const Duration(milliseconds: 100))
                  .toIso8601String(),
            }),
          ),
          'https://example.test/api',
        ),
        store,
      );
      addTearDown(auth.dispose);
      await auth.restore();
      await auth.login('0349079940', 'password');
      await tester.pump(const Duration(seconds: 1));
      expect(auth.status, isNot(AuthStatus.signedIn));
      store.cleared.complete();
      await tester.pump();
    },
  );
  test(
    'Phone login preserves password, stores only token/expiry and logout clears session',
    () async {
      final store = MemoryStore();
      final requests = <http.Request>[];
      final auth = createController(
        AuthApi(
          MockClient((request) async {
            requests.add(request);
            return request.url.path.endsWith('/login')
                ? jsonResponse(sessionJson())
                : http.Response('', 204);
          }),
          'https://example.test/api',
        ),
        store,
      );
      addTearDown(auth.dispose);
      await auth.restore();
      await auth.login(' 0349079940 ', ' password with spaces ');
      expect(jsonDecode(requests.first.body), {
        'identifier': '0349079940',
        'password': ' password with spaces ',
      });
      expect(auth.status, AuthStatus.signedIn);
      expect(auth.user!.fullName, 'Nguyễn An');
      expect(
        (jsonDecode(store.value!) as Map).keys,
        unorderedEquals(['token', 'expiresAt']),
      );
      await auth.logout();
      expect(store.value, isNull);
      expect(auth.status, AuthStatus.signedOut);
      expect(requests.last.headers['Authorization'], 'Bearer test-token');
    },
  );

  test('Rejected saved token is erased and user must sign in again', () async {
    final store = MemoryStore()
      ..value = jsonEncode({
        'token': 'old',
        'expiresAt': DateTime.now()
            .add(const Duration(hours: 1))
            .toIso8601String(),
      });
    final auth = createController(
      AuthApi(
        MockClient((_) async => http.Response('', 401)),
        'https://example.test/api',
      ),
      store,
    );
    addTearDown(auth.dispose);
    await auth.restore();
    expect(store.value, isNull);
    expect(auth.status, AuthStatus.signedOut);
    expect(auth.user, isNull);
  });

  test(
    'Offline restore preserves stored token and successful retry loads current user',
    () async {
      final store = MemoryStore()
        ..value = jsonEncode({
          'token': 'saved',
          'expiresAt': DateTime.now()
              .add(const Duration(hours: 1))
              .toIso8601String(),
        });
      var offline = true;
      final auth = createController(
        AuthApi(
          MockClient((request) async {
            expect(request.headers['Authorization'], 'Bearer saved');
            if (offline) throw http.ClientException('offline');
            return jsonResponse(userJson);
          }),
          'https://example.test/api',
        ),
        store,
      );
      addTearDown(auth.dispose);
      await auth.restore();
      expect(auth.status, AuthStatus.restoreFailed);
      expect(auth.user, isNull);
      expect(store.value, isNotNull);
      offline = false;
      await auth.restore();
      expect(auth.status, AuthStatus.signedIn);
      expect(auth.user!.roles, ['PARENT']);
    },
  );

  test('Expired token is cleared without contacting server', () async {
    final store = MemoryStore()
      ..value = jsonEncode({
        'token': 'expired',
        'expiresAt': DateTime.now()
            .subtract(const Duration(minutes: 1))
            .toIso8601String(),
      });
    final auth = createController(
      AuthApi(
        MockClient((_) async => throw StateError('No request expected')),
        'https://example.test/api',
      ),
      store,
    );
    addTearDown(auth.dispose);
    await auth.restore();
    expect(auth.status, AuthStatus.signedOut);
    expect(store.value, isNull);
  });

  test('Wrong password does not create a saved session', () async {
    final store = MemoryStore();
    final auth = createController(
      AuthApi(
        MockClient((_) async => http.Response('', 401)),
        'https://example.test/api',
      ),
      store,
    );
    addTearDown(auth.dispose);
    await auth.restore();
    await auth.login('parent@example.test', 'wrong');
    expect(auth.status, AuthStatus.signedOut);
    expect(auth.message, contains('không đúng'));
    expect(store.value, isNull);
  });

  test(
    'Storage failure prevents entering app and attempts server logout',
    () async {
      final store = MemoryStore()..failWrite = true;
      final paths = <String>[];
      final auth = createController(
        AuthApi(
          MockClient((request) async {
            paths.add(request.url.path);
            return request.url.path.endsWith('/login')
                ? jsonResponse(sessionJson())
                : http.Response('', 204);
          }),
          'https://example.test/api',
        ),
        store,
      );
      addTearDown(auth.dispose);
      await auth.restore();
      await auth.login('0349079940', 'password');
      expect(auth.status, AuthStatus.signedOut);
      expect(auth.user, isNull);
      expect(paths.last, '/api/auth/logout');
    },
  );
}

import 'dart:io';

import 'package:http/http.dart' as http;
import 'package:mealtrace_mobile/features/auth/data/auth_api.dart';
import 'package:mealtrace_mobile/features/auth/domain/auth_models.dart';

Future<void> main() async {
  final baseUrl =
      Platform.environment['API_BASE_URL'] ?? 'http://localhost:5184/api';
  final identifier = Platform.environment['AUTH_LIVE_IDENTIFIER'] ?? '';
  final password = Platform.environment['AUTH_LIVE_PASSWORD'] ?? '';
  if (identifier.isEmpty || password.isEmpty) {
    throw StateError(
      'Set AUTH_LIVE_IDENTIFIER and AUTH_LIVE_PASSWORD in the process environment.',
    );
  }

  final client = http.Client();
  final api = AuthApi(client, baseUrl);
  try {
    try {
      await api.login(identifier, 'intentionally-wrong-password');
      throw StateError('Expected the invalid password to be rejected.');
    } on AuthFailure catch (error) {
      if (!error.unauthorized) rethrow;
    }

    final session = await api.login(identifier, password);
    final user = await api.currentUser(session.token);
    if (!user.roles.contains('ADMIN')) {
      throw StateError('The test account does not have the ADMIN role.');
    }

    await api.logout(session.token);
    try {
      await api.currentUser(session.token);
      throw StateError('The session token remained active after logout.');
    } on AuthFailure catch (error) {
      if (!error.unauthorized) rethrow;
    }

    stdout.writeln(
      'Live auth check passed: invalid password, login, current user, and logout.',
    );
  } finally {
    api.dispose();
  }
}

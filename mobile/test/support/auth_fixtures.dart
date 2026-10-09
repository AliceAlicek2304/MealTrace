import 'dart:convert';
import 'dart:async';

import 'package:http/http.dart' as http;

import 'package:mealtrace_mobile/features/auth/presentation/auth_controller.dart';
import 'package:mealtrace_mobile/features/auth/data/auth_api.dart';
import 'package:mealtrace_mobile/features/auth/data/session_store.dart';
import 'package:mealtrace_mobile/features/auth/data/auth_repository_impl.dart';

class MemoryStore implements SessionStore {
  String? value;
  bool failWrite = false;
  @override
  Future<String?> read() async => value;
  @override
  Future<void> write(String value) async {
    if (failWrite) throw StateError('storage unavailable');
    this.value = value;
  }

  @override
  Future<void> clear() async {
    value = null;
  }
}

class DelayedClearStore extends MemoryStore {
  final cleared = Completer<void>();
  @override
  Future<void> clear() async {
    await cleared.future;
    await super.clear();
  }
}

class OnceBlockedClearStore extends MemoryStore {
  final firstClear = Completer<void>();
  int clearCount = 0;
  @override
  Future<void> clear() async {
    clearCount++;
    if (clearCount == 1) await firstClear.future;
    await super.clear();
  }
}

final userJson = {
  'id': 'user-1',
  'fullName': 'Nguyễn An',
  'email': '',
  'phoneNumber': '0349079940',
  'roles': ['PARENT'],
};
Map<String, dynamic> sessionJson() => {
  'accessToken': 'test-token',
  'expiresAt': DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
  'user': userJson,
};
http.Response jsonResponse(Object value) => http.Response(
  jsonEncode(value),
  200,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

AuthController createController(AuthApi api, SessionStore store) =>
    AuthController(AuthRepositoryImpl(api, store));

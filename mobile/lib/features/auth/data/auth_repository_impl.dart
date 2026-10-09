import 'dart:convert';

import '../domain/auth_models.dart';
import '../domain/auth_repository.dart';
import 'auth_api.dart';
import 'session_store.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._api, this._store);
  final AuthApi _api;
  final SessionStore _store;
  String? _token;
  bool _closed = false;
  // Used only by the composition root to authorize business repositories.
  String? get accessToken => _closed ? null : _token;

  void _ensureOpen() {
    if (_closed) throw const AuthFailure('Yêu cầu đã kết thúc.');
  }

  @override
  Future<LoginSession?> restore() async {
    _ensureOpen();
    final String? raw;
    try {
      raw = await _store.read();
    } catch (_) {
      throw const AuthFailure(
        'Không thể đọc phiên trên thiết bị. Vui lòng thử lại.',
      );
    }
    _ensureOpen();
    if (raw == null) {
      _token = null;
      return null;
    }
    final String token;
    final DateTime expiry;
    try {
      final json = jsonDecode(raw) as Map<String, dynamic>;
      token = json['token'] as String;
      expiry = DateTime.parse(json['expiresAt'] as String);
      if (token.isEmpty) throw const FormatException();
    } catch (_) {
      await clearLocalSession();
      throw const AuthFailure(
        'Phiên đã lưu không hợp lệ. Vui lòng đăng nhập lại.',
        unauthorized: true,
      );
    }
    if (!expiry.isAfter(DateTime.now())) {
      await clearLocalSession();
      throw const AuthFailure(
        'Phiên đã hết hạn. Vui lòng đăng nhập lại.',
        unauthorized: true,
      );
    }
    _token = token;
    try {
      final user = await _api.currentUser(token);
      _ensureOpen();
      if (!expiry.isAfter(DateTime.now())) {
        throw const AuthFailure('Phiên đã hết hạn.', unauthorized: true);
      }
      return LoginSession(token, expiry, user);
    } on AuthFailure catch (error) {
      if (error.unauthorized && !_closed) await clearLocalSession();
      rethrow;
    }
  }

  @override
  Future<LoginSession> login(String identifier, String password) async {
    _ensureOpen();
    final session = await _api.login(identifier, password);
    _ensureOpen();
    try {
      await _store.write(
        jsonEncode({
          'token': session.token,
          'expiresAt': session.expiresAt.toIso8601String(),
        }),
      );
    } catch (_) {
      try {
        await _api.logout(session.token);
      } catch (_) {
        /* Server token will expire if revocation is unavailable. */
      }
      throw const AuthFailure(
        'Không thể lưu phiên an toàn trên thiết bị. Vui lòng thử lại.',
      );
    }
    _ensureOpen();
    _token = session.token;
    return session;
  }

  @override
  Future<void> clearLocalSession() async {
    _ensureOpen();
    try {
      await _store.clear();
    } catch (_) {
      throw const AuthFailure(
        'Không thể xóa phiên trên thiết bị. Vui lòng thử lại.',
      );
    }
    _token = null;
  }

  @override
  Future<LogoutResult> logout() async {
    final token = _token;
    await clearLocalSession();
    if (token == null) return LogoutResult.complete;
    try {
      await _api.logout(token);
      return LogoutResult.complete;
    } on AuthFailure catch (error) {
      return error.unauthorized
          ? LogoutResult.complete
          : LogoutResult.localOnly;
    }
  }

  @override
  void dispose() {
    _closed = true;
    _api.dispose();
  }
}

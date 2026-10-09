import '../domain/parent_otp_challenge.dart';
import '../domain/parent_registration.dart';
import 'dart:async';
import 'package:flutter/foundation.dart';
import '../domain/auth_models.dart';
import '../domain/auth_repository.dart';

enum AuthStatus { restoring, signedOut, signedIn, restoreFailed }

/// Owns UI session state. HTTP/JSON/storage are the repository's responsibility.
class AuthController extends ChangeNotifier {
  AuthController(this._repository);
  final AuthRepository _repository;
  AuthStatus _status = AuthStatus.restoring;
  CurrentUser? _user;
  bool _busy = false;
  String? _message;
  Timer? _expiryTimer;
  bool _disposed = false;
  bool _expiryPending = false;

  AuthStatus get status => _status;
  CurrentUser? get user => _user;
  bool get busy => _busy;
  String? get message => _message;

  Future<void> invalidateSession() => _expire();

  void _changed() {
    if (!_disposed) notifyListeners();
  }

  void _finishOperation() {
    _busy = false;
    _changed();
    final expired = _expiryPending;
    _expiryPending = false;
    if (!_disposed && expired && _status == AuthStatus.signedIn) {
      unawaited(_expire());
    }
  }

  void _accept(LoginSession session) {
    _user = session.user;
    _status = AuthStatus.signedIn;
    _expiryTimer?.cancel();
    _expiryTimer = Timer(session.expiresAt.difference(DateTime.now()), _expire);
  }

  Future<void> _expire() async {
    if (_disposed) return;
    if (_busy) {
      _expiryPending = true;
      return;
    }
    _busy = true;
    // Hide the account before awaiting storage; signedIn always has a user.
    _status = AuthStatus.restoring;
    _user = null;
    _message = 'Phiên đã hết hạn. Vui lòng đăng nhập lại.';
    _changed();
    try {
      await _repository.clearLocalSession();
      if (_disposed) return;
      _status = AuthStatus.signedOut;
    } on AuthFailure catch (error) {
      if (_disposed) return;
      _status = AuthStatus.restoreFailed;
      _message = error.message;
    } finally {
      _finishOperation();
    }
  }

  Future<void> restore({bool preserveSessionView = false}) async {
    if (_disposed || _busy) return;
    final preserve = preserveSessionView && _status == AuthStatus.signedIn;
    _busy = true;
    if (!preserve) {
      _status = AuthStatus.restoring;
      _user = null;
      _expiryTimer?.cancel();
    }
    _message = null;
    _changed();
    try {
      final session = await _repository.restore();
      if (_disposed) return;
      if (session == null) {
        _expiryTimer?.cancel();
        _user = null;
        _status = AuthStatus.signedOut;
      } else {
        _accept(session);
      }
    } on AuthFailure catch (error) {
      if (_disposed) return;
      _expiryTimer?.cancel();
      _user = null;
      _message = error.message;
      _status = error.unauthorized
          ? AuthStatus.signedOut
          : AuthStatus.restoreFailed;
    } finally {
      _finishOperation();
    }
  }

  void clearMessage() {
    if (_disposed || _busy) return;
    _message = null;
    _changed();
  }

  Future<ParentOtpChallenge?> requestParentOtp(String phone) async {
    if (_disposed || _busy || _status != AuthStatus.signedOut) return null;
    final normalized = ParentRegistration('', phone, '').normalizedPhone;
    if (normalized == null) {
      _message = 'SĐT Việt Nam không hợp lệ.';
      _changed();
      return null;
    }
    _busy = true;
    _message = null;
    _changed();
    try {
      final result = await _repository.requestParentOtp(normalized);
      return _disposed ? null : result;
    } on AuthFailure catch (error) {
      if (!_disposed) _message = error.message;
      return null;
    } finally {
      _finishOperation();
    }
  }

  Future<bool> registerParent(ParentRegistration input) async {
    if (_disposed || _busy || _status != AuthStatus.signedOut) return false;
    _message = input.validate(requireOtp: true);
    if (_message != null) {
      _changed();
      return false;
    }
    _busy = true;
    _changed();
    try {
      await _repository.registerParent(input);
      return !_disposed;
    } on AuthFailure catch (error) {
      if (!_disposed) _message = error.message;
      return false;
    } finally {
      _finishOperation();
    }
  }

  Future<void> login(String identifier, String password) async {
    if (_disposed || _busy || _status != AuthStatus.signedOut) return;
    if (identifier.trim().isEmpty || password.isEmpty) {
      _message = 'Nhập SĐT/email và mật khẩu.';
      _changed();
      return;
    }
    _busy = true;
    _message = null;
    _changed();
    try {
      final session = await _repository.login(identifier, password);
      if (_disposed) return;
      _accept(session);
    } on AuthFailure catch (error) {
      if (!_disposed) _message = error.message;
    } finally {
      _finishOperation();
    }
  }

  Future<void> logout() async {
    if (_disposed || _busy) return;
    _busy = true;
    _message = null;
    _changed();
    try {
      final result = await _repository.logout();
      if (_disposed) return;
      _expiryTimer?.cancel();
      _user = null;
      _status = AuthStatus.signedOut;
      if (result == LogoutResult.localOnly) {
        _message =
            'Đã đăng xuất trên thiết bị. Chưa thể kết thúc phiên trên máy chủ.';
      }
    } on AuthFailure catch (error) {
      if (!_disposed) _message = error.message;
    } finally {
      _finishOperation();
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _expiryTimer?.cancel();
    _repository.dispose();
    super.dispose();
  }
}

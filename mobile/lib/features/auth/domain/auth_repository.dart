import 'auth_models.dart';

enum LogoutResult { complete, localOnly }

abstract interface class AuthRepository {
  Future<LoginSession?> restore();
  Future<LoginSession> login(String identifier, String password);
  Future<LogoutResult> logout();
  Future<void> clearLocalSession();
  void dispose();
}

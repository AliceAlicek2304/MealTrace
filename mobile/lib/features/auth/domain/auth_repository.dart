import 'parent_otp_challenge.dart';
import 'parent_registration.dart';
import 'auth_models.dart';

enum LogoutResult { complete, localOnly }

abstract interface class AuthRepository {
  Future<LoginSession?> restore();
  Future<ParentOtpChallenge> requestParentOtp(String phone);
  Future<void> registerParent(ParentRegistration input);
  Future<LoginSession> login(String identifier, String password);
  Future<LogoutResult> logout();
  Future<void> clearLocalSession();
  void dispose();
}

import 'package:http/http.dart' as http;
import 'package:flutter/foundation.dart';
import '../core/config/app_config.dart';
import '../features/auth/data/auth_api.dart';
import '../features/auth/data/auth_repository_impl.dart';
import '../features/auth/data/session_store.dart';
import '../features/auth/presentation/auth_controller.dart';
import '../features/school/data/school_repository_impl.dart';
import '../features/school/domain/school_models.dart';

class ApplicationDependencies {
  ApplicationDependencies(this.auth, this.school);
  final AuthController auth;
  final SchoolRepository school;
}

ApplicationDependencies createApplicationDependencies() {
  final baseUrl = AppConfig.validateApiBaseUrl(
    AppConfig.apiBaseUrl,
    allowHttp: kDebugMode,
  );
  final authRepository = AuthRepositoryImpl(
    AuthApi(http.Client(), baseUrl),
    SecureSessionStore(),
  );
  return ApplicationDependencies(
    AuthController(authRepository),
    SchoolRepositoryImpl(
      http.Client(),
      baseUrl,
      () => authRepository.accessToken,
    ),
  );
}

AuthController createAuthController() {
  final baseUrl = AppConfig.validateApiBaseUrl(
    AppConfig.apiBaseUrl,
    allowHttp: kDebugMode,
  );
  return AuthController(
    AuthRepositoryImpl(AuthApi(http.Client(), baseUrl), SecureSessionStore()),
  );
}

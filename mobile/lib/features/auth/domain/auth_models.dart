class AuthFailure implements Exception {
  const AuthFailure(this.message, {this.unauthorized = false});
  final String message;
  final bool unauthorized;
}

class CurrentUser {
  CurrentUser({
    required this.id,
    required this.fullName,
    required this.email,
    required List<String> roles,
    this.phoneNumber,
    this.inspectorAccessUntil,
  }) : roles = List.unmodifiable(roles);
  final String id;
  final String fullName;
  final String email;
  final String? phoneNumber;
  final String? inspectorAccessUntil;
  final List<String> roles;
}

class LoginSession {
  const LoginSession(this.token, this.expiresAt, this.user);
  final String token;
  final DateTime expiresAt;
  final CurrentUser user;
}

class CurrentUser {
  CurrentUser.fromJson(Map<String, dynamic> json)
    : id = (json['id'] as String?) ?? '',
      fullName = (json['fullName'] as String?) ?? '',
      email = (json['email'] as String?) ?? '',
      roles = List<String>.from(json['roles'] as List? ?? const []);
  final String id;
  final String fullName;
  final String email;
  final List<String> roles;
  bool has(String role) => roles.contains(role);
}

/// Public backend address only. Provider secrets belong on the backend.
abstract final class AppConfig {
  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5184/api',
  );

  static String validateApiBaseUrl(String value, {required bool allowHttp}) {
    final uri = Uri.tryParse(value);
    if (uri == null ||
        !uri.hasAuthority ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        uri.hasQuery ||
        uri.hasFragment ||
        (uri.scheme != 'https' && !(allowHttp && uri.scheme == 'http'))) {
      throw const FormatException(
        'API_BASE_URL must be a valid HTTPS base URL (HTTP is allowed only in debug).',
      );
    }
    return value.replaceAll(RegExp(r'/+$'), '');
  }
}

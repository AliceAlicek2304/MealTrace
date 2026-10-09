import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/core/config/app_config.dart';

void main() {
  test('Production config requires HTTPS and rejects embedded credentials', () {
    expect(
      () => AppConfig.validateApiBaseUrl(
        'http://example.test/api',
        allowHttp: false,
      ),
      throwsFormatException,
    );
    expect(
      () => AppConfig.validateApiBaseUrl(
        'https://user:secret@example.test/api',
        allowHttp: false,
      ),
      throwsFormatException,
    );
    expect(
      AppConfig.validateApiBaseUrl(
        'https://example.test/api/',
        allowHttp: false,
      ),
      'https://example.test/api',
    );
  });
  test('Debug config permits local HTTP and rejects invalid endpoints', () {
    expect(
      AppConfig.validateApiBaseUrl(
        'http://127.0.0.1:5184/api',
        allowHttp: true,
      ),
      'http://127.0.0.1:5184/api',
    );
    for (final value in [
      'not-a-url',
      'file:///api',
      'https://example.test/api?token=secret',
      'https://example.test/api#login',
    ]) {
      expect(
        () => AppConfig.validateApiBaseUrl(value, allowHttp: true),
        throwsFormatException,
      );
    }
  });
}

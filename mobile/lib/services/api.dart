import 'dart:convert';
import 'dart:io';

typedef ApiTransport =
    Future<ApiResponse> Function(
      Uri uri,
      String method,
      Map<String, String> headers,
      Object? body,
    );

class ApiResponse {
  const ApiResponse(this.statusCode, this.body);

  final int statusCode;
  final String body;
}

class Api {
  Api(this.baseUrl, {ApiTransport? transport})
    : _transport = transport ?? _sendRequest;

  final String baseUrl;
  final ApiTransport _transport;
  String? token;

  Future<dynamic> request(
    String method,
    String path, {
    Object? body,
    bool auth = true,
  }) async {
    final uri = Uri.parse('$baseUrl/api$path');
    final headers = <String, String>{
      HttpHeaders.acceptHeader: 'application/json',
    };
    if (body != null) {
      headers[HttpHeaders.contentTypeHeader] = 'application/json';
    }
    if (auth && token != null) {
      headers[HttpHeaders.authorizationHeader] = 'Bearer $token';
    }

    try {
      final response = await _transport(uri, method, headers, body);
      dynamic data;
      if (response.body.isNotEmpty) {
        try {
          data = jsonDecode(response.body);
        } on FormatException {
          data = null;
        }
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        final message = data is Map ? data['message'] : null;
        throw ApiException(
          message?.toString() ?? _statusMessage(response.statusCode),
        );
      }
      return data;
    } on SocketException {
      throw ApiException(
        'Không kết nối được API. Kiểm tra địa chỉ máy chủ và mạng.',
      );
    } on HandshakeException {
      throw ApiException('Lỗi kết nối bảo mật SSL/TLS với máy chủ.');
    }
  }

  static Future<ApiResponse> _sendRequest(
    Uri uri,
    String method,
    Map<String, String> headers,
    Object? body,
  ) async {
    final client = HttpClient()
      ..connectionTimeout = const Duration(seconds: 10);
    try {
      final request = await client.openUrl(method, uri);
      headers.forEach(request.headers.set);
      if (body != null) request.write(jsonEncode(body));
      final response = await request.close();
      final text = await response.transform(utf8.decoder).join();
      return ApiResponse(response.statusCode, text);
    } finally {
      client.close(force: true);
    }
  }

  static String _statusMessage(int status) => switch (status) {
    401 => 'Số điện thoại/email hoặc mật khẩu không đúng.',
    403 => 'Tài khoản không có quyền xem dữ liệu này.',
    409 => 'Dữ liệu vừa thay đổi hoặc đã qua giờ chốt.',
    429 => 'Thử đăng nhập quá nhanh. Vui lòng đợi một phút.',
    _ => 'Máy chủ trả về lỗi ($status).',
  };
}

class ApiException implements Exception {
  ApiException(this.message);
  final String message;
  @override
  String toString() => message;
}

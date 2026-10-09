import '../domain/parent_otp_challenge.dart';
import '../domain/parent_registration.dart';
import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;
import '../domain/auth_models.dart';

class AuthApi {
  AuthApi(this._client, String baseUrl)
    : baseUrl = baseUrl.replaceAll(RegExp(r'/+$'), '');
  final http.Client _client;
  final String baseUrl;

  Future<Map<String, dynamic>> _request(
    String path, {
    String? token,
    Map<String, String>? body,
    bool post = false,
  }) async {
    try {
      final uri = Uri.parse('$baseUrl/auth/$path');
      final headers = {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
      };
      final response =
          await (post
                  ? _client.post(
                      uri,
                      headers: headers,
                      body: body == null ? null : jsonEncode(body),
                    )
                  : _client.get(uri, headers: headers))
              .timeout(const Duration(seconds: 15));
      if (response.statusCode == 401) {
        throw AuthFailure(
          path == 'login'
              ? 'SĐT/email hoặc mật khẩu không đúng, hoặc tài khoản không thể đăng nhập.'
              : 'Phiên đăng nhập đã hết hiệu lực.',
          unauthorized: true,
        );
      }
      if (response.statusCode == 429) {
        throw const AuthFailure(
          'Bạn đã thử quá nhiều lần. Vui lòng đợi rồi thử lại.',
        );
      }
      if (path.startsWith('register') &&
          (response.statusCode == 400 || response.statusCode == 409)) {
        final data = jsonDecode(utf8.decode(response.bodyBytes));
        final message = data is Map ? data['message'] : null;
        throw AuthFailure(
          message is String && message.length <= 500
              ? message
              : 'Không thể đăng ký tài khoản.',
        );
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw const AuthFailure(
          'Không thể xử lý yêu cầu. Vui lòng thử lại sau.',
        );
      }
      return response.body.isEmpty
          ? {}
          : jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
    } on AuthFailure {
      rethrow;
    } on TimeoutException {
      throw const AuthFailure('Kết nối quá thời gian. Vui lòng thử lại.');
    } on http.ClientException {
      throw const AuthFailure(
        'Không kết nối được máy chủ. Kiểm tra mạng và thử lại.',
      );
    } on FormatException {
      throw const AuthFailure('Phản hồi máy chủ không hợp lệ.');
    } on TypeError {
      throw const AuthFailure('Phản hồi máy chủ không hợp lệ.');
    }
  }

  Future<ParentOtpChallenge> requestParentOtp(String phone) async {
    final json = await _request(
      'register/otp',
      post: true,
      body: {'phoneNumber': phone},
    );
    try {
      return ParentOtpChallenge(
        json['challengeId'] as String,
        DateTime.parse(json['expiresAt'] as String),
        DateTime.parse(json['resendAt'] as String),
        json['message'] as String,
      );
    } catch (_) {
      throw const AuthFailure('Phản hồi OTP không hợp lệ.');
    }
  }

  Future<void> registerParent(ParentRegistration input) async {
    await _request(
      'register',
      post: true,
      body: {
        'fullName': input.fullName.trim(),
        'phoneNumber': input.normalizedPhone ?? input.phoneNumber,
        'password': input.password,
        if (input.challengeId != null) 'challengeId': input.challengeId!,
        if (input.otpCode != null) 'otpCode': input.otpCode!,
      },
    );
  }

  Future<LoginSession> login(String identifier, String password) async {
    final json = await _request(
      'login',
      post: true,
      body: {'identifier': identifier.trim(), 'password': password},
    );
    try {
      final token = json['accessToken'] as String;
      final expiry = DateTime.parse(json['expiresAt'] as String);
      if (token.isEmpty || !expiry.isAfter(DateTime.now())) {
        throw const FormatException();
      }
      return LoginSession(
        token,
        expiry,
        _userFromJson(json['user'] as Map<String, dynamic>),
      );
    } catch (_) {
      throw const AuthFailure('Thông tin phiên đăng nhập không hợp lệ.');
    }
  }

  Future<CurrentUser> currentUser(String token) async {
    final json = await _request('me', token: token);
    try {
      return _userFromJson(json);
    } catch (_) {
      throw const AuthFailure('Thông tin tài khoản không hợp lệ.');
    }
  }

  Future<void> logout(String token) async {
    await _request('logout', token: token, post: true);
  }

  CurrentUser _userFromJson(Map<String, dynamic> json) => CurrentUser(
    id: json['id'] as String,
    fullName: json['fullName'] as String,
    email: json['email'] as String,
    phoneNumber: json['phoneNumber'] as String?,
    inspectorAccessUntil: json['inspectorAccessUntil'] as String?,
    roles: List<String>.from(json['roles'] as List<dynamic>),
  );

  void dispose() => _client.close();
}

import 'package:flutter/material.dart';

import '../models/current_user.dart';
import '../services/api.dart';
import '../theme/app_colors.dart';
import '../widgets/app_error_card.dart';
import 'home_page.dart';
class LoginPage extends StatefulWidget {
  const LoginPage({super.key});
  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _form = GlobalKey<FormState>();
  final _identifier = TextEditingController();
  final _password = TextEditingController();
  final _apiUrl = TextEditingController(text: 'http://10.0.2.2:5184');
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _identifier.dispose();
    _password.dispose();
    _apiUrl.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final api = Api(_apiUrl.text.trim().replaceFirst(RegExp(r'/+$'), ''));
    try {
      final result = await api.request(
        'POST',
        '/auth/login',
        auth: false,
        body: {
          'identifier': _identifier.text.trim(),
          'password': _password.text,
        },
      );
      if (result is! Map<String, dynamic> || result['accessToken'] == null) {
        throw ApiException('Phản hồi đăng nhập không hợp lệ từ máy chủ.');
      }
      api.token = result['accessToken'] as String;
      final userData = result['user'];
      if (userData is! Map<String, dynamic>) {
        throw ApiException('Không tìm thấy thông tin người dùng.');
      }
      final user = CurrentUser.fromJson(userData);
      if (!mounted) return;
      final navigator = Navigator.of(context);
      navigator.pushReplacement(
        MaterialPageRoute(
          builder: (_) => HomePage(
            api: api,
            user: user,
            onLogout: () => navigator.pushAndRemoveUntil(
              MaterialPageRoute(builder: (_) => const LoginPage()),
              (_) => false,
            ),
          ),
        ),
      );
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(
        () => _error = 'Không thể đăng nhập. Kiểm tra thông tin và thử lại.',
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: Form(
              key: _form,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Container(
                    width: 58,
                    height: 58,
                    decoration: BoxDecoration(
                      color: const Color(0xFFE3F1E8),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: const Icon(
                      Icons.eco_rounded,
                      color: AppColors.green,
                      size: 30,
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    'MealTrace',
                    style: Theme.of(context).textTheme.headlineMedium
                        ?.copyWith(fontWeight: FontWeight.w800, color: AppColors.ink),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Bữa ăn minh bạch, chăm sóc trọn vẹn.',
                    style: TextStyle(color: Color(0xFF68786E)),
                  ),
                  const SizedBox(height: 32),
                  _FieldLabel('Số điện thoại hoặc email'),
                  TextFormField(
                    controller: _identifier,
                    keyboardType: TextInputType.emailAddress,
                    textInputAction: TextInputAction.next,
                    decoration: const InputDecoration(
                      hintText: 'Nhập số điện thoại hoặc email',
                    ),
                    validator: (value) => value == null || value.trim().isEmpty
                        ? 'Vui lòng nhập tài khoản.'
                        : null,
                  ),
                  const SizedBox(height: 18),
                  _FieldLabel('Mật khẩu'),
                  TextFormField(
                    controller: _password,
                    obscureText: true,
                    textInputAction: TextInputAction.done,
                    onFieldSubmitted: (_) => _login(),
                    decoration: const InputDecoration(
                      hintText: 'Nhập mật khẩu',
                    ),
                    validator: (value) => value == null || value.isEmpty
                        ? 'Vui lòng nhập mật khẩu.'
                        : null,
                  ),
                  const SizedBox(height: 18),
                  ExpansionTile(
                    tilePadding: EdgeInsets.zero,
                    childrenPadding: const EdgeInsets.only(bottom: 12),
                    title: const Text(
                      'Cấu hình kết nối API',
                      style: TextStyle(fontSize: 14),
                    ),
                    children: [
                      TextFormField(
                        controller: _apiUrl,
                        keyboardType: TextInputType.url,
                        decoration: const InputDecoration(
                          labelText: 'Địa chỉ máy chủ',
                          hintText: 'http://10.0.2.2:5184',
                        ),
                      ),
                    ],
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: 8),
                    AppErrorCard(_error!),
                  ],
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _busy ? null : _login,
                    style: FilledButton.styleFrom(
                      minimumSize: const Size.fromHeight(54),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(15),
                      ),
                    ),
                    child: _busy
                        ? const SizedBox.square(
                            dimension: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('Đăng nhập'),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Đăng nhập bằng tài khoản do nhà trường cấp.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Color(0xFF7B8981), fontSize: 12),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.label);
  final String label;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      label,
      style: const TextStyle(
        color: AppColors.ink,
        fontWeight: FontWeight.w700,
      ),
    ),
  );
}

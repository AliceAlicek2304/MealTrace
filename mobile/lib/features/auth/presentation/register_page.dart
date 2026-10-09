import 'dart:async';
import 'package:flutter/services.dart';
import '../domain/parent_otp_challenge.dart';
import 'package:flutter/material.dart';
import '../domain/parent_registration.dart';
import 'auth_controller.dart';

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key, required this.auth});
  final AuthController auth;
  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _confirmation = TextEditingController();
  final _otp = TextEditingController();
  ParentOtpChallenge? _challenge;
  Timer? _timer;
  DateTime? _resendAt;
  int get _resendSeconds => _resendAt == null
      ? 0
      : _resendAt!.difference(DateTime.now()).inSeconds.clamp(0, 60);
  String? _error;

  Future<void> _sendOtp() async {
    if (widget.auth.busy || _resendSeconds > 0) return;
    setState(() {
      _error = null;
      _challenge = null;
      _otp.clear();
    });
    _resendAt = DateTime.now().add(const Duration(seconds: 60));
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
    final result = await widget.auth.requestParentOtp(_phone.text);
    if (!mounted || result == null) return;
    setState(() {
      _challenge = result;
      _resendAt = result.resendAt;
      _otp.clear();
    });
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  bool _obscure = true;

  Future<void> _submit() async {
    if (widget.auth.busy || !_form.currentState!.validate()) return;
    final input = ParentRegistration(
      _name.text,
      _phone.text,
      _password.text,
      challengeId: _challenge?.id,
      otpCode: _otp.text,
    );
    setState(() => _error = input.validate(requireOtp: true));
    if (_error != null) return;
    if (!_challenge!.expiresAt.isAfter(DateTime.now())) {
      setState(() => _error = 'OTP đã hết hạn. Vui lòng gửi lại mã.');
      return;
    }
    FocusScope.of(context).unfocus();
    if (await widget.auth.registerParent(input) && mounted) {
      Navigator.of(context).pop(input.normalizedPhone);
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    for (final controller in [_name, _phone, _password, _confirmation, _otp]) {
      controller.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: widget.auth,
    builder: (context, _) {
      final busy = widget.auth.busy;
      return PopScope(
        canPop: !busy,
        child: Scaffold(
          appBar: AppBar(title: const Text('Đăng ký phụ huynh')),
          body: Form(
            key: _form,
            child: AutofillGroup(
              child: ListView(
                padding: const EdgeInsets.all(24),
                children: [
                  const Text(
                    'Nhà trường cần liên kết tài khoản với trẻ để bạn xem dữ liệu. Bạn không cần chọn vai trò.',
                  ),
                  const SizedBox(height: 24),
                  TextFormField(
                    controller: _name,
                    enabled: !busy,
                    maxLength: 120,
                    autofillHints: const [AutofillHints.name],
                    decoration: const InputDecoration(labelText: 'Họ tên'),
                    textInputAction: TextInputAction.next,
                  ),
                  TextFormField(
                    controller: _phone,
                    onChanged: (_) => setState(() {
                      _challenge = null;
                      _resendAt = null;
                      _otp.clear();
                    }),
                    enabled: !busy,
                    maxLength: 30,
                    keyboardType: TextInputType.phone,
                    autofillHints: const [AutofillHints.telephoneNumber],
                    decoration: const InputDecoration(
                      labelText: 'Số điện thoại',
                    ),
                    textInputAction: TextInputAction.next,
                  ),
                  OutlinedButton(
                    onPressed: busy || _resendSeconds > 0 ? null : _sendOtp,
                    child: Text(
                      _resendSeconds > 0
                          ? 'Gửi lại sau ${_resendSeconds}s'
                          : _challenge == null
                          ? 'Gửi OTP qua WhatsApp'
                          : 'Gửi lại OTP WhatsApp',
                    ),
                  ),
                  if (_challenge != null) ...[
                    Text(_challenge!.message),
                    TextFormField(
                      controller: _otp,
                      enabled: !busy,
                      maxLength: 6,
                      keyboardType: TextInputType.number,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      autofillHints: const [AutofillHints.oneTimeCode],
                      decoration: const InputDecoration(
                        labelText: 'Mã OTP WhatsApp',
                        helperText:
                            'Mã có hiệu lực 5 phút. Gửi lại sẽ vô hiệu mã cũ.',
                        helperMaxLines: 2,
                      ),
                    ),
                  ],
                  TextFormField(
                    controller: _password,
                    enabled: !busy,
                    maxLength: 128,
                    obscureText: _obscure,
                    autocorrect: false,
                    enableSuggestions: false,
                    autofillHints: const [AutofillHints.newPassword],
                    decoration: InputDecoration(
                      labelText: 'Mật khẩu',
                      helperText:
                          '12–128 ký tự: chữ hoa, chữ thường, số, ký tự đặc biệt.',
                      helperMaxLines: 3,
                      suffixIcon: IconButton(
                        onPressed: busy
                            ? null
                            : () => setState(() => _obscure = !_obscure),
                        tooltip: _obscure ? 'Hiện mật khẩu' : 'Ẩn mật khẩu',
                        icon: Icon(
                          _obscure
                              ? Icons.visibility_outlined
                              : Icons.visibility_off_outlined,
                        ),
                      ),
                    ),
                    textInputAction: TextInputAction.next,
                  ),
                  TextFormField(
                    controller: _confirmation,
                    enabled: !busy,
                    maxLength: 128,
                    obscureText: _obscure,
                    autocorrect: false,
                    enableSuggestions: false,
                    autofillHints: const [AutofillHints.newPassword],
                    decoration: const InputDecoration(
                      labelText: 'Nhập lại mật khẩu',
                    ),
                    validator: (value) => value != _password.text
                        ? 'Hai mật khẩu chưa khớp.'
                        : null,
                    textInputAction: TextInputAction.done,
                    onFieldSubmitted: (_) => _submit(),
                  ),
                  if (_error != null || widget.auth.message != null)
                    Semantics(
                      liveRegion: true,
                      child: Text(
                        _error ?? widget.auth.message!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
          bottomNavigationBar: SafeArea(
            child: Padding(
              padding: EdgeInsets.fromLTRB(
                16,
                16,
                16,
                16 + MediaQuery.viewInsetsOf(context).bottom,
              ),
              child: FilledButton(
                onPressed: busy ? null : _submit,
                child: Text(busy ? 'Đang đăng ký…' : 'Tạo tài khoản phụ huynh'),
              ),
            ),
          ),
        ),
      );
    },
  );
}

class ParentRegistration {
  const ParentRegistration(
    this.fullName,
    this.phoneNumber,
    this.password, {
    this.challengeId,
    this.otpCode,
  });
  final String fullName;
  final String phoneNumber;
  final String password;
  final String? challengeId;
  final String? otpCode;

  String? get normalizedPhone {
    var phone = phoneNumber.trim().replaceAll(RegExp(r'[\s().-]'), '');
    if (phone.startsWith('+84')) {
      phone = '0${phone.substring(3)}';
    } else if (phone.startsWith('84')) {
      phone = '0${phone.substring(2)}';
    }
    return RegExp(r'^0[1-9][0-9]{8}$').hasMatch(phone) ? phone : null;
  }

  String? validate({bool requireOtp = false}) {
    if (fullName.trim().length < 2 || fullName.trim().length > 120) {
      return 'Họ tên cần từ 2 đến 120 ký tự.';
    }
    if (normalizedPhone == null) return 'SĐT Việt Nam không hợp lệ.';
    if (password.length < 12 ||
        password.length > 128 ||
        !RegExp(r'[A-Z]').hasMatch(password) ||
        !RegExp(r'[a-z]').hasMatch(password) ||
        !RegExp(r'[0-9]').hasMatch(password) ||
        !RegExp(r'[^a-zA-Z0-9]').hasMatch(password)) {
      return 'Mật khẩu cần 12–128 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.';
    }
    if (requireOtp &&
        (challengeId == null || !RegExp(r'^\d{6}$').hasMatch(otpCode ?? ''))) {
      return 'Vui lòng nhận và nhập 6 chữ số OTP từ WhatsApp.';
    }
    return null;
  }
}

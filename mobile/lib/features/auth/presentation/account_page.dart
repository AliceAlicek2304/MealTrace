import 'package:flutter/material.dart';
import 'auth_controller.dart';

class AccountPage extends StatelessWidget {
  const AccountPage({super.key, required this.auth});
  final AuthController auth;
  static const _roles = {
    'ADMIN': 'Quản trị viên',
    'TEACHER': 'Giáo viên',
    'PARENT': 'Phụ huynh',
    'KITCHEN_STAFF': 'Nhân viên bếp',
  };
  @override
  Widget build(BuildContext context) {
    final user = auth.user!;
    return Scaffold(
      appBar: AppBar(title: const Text('MealTrace')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Xin chào, ${user.fullName}',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 16),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      if (user.phoneNumber?.isNotEmpty == true)
                        Text('SĐT: ${user.phoneNumber}'),
                      if (user.email.isNotEmpty) Text('Email: ${user.email}'),
                      const SizedBox(height: 12),
                      Text(
                        user.roles.isEmpty
                            ? 'Tài khoản thanh tra'
                            : user.roles
                                  .map((role) => _roles[role] ?? role)
                                  .join(' · '),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 20),
              const Text(
                'Bạn đã đăng nhập thành công. Các chức năng bữa ăn đang được phát triển.',
              ),
              if (auth.message != null)
                Padding(
                  padding: const EdgeInsets.only(top: 16),
                  child: Text(auth.message!),
                ),
              const SizedBox(height: 24),
              OutlinedButton(
                onPressed: auth.busy ? null : auth.logout,
                child: Text(auth.busy ? 'Đang đăng xuất…' : 'Đăng xuất'),
              ),
              const SizedBox(height: 8),
              const Text(
                'Đăng xuất sẽ kết thúc các phiên hiện có của tài khoản trên máy chủ.',
                style: TextStyle(fontSize: 12),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

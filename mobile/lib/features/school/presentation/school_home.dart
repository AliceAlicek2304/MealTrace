import 'package:flutter/material.dart';
import '../../auth/presentation/auth_controller.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_form_page.dart';
import 'school_forms.dart';
import 'school_page.dart';

class SchoolHome extends StatefulWidget {
  const SchoolHome({super.key, required this.auth, required this.repository});
  final AuthController auth;
  final SchoolRepository repository;
  @override
  State<SchoolHome> createState() => _SchoolHomeState();
}

class _SchoolHomeState extends State<SchoolHome> {
  late final SchoolController controller;
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.auth.user!.roles,
      widget.auth.invalidateSession,
    );
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> open(SchoolOperation operation, String title) async {
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => SchoolPage(
          operation: operation,
          title: title,
          repository: widget.repository,
          roles: widget.auth.user!.roles,
          onUnauthorized: widget.auth.invalidateSession,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final user = widget.auth.user!;
    final sections =
        <({SchoolOperation operation, String title, IconData icon})>[
              (
                operation: SchoolOperation.workflowDays,
                title: 'Sổ suất',
                icon: Icons.restaurant,
              ),
              (
                operation: SchoolOperation.users,
                title: 'Tài khoản',
                icon: Icons.manage_accounts,
              ),
              (
                operation: user.roles.contains('ADMIN')
                    ? SchoolOperation.students
                    : SchoolOperation.scopedStudents,
                title: 'Trẻ',
                icon: Icons.child_care,
              ),
              (
                operation: SchoolOperation.classes,
                title: 'Lớp',
                icon: Icons.school,
              ),
              (
                operation: SchoolOperation.yearConfiguration,
                title: 'Năm học',
                icon: Icons.event,
              ),
              (
                operation: SchoolOperation.calendar,
                title: 'Lịch bữa ăn',
                icon: Icons.calendar_month,
              ),
              (
                operation: SchoolOperation.absences,
                title: 'Báo vắng / Không ăn',
                icon: Icons.event_busy,
              ),
              (
                operation: SchoolOperation.children,
                title: 'Trẻ đã liên kết',
                icon: Icons.family_restroom,
              ),
              (
                operation: SchoolOperation.meals,
                title: 'Hồ sơ ngày ăn',
                icon: Icons.fact_check,
              ),
            ]
            .where((section) => canExecute(section.operation, user.roles))
            .toList();
    Widget menuItem(
      ({SchoolOperation operation, String title, IconData icon}) section,
    ) => ListTile(
      leading: Icon(section.icon),
      title: Text(section.title),
      trailing: const Icon(Icons.chevron_right),
      onTap: () {
        Navigator.maybePop(context);
        open(section.operation, section.title);
      },
    );
    return Scaffold(
      appBar: AppBar(title: const Text('MealTrace')),
      drawer: Drawer(
        child: SafeArea(
          child: ListView(
            children: [
              Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  'MealTrace',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              for (final section in sections) menuItem(section),
              const Divider(),
              ListTile(
                leading: const Icon(Icons.logout),
                title: const Text('Đăng xuất'),
                onTap: widget.auth.busy ? null : widget.auth.logout,
              ),
            ],
          ),
        ),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(
              'Xin chào, ${user.fullName}',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 16),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (user.phoneNumber?.isNotEmpty == true)
                      Text('SĐT: ${user.phoneNumber}'),
                    if (user.email.isNotEmpty) Text('Email: ${user.email}'),
                    if (user.inspectorAccessUntil != null)
                      Text('Quyền thanh tra đến ${user.inspectorAccessUntil}'),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: user.roles
                          .map(
                            (role) =>
                                Chip(label: Text(roleLabels[role] ?? role)),
                          )
                          .toList(),
                    ),
                    if (user.roles.isEmpty)
                      const Text(
                        'Tài khoản thanh tra — quyền dữ liệu được kiểm tra tại máy chủ.',
                      ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            for (final section in sections)
              Card(
                child: ListTile(
                  leading: Icon(section.icon),
                  title: Text(section.title),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => open(section.operation, section.title),
                ),
              ),
            const SizedBox(height: 20),
            OutlinedButton.icon(
              onPressed: () => Navigator.push<bool>(
                context,
                MaterialPageRoute(
                  builder: (_) => SchoolFormPage(
                    form: schoolForms[SchoolOperation.changePassword]!,
                    controller: controller,
                  ),
                ),
              ),
              icon: const Icon(Icons.lock_outline),
              label: const Text('Đổi mật khẩu'),
            ),
            OutlinedButton(
              onPressed: widget.auth.busy ? null : widget.auth.logout,
              child: const Text('Đăng xuất'),
            ),
            const Text(
              'Đăng xuất kết thúc các phiên hiện có của tài khoản trên máy chủ.',
              style: TextStyle(fontSize: 12),
            ),
          ],
        ),
      ),
    );
  }
}

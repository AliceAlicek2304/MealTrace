import 'package:flutter/material.dart';
import '../../auth/presentation/auth_controller.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_form_page.dart';
import 'school_forms.dart';
import 'school_page.dart';

typedef SchoolSection = ({
  SchoolOperation operation,
  String title,
  IconData icon,
});

class SchoolHome extends StatefulWidget {
  const SchoolHome({super.key, required this.auth, required this.repository});
  final AuthController auth;
  final SchoolRepository repository;
  @override
  State<SchoolHome> createState() => _SchoolHomeState();
}

class _SchoolHomeState extends State<SchoolHome> {
  late final SchoolController controller;
  int tab = 0;
  bool primaryVisited = false;
  List<SchoolSection> get sections {
    final roles = widget.auth.user!.roles;
    return <SchoolSection>[
      (
        operation: SchoolOperation.workflowDays,
        title: 'Sổ suất',
        icon: Icons.restaurant_outlined,
      ),
      (
        operation: SchoolOperation.absences,
        title: 'Báo vắng / Không ăn',
        icon: Icons.event_busy_outlined,
      ),
      (
        operation: SchoolOperation.children,
        title: 'Trẻ đã liên kết',
        icon: Icons.family_restroom_outlined,
      ),
      (
        operation: SchoolOperation.users,
        title: 'Tài khoản',
        icon: Icons.manage_accounts_outlined,
      ),
      (
        operation: roles.contains('ADMIN')
            ? SchoolOperation.students
            : SchoolOperation.scopedStudents,
        title: 'Trẻ',
        icon: Icons.child_care_outlined,
      ),
      (
        operation: SchoolOperation.classes,
        title: 'Lớp',
        icon: Icons.school_outlined,
      ),
      (
        operation: SchoolOperation.yearConfiguration,
        title: 'Năm học',
        icon: Icons.event_outlined,
      ),
      (
        operation: SchoolOperation.calendar,
        title: 'Lịch bữa ăn',
        icon: Icons.calendar_month_outlined,
      ),
      (
        operation: SchoolOperation.meals,
        title: 'Hồ sơ ngày ăn',
        icon: Icons.fact_check_outlined,
      ),
    ].where((section) => canExecute(section.operation, roles)).toList();
  }

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

  Future<void> open(SchoolSection section) => Navigator.push<void>(
    context,
    MaterialPageRoute(
      builder: (_) => SchoolPage(
        operation: section.operation,
        title: section.title,
        repository: widget.repository,
        roles: widget.auth.user!.roles,
        onUnauthorized: widget.auth.invalidateSession,
      ),
    ),
  );

  Future<void> showFunctions() async {
    final selected = await showModalBottomSheet<SchoolSection>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (context) => SafeArea(
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxHeight: MediaQuery.sizeOf(context).height * .8,
          ),
          child: ListView(
            shrinkWrap: true,
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 16),
            children: [
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(
                  'Chức năng',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              for (final section in sections)
                ListTile(
                  leading: Icon(section.icon),
                  title: Text(section.title),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => Navigator.pop(context, section),
                ),
            ],
          ),
        ),
      ),
    );
    if (selected != null && mounted) await open(selected);
  }

  Widget home() {
    final user = widget.auth.user!;
    return ListView(
      key: const PageStorageKey('school-home'),
      padding: const EdgeInsets.all(20),
      children: [
        Text(
          'Xin chào, ${user.fullName}',
          style: Theme.of(
            context,
          ).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 8),
        Text(user.roles.map((role) => roleLabels[role] ?? role).join(' · ')),
        const SizedBox(height: 28),
        Text('Truy cập nhanh', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 12),
        LayoutBuilder(
          builder: (context, bounds) {
            final columns = bounds.maxWidth >= 600 ? 3 : 2;
            final width = (bounds.maxWidth - 12 * (columns - 1)) / columns;
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final section in sections.take(4))
                  SizedBox(
                    width: width,
                    child: Card(
                      margin: EdgeInsets.zero,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(20),
                        onTap: () => open(section),
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Icon(
                                section.icon,
                                size: 28,
                                color: Theme.of(context).colorScheme.primary,
                              ),
                              const SizedBox(height: 20),
                              Text(
                                section.title,
                                style: Theme.of(context).textTheme.titleSmall,
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
        const SizedBox(height: 16),
        OutlinedButton.icon(
          onPressed: showFunctions,
          icon: const Icon(Icons.apps),
          label: const Text('Xem tất cả chức năng'),
        ),
        const SizedBox(height: 20),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(Icons.info_outline),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    user.roles.contains('PARENT')
                        ? 'Báo không ăn cho trẻ theo ngày hoặc khoảng ngày. Các thay đổi sau giờ chốt vẫn lưu lịch sử.'
                        : 'Suất dự kiến dựa trên ghi danh và báo không ăn. Điều chỉnh sau chốt chỉ áp dụng khi được Admin duyệt.',
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget profile() {
    final user = widget.auth.user!;
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        CircleAvatar(
          radius: 32,
          child: Text(
            user.fullName.trim().isEmpty
                ? '?'
                : user.fullName.trim().substring(0, 1).toUpperCase(),
            style: const TextStyle(fontSize: 26),
          ),
        ),
        const SizedBox(height: 16),
        Text(
          user.fullName,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 24),
        Card(
          child: Column(
            children: [
              if (user.phoneNumber?.isNotEmpty == true)
                ListTile(
                  leading: const Icon(Icons.phone_outlined),
                  title: const Text('Số điện thoại'),
                  subtitle: Text(user.phoneNumber!),
                ),
              if (user.email.isNotEmpty)
                ListTile(
                  leading: const Icon(Icons.mail_outline),
                  title: const Text('Email'),
                  subtitle: Text(user.email),
                ),
              ListTile(
                leading: const Icon(Icons.badge_outlined),
                title: const Text('Vai trò'),
                subtitle: Text(
                  user.roles.isEmpty
                      ? 'Thanh tra'
                      : user.roles
                            .map((role) => roleLabels[role] ?? role)
                            .join(' · '),
                ),
              ),
              if (user.inspectorAccessUntil != null)
                ListTile(
                  title: const Text('Quyền thanh tra đến'),
                  subtitle: Text(user.inspectorAccessUntil!),
                ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Column(
            children: [
              ListTile(
                leading: const Icon(Icons.lock_outline),
                title: const Text('Đổi mật khẩu'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.push<bool>(
                  context,
                  MaterialPageRoute(
                    builder: (_) => SchoolFormPage(
                      form: schoolForms[SchoolOperation.changePassword]!,
                      controller: controller,
                    ),
                  ),
                ),
              ),
              ListTile(
                leading: Icon(
                  Icons.logout,
                  color: Theme.of(context).colorScheme.error,
                ),
                title: const Text('Đăng xuất'),
                onTap: widget.auth.busy
                    ? null
                    : () async {
                        final confirmed = await showDialog<bool>(
                          context: context,
                          builder: (context) => AlertDialog(
                            title: const Text('Đăng xuất?'),
                            content: const Text(
                              'Các phiên hiện có của tài khoản trên máy chủ cũng sẽ kết thúc.',
                            ),
                            actions: [
                              TextButton(
                                onPressed: () => Navigator.pop(context, false),
                                child: const Text('Ở lại'),
                              ),
                              FilledButton(
                                onPressed: () => Navigator.pop(context, true),
                                child: const Text('Đăng xuất'),
                              ),
                            ],
                          ),
                        );
                        if (confirmed == true && mounted) {
                          await widget.auth.logout();
                        }
                      },
              ),
            ],
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final available = sections;
    final primary = available.isEmpty ? null : available.first;
    return PopScope(
      canPop: tab == 0,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop && mounted) setState(() => tab = 0);
      },
      child: Scaffold(
        appBar: tab == 1
            ? null
            : AppBar(title: Text(tab == 3 ? 'Hồ sơ' : 'MealTrace')),
        body: SafeArea(
          child: IndexedStack(
            index: tab == 3 ? 2 : tab,
            children: [
              home(),
              primaryVisited && primary != null
                  ? SchoolPage(
                      embedded: true,
                      operation: primary.operation,
                      title: primary.title,
                      repository: widget.repository,
                      roles: widget.auth.user!.roles,
                      onUnauthorized: widget.auth.invalidateSession,
                    )
                  : const SizedBox(),
              profile(),
            ],
          ),
        ),
        bottomNavigationBar: NavigationBar(
          selectedIndex: tab,
          onDestinationSelected: (index) {
            if (index == 2 || index == 1 && primary == null) {
              showFunctions();
              return;
            }
            setState(() {
              tab = index;
              if (index == 1) primaryVisited = true;
            });
          },
          destinations: [
            const NavigationDestination(
              icon: Icon(Icons.home_outlined),
              selectedIcon: Icon(Icons.home),
              label: 'Trang chủ',
            ),
            NavigationDestination(
              icon: Icon(primary?.icon ?? Icons.work_outline),
              label: primary?.operation == SchoolOperation.absences
                  ? 'Không ăn'
                  : 'Suất ăn',
            ),
            const NavigationDestination(
              icon: Icon(Icons.apps),
              label: 'Chức năng',
            ),
            const NavigationDestination(
              icon: Icon(Icons.person_outline),
              selectedIcon: Icon(Icons.person),
              label: 'Hồ sơ',
            ),
          ],
        ),
      ),
    );
  }
}

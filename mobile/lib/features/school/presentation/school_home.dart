import '../../../core/presentation/meal_widgets.dart';
import 'kitchen_settlements_page.dart';
import 'kitchen_catalog_page.dart';
import 'recipe_page.dart';
import 'report_lookup_page.dart';
import 'notification_test_page.dart';
import 'student_import_history_page.dart';
import 'parent_link_review_page.dart';
import 'student_import_page.dart';
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
        operation: SchoolOperation.parentLinks,
        title: 'Liên kết trẻ',
        icon: Icons.link_outlined,
      ),
      (
        operation: SchoolOperation.reviewableParentLinks,
        title: 'Yêu cầu liên kết trẻ',
        icon: Icons.verified_user_outlined,
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
        operation: SchoolOperation.studentImportHistory,
        title: 'Lịch sử nhập trẻ',
        icon: Icons.history_outlined,
      ),
      (
        operation: SchoolOperation.previewStudentImport,
        title: 'Nhập trẻ từ Excel',
        icon: Icons.upload_file_outlined,
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
        operation: SchoolOperation.kitchenSettlements,
        title: 'Tiếp nhận suất ăn',
        icon: Icons.fact_check_outlined,
      ),
      (
        operation: SchoolOperation.meals,
        title: 'Hồ sơ ngày ăn',
        icon: Icons.fact_check_outlined,
      ),
      (
        operation: SchoolOperation.ingredients,
        title: 'Nguyên liệu',
        icon: Icons.kitchen_outlined,
      ),
      (
        operation: SchoolOperation.nutrition,
        title: 'Công thức & dinh dưỡng',
        icon: Icons.menu_book_outlined,
      ),
      (
        operation: SchoolOperation.reportLineage,
        title: 'Truy vết báo cáo',
        icon: Icons.receipt_long_outlined,
      ),
      (
        operation: SchoolOperation.sendTestNotification,
        title: 'Kiểm tra gửi tin',
        icon: Icons.chat_outlined,
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
      builder: (_) => section.operation == SchoolOperation.ingredients
          ? KitchenCatalogPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.nutrition
          ? RecipePage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.reportLineage
          ? ReportLookupPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.sendTestNotification
          ? NotificationTestPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.kitchenSettlements
          ? KitchenSettlementsPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.studentImportHistory
          ? StudentImportHistoryPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.previewStudentImport
          ? StudentImportPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : section.operation == SchoolOperation.reviewableParentLinks
          ? ParentLinkReviewPage(
              repository: widget.repository,
              roles: widget.auth.user!.roles,
              onUnauthorized: widget.auth.invalidateSession,
            )
          : SchoolPage(
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
              for (final group
                  in sections
                      .map((section) => sectionGroup(section.operation))
                      .toSet()) ...[
                Padding(
                  padding: const EdgeInsets.fromLTRB(14, 20, 14, 6),
                  child: Text(
                    group,
                    style: Theme.of(
                      context,
                    ).textTheme.labelLarge?.copyWith(color: MealColors.muted),
                  ),
                ),
                for (final section in sections.where(
                  (section) => sectionGroup(section.operation) == group,
                ))
                  ListTile(
                    leading: MealIcon(section.icon, size: 40),
                    title: Text(section.title),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.pop(context, section),
                  ),
              ],
            ],
          ),
        ),
      ),
    );
    if (selected != null && mounted) await open(selected);
  }

  String sectionGroup(SchoolOperation operation) => switch (operation) {
    SchoolOperation.workflowDays ||
    SchoolOperation.calendar ||
    SchoolOperation.meals ||
    SchoolOperation.kitchenSettlements => 'Bữa ăn & suất ăn',
    SchoolOperation.ingredients ||
    SchoolOperation.nutrition => 'Bếp & dinh dưỡng',
    SchoolOperation.users ||
    SchoolOperation.yearConfiguration => 'Quản trị nhà trường',
    SchoolOperation.reportLineage => 'Báo cáo',
    SchoolOperation.sendTestNotification => 'Thông báo',
    _ => 'Lớp học & gia đình',
  };

  Widget home() {
    final user = widget.auth.user!;
    return ListView(
      key: const PageStorageKey('school-home'),
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
      children: [
        MealHero(
          title: 'Xin chào, ${user.fullName}',
          subtitle: user.roles.contains('PARENT')
              ? 'Cùng nhà trường chăm sóc từng bữa ăn của con.'
              : 'Một ngày làm việc gọn gàng, mỗi bữa ăn thêm an tâm.',
          icon: Icons.spa_outlined,
          eyebrow: user.roles
              .map((role) => roleLabels[role] ?? role)
              .join(' · '),
        ),
        const MealSectionTitle(
          'Truy cập nhanh',
          subtitle: 'Các công việc thường dùng của bạn',
        ),
        LayoutBuilder(
          builder: (context, bounds) {
            final columns = bounds.maxWidth >= 600
                ? 3
                : bounds.maxWidth < 360 &&
                      MediaQuery.textScalerOf(context).scale(14) > 18
                ? 1
                : 2;
            final width = (bounds.maxWidth - 12 * (columns - 1)) / columns;
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final (index, section) in sections.take(4).indexed)
                  SizedBox(
                    width: width,
                    child: Card(
                      margin: EdgeInsets.zero,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(22),
                        onTap: () => open(section),
                        child: Padding(
                          padding: const EdgeInsets.all(18),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              MealIcon(
                                section.icon,
                                background: index.isEven
                                    ? MealColors.lavender
                                    : MealColors.mint,
                                color: index.isEven
                                    ? MealColors.primary
                                    : MealColors.green,
                              ),
                              const SizedBox(height: 18),
                              ConstrainedBox(
                                constraints: BoxConstraints(
                                  minHeight: MediaQuery.textScalerOf(
                                    context,
                                  ).scale(40),
                                ),
                                child: Text(
                                  section.title,
                                  style: Theme.of(context).textTheme.titleSmall,
                                ),
                              ),
                              const SizedBox(height: 10),
                              const Icon(
                                Icons.arrow_forward_rounded,
                                size: 18,
                                color: MealColors.muted,
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
        const SizedBox(height: 18),
        OutlinedButton.icon(
          onPressed: showFunctions,
          icon: const Icon(Icons.grid_view_rounded),
          label: const Text('Xem tất cả chức năng'),
        ),
        const SizedBox(height: 28),
        const MealSectionTitle('Lưu ý trong ngày'),
        Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            color: MealColors.mint,
            borderRadius: BorderRadius.circular(20),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(Icons.verified_outlined, color: MealColors.green),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  user.roles.contains('PARENT')
                      ? 'Báo không ăn cho trẻ theo ngày hoặc khoảng ngày. Các thay đổi sau giờ chốt vẫn lưu lịch sử.'
                      : 'Suất dự kiến dựa trên ghi danh và báo không ăn. Điều chỉnh sau chốt chỉ áp dụng khi được quản lý duyệt.',
                  style: const TextStyle(color: MealColors.green, height: 1.5),
                ),
              ),
            ],
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
          backgroundColor: MealColors.primary,
          foregroundColor: Colors.white,
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
            : AppBar(
                leading: tab == 3
                    ? null
                    : const Padding(
                        padding: EdgeInsets.only(left: 16),
                        child: Center(
                          child: MealIcon(Icons.spa_outlined, size: 36),
                        ),
                      ),
                title: Text(tab == 3 ? 'Hồ sơ' : 'MealTrace'),
              ),
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

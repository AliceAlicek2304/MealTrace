import 'package:flutter/material.dart';

import '../../../app/mealtrace_brand.dart';
import '../../../app/theme.dart';
import '../../../app/theme_mode_selector.dart';
import '../../auth/domain/auth_models.dart';
import '../../auth/presentation/auth_controller.dart';
import '../domain/school_models.dart';
import 'parent_link_review_page.dart';
import 'school_controller.dart';
import 'school_form_page.dart';
import 'school_forms.dart';
import 'school_page.dart';
import 'student_import_history_page.dart';
import 'student_import_page.dart';

typedef SchoolSection = ({
  SchoolOperation operation,
  String title,
  IconData icon,
});

class SchoolHome extends StatefulWidget {
  const SchoolHome({
    super.key,
    required this.auth,
    required this.repository,
    this.themeMode = ThemeMode.system,
    this.onThemeModeChanged,
  });
  final AuthController auth;
  final SchoolRepository repository;
  final ThemeMode themeMode;
  final ValueChanged<ThemeMode>? onThemeModeChanged;
  @override
  State<SchoolHome> createState() => _SchoolHomeState();
}

class _SchoolHomeState extends State<SchoolHome> {
  late final SchoolController controller;
  int tab = 0;
  bool primaryVisited = false;
  Future<SchoolResult>? _parentChildren;
  int _selectedChildIndex = 0;
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
    if (widget.auth.user!.roles.contains('PARENT')) {
      _parentChildren = controller.execute(SchoolOperation.children);
    }
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> open(SchoolSection section) => Navigator.push<void>(
    context,
    MaterialPageRoute(
      builder: (_) => section.operation == SchoolOperation.studentImportHistory
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

  Widget _sectionIcon(SchoolSection section) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    final palette = _quickAccessPalette(section.operation, dark);
    return CircleAvatar(
      radius: 19,
      backgroundColor: palette.background,
      child: Icon(section.icon, size: 19, color: palette.foreground),
    );
  }

  Future<void> showFunctions() async {
    final teacher = widget.auth.user!.roles.contains('TEACHER');
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
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Chức năng',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    if (teacher) ...[
                      const SizedBox(height: 3),
                      Text(
                        'Các chức năng dành cho Giáo viên',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              for (final section in sections)
                if (teacher)
                  Card(
                    margin: const EdgeInsets.symmetric(
                      horizontal: 4,
                      vertical: 4,
                    ),
                    child: ListTile(
                      leading: _sectionIcon(section),
                      title: Text(
                        section.title,
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => Navigator.pop(context, section),
                    ),
                  )
                else
                  ListTile(
                    leading: _sectionIcon(section),
                    title: Text(section.title),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.pop(context, section),
                  ),
            ],
          ),
        ),
      ),
    );
    if (selected != null && mounted) {
      if (widget.auth.user!.roles.contains('PARENT') &&
          selected.operation == SchoolOperation.absences) {
        setState(() {
          tab = 1;
          primaryVisited = true;
        });
      } else {
        await open(selected);
      }
    }
  }

  Widget home() {
    final user = widget.auth.user!;
    if (user.roles.contains('PARENT')) return parentHome(user);
    if (user.roles.contains('TEACHER')) return teacherHome(user);

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
            final dark = Theme.of(context).brightness == Brightness.dark;
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final section in sections.take(4)) ...[
                  Builder(
                    builder: (context) {
                      final palette = _quickAccessPalette(
                        section.operation,
                        dark,
                      );
                      return SizedBox(
                        width: width,
                        child: Card(
                          color: palette.background,
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
                                    color: palette.foreground,
                                  ),
                                  const SizedBox(height: 20),
                                  Text(
                                    section.title,
                                    style: Theme.of(
                                      context,
                                    ).textTheme.titleSmall,
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      );
                    },
                  ),
                ],
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
                        : 'Suất dự kiến dựa trên ghi danh và báo không ăn. Điều chỉnh sau chốt chỉ áp dụng khi được quản lý duyệt.',
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget teacherHome(CurrentUser user) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final roleLabel = user.roles
        .map((role) => roleLabels[role] ?? role)
        .join(' · ');
    final initial = user.fullName.trim().isEmpty
        ? '?'
        : user.fullName.trim().substring(0, 1).toUpperCase();

    return ListView(
      key: const PageStorageKey('teacher-home'),
      padding: const EdgeInsets.fromLTRB(18, 14, 18, 24),
      children: [
        Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Xin chào Giáo viên 👋',
                        style: theme.textTheme.labelLarge?.copyWith(
                          color: colors.onSurfaceVariant,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        user.fullName,
                        style: theme.textTheme.headlineSmall?.copyWith(
                          fontWeight: FontWeight.w800,
                          letterSpacing: -.4,
                        ),
                      ),
                      const SizedBox(height: 9),
                      _profilePill(
                        roleLabel,
                        colors.secondaryContainer,
                        colors.onSecondaryContainer,
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                Stack(
                  clipBehavior: Clip.none,
                  children: [
                    CircleAvatar(
                      radius: 27,
                      backgroundColor: colors.tertiaryContainer,
                      foregroundColor: colors.onTertiaryContainer,
                      child: Text(
                        initial,
                        style: theme.textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    Positioned(
                      right: -1,
                      bottom: 0,
                      child: Container(
                        width: 12,
                        height: 12,
                        decoration: BoxDecoration(
                          color: colors.tertiary,
                          shape: BoxShape.circle,
                          border: Border.all(color: colors.surface, width: 2),
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 22),
        Text(
          'Truy cập nhanh',
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 12),
        LayoutBuilder(
          builder: (context, bounds) {
            const columns = 2;
            final width = (bounds.maxWidth - 12 * (columns - 1)) / columns;
            final dark = Theme.of(context).brightness == Brightness.dark;
            final quickSections = sections.take(4);
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final section in quickSections)
                  Builder(
                    builder: (context) {
                      final palette = _quickAccessPalette(
                        section.operation,
                        dark,
                      );
                      final subtitle = switch (section.operation) {
                        SchoolOperation.workflowDays =>
                          'Quản lý điểm danh & suất ăn',
                        SchoolOperation.reviewableParentLinks =>
                          'Xác nhận kết nối phụ huynh',
                        SchoolOperation.scopedStudents =>
                          'Quản lý học sinh lớp',
                        _ => '',
                      };
                      return SizedBox(
                        width: width,
                        child: Card(
                          color: palette.background,
                          margin: EdgeInsets.zero,
                          child: InkWell(
                            borderRadius: BorderRadius.circular(20),
                            onTap: () => open(section),
                            child: Padding(
                              padding: const EdgeInsets.all(15),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    children: [
                                      Icon(
                                        section.icon,
                                        size: 23,
                                        color: palette.foreground,
                                      ),
                                      const Spacer(),
                                      Icon(
                                        Icons.arrow_forward_rounded,
                                        size: 17,
                                        color: palette.foreground.withValues(
                                          alpha: .7,
                                        ),
                                      ),
                                    ],
                                  ),
                                  const SizedBox(height: 22),
                                  Text(
                                    section.operation ==
                                            SchoolOperation.scopedStudents
                                        ? 'Danh sách Trẻ'
                                        : section.title,
                                    style: theme.textTheme.titleSmall?.copyWith(
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  if (subtitle.isNotEmpty) ...[
                                    const SizedBox(height: 4),
                                    Text(
                                      subtitle,
                                      maxLines: 2,
                                      overflow: TextOverflow.ellipsis,
                                      style: theme.textTheme.bodySmall
                                          ?.copyWith(
                                            color: colors.onSurfaceVariant,
                                            height: 1.25,
                                          ),
                                    ),
                                  ],
                                ],
                              ),
                            ),
                          ),
                        ),
                      );
                    },
                  ),
              ],
            );
          },
        ),
        const SizedBox(height: 14),
        OutlinedButton.icon(
          onPressed: showFunctions,
          icon: const Icon(Icons.grid_view_rounded),
          label: const Text('Xem tất cả chức năng'),
        ),
        const SizedBox(height: 12),
        Card(
          color: colors.secondaryContainer,
          child: Padding(
            padding: const EdgeInsets.all(15),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: colors.surfaceContainer,
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    Icons.info_outline_rounded,
                    color: colors.secondary,
                    size: 18,
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'QUY ĐỊNH CHỐT SUẤT ĂN TẠI TRƯỜNG',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: colors.onSecondaryContainer,
                          fontWeight: FontWeight.w800,
                          letterSpacing: .25,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Suất dự kiến dựa trên ghi danh và báo không ăn. Điều chỉnh sau chốt chỉ áp dụng khi được quản lý duyệt.',
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: colors.onSecondaryContainer,
                          height: 1.35,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget parentHome(CurrentUser user) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final absenceSection = sections
        .where((section) => section.operation == SchoolOperation.absences)
        .firstOrNull;
    final linksSection = sections
        .where((section) => section.operation == SchoolOperation.parentLinks)
        .firstOrNull;

    return ListView(
      key: const PageStorageKey('parent-home'),
      padding: const EdgeInsets.fromLTRB(18, 12, 18, 24),
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Xin chào Phụ huynh 👋',
                    style: theme.textTheme.titleSmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    user.fullName,
                    style: theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                      letterSpacing: -.5,
                    ),
                  ),
                  const SizedBox(height: 4),
                  FutureBuilder<SchoolResult>(
                    future: _parentChildren,
                    builder: (context, snapshot) {
                      final children =
                          snapshot.data?.records ?? const <SchoolRecord>[];
                      final child = children.isEmpty
                          ? null
                          : children[_selectedChildIndex.clamp(
                              0,
                              children.length - 1,
                            )];
                      final year = child?.text('schoolYear') ?? '';
                      return Text(
                        year.isEmpty ? 'Theo dõi thông tin của trẻ' : year,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: colors.onSurfaceVariant,
                        ),
                      );
                    },
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 18),
        _parentChildCard(linksSection),
        const SizedBox(height: 22),
        Row(
          children: [
            Expanded(
              child: Text(
                'Truy cập nhanh',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            TextButton.icon(
              onPressed: showFunctions,
              icon: const Icon(Icons.grid_view_rounded, size: 17),
              label: const Text('Tất cả'),
            ),
          ],
        ),
        const SizedBox(height: 8),
        LayoutBuilder(
          builder: (context, bounds) {
            final gap = 12.0;
            final width = (bounds.maxWidth - gap) / 2;
            return Wrap(
              spacing: gap,
              runSpacing: gap,
              children: [
                if (absenceSection != null)
                  SizedBox(
                    width: width,
                    child: _parentQuickAction(
                      title: 'Báo vắng ăn',
                      subtitle: 'Gửi thông tin không ăn',
                      icon: Icons.event_busy_outlined,
                      operation: absenceSection.operation,
                      background: theme.brightness == Brightness.dark
                          ? MealTraceColors.darkAbsenceTile
                          : MealTraceColors.absenceTile,
                      foreground: theme.brightness == Brightness.dark
                          ? MealTraceColors.darkAbsenceTileInk
                          : MealTraceColors.absenceTileInk,
                    ),
                  ),
                if (linksSection != null)
                  SizedBox(
                    width: width,
                    child: _parentQuickAction(
                      title: 'Liên kết trẻ',
                      subtitle: 'Quản lý hồ sơ trẻ',
                      icon: Icons.link_outlined,
                      operation: linksSection.operation,
                      background: theme.brightness == Brightness.dark
                          ? MealTraceColors.darkPeopleTile
                          : MealTraceColors.peopleTile,
                      foreground: theme.brightness == Brightness.dark
                          ? MealTraceColors.darkPeopleTileInk
                          : MealTraceColors.peopleTileInk,
                    ),
                  ),
              ],
            );
          },
        ),
        const SizedBox(height: 18),
        _parentMealCard(),
        const SizedBox(height: 14),
        Card(
          color: theme.brightness == Brightness.dark
              ? MealTraceColors.darkVioletContainer
              : colors.secondaryContainer,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.info_outline, color: colors.secondary),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Lưu ý báo vắng',
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 5),
                      Text(
                        'Thời điểm chốt suất do nhà trường quy định. Thông tin gửi sau giờ chốt vẫn được lưu trong lịch sử và có thể cần xác nhận.',
                        style: theme.textTheme.bodySmall?.copyWith(
                          height: 1.45,
                        ),
                      ),
                      if (absenceSection != null) ...[
                        const SizedBox(height: 10),
                        TextButton.icon(
                          onPressed: () => open(absenceSection),
                          icon: const Icon(Icons.event_note_outlined, size: 17),
                          label: const Text('Xem báo vắng'),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _parentChildCard(SchoolSection? linksSection) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return FutureBuilder<SchoolResult>(
      future: _parentChildren,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Card(
            child: Padding(
              padding: EdgeInsets.all(22),
              child: Center(child: CircularProgressIndicator()),
            ),
          );
        }
        if (snapshot.hasError) {
          return Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Chưa tải được hồ sơ trẻ.'),
                  const SizedBox(height: 8),
                  TextButton.icon(
                    onPressed: () => setState(() {
                      _parentChildren = controller.execute(
                        SchoolOperation.children,
                      );
                    }),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Thử tải lại'),
                  ),
                ],
              ),
            ),
          );
        }

        final children = snapshot.data?.records ?? const <SchoolRecord>[];
        if (children.isEmpty) {
          return Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  CircleAvatar(
                    backgroundColor: colors.tertiaryContainer,
                    foregroundColor: colors.onTertiaryContainer,
                    child: const Icon(Icons.child_care_outlined),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Chưa có trẻ được liên kết',
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 3),
                        Text(
                          'Gửi yêu cầu liên kết để xem thông tin của trẻ.',
                          style: theme.textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                  if (linksSection != null)
                    IconButton(
                      tooltip: 'Liên kết trẻ',
                      onPressed: () => open(linksSection),
                      icon: const Icon(Icons.chevron_right),
                    ),
                ],
              ),
            ),
          );
        }

        final index = _selectedChildIndex.clamp(0, children.length - 1);
        final child = children[index];
        final initials = child.text('fullName').trim().isEmpty
            ? '?'
            : child.text('fullName').trim().substring(0, 1).toUpperCase();
        return Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                Row(
                  children: [
                    CircleAvatar(
                      radius: 26,
                      backgroundColor: colors.tertiaryContainer,
                      foregroundColor: colors.onTertiaryContainer,
                      child: Text(
                        initials,
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            child.text('fullName'),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Wrap(
                            spacing: 6,
                            runSpacing: 4,
                            children: [
                              _parentBadge(
                                child.text('className').isEmpty
                                    ? 'Đã liên kết'
                                    : child.text('className'),
                                colors.secondaryContainer,
                                colors.onSecondaryContainer,
                              ),
                              _parentBadge(
                                'Đã liên kết',
                                colors.tertiaryContainer,
                                colors.onTertiaryContainer,
                              ),
                            ],
                          ),
                          const SizedBox(height: 4),
                          Text(
                            'Mã trẻ: ${child.text('studentCode')}',
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: colors.onSurfaceVariant,
                            ),
                          ),
                        ],
                      ),
                    ),
                    if (children.length > 1)
                      IconButton(
                        tooltip: 'Chuyển hồ sơ trẻ',
                        onPressed: () => setState(
                          () => _selectedChildIndex =
                              (_selectedChildIndex + 1) % children.length,
                        ),
                        icon: const Icon(Icons.sync),
                      ),
                  ],
                ),
                const SizedBox(height: 16),
                Divider(height: 1, color: colors.outlineVariant),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: _parentInfoValue(
                        icon: Icons.restaurant_outlined,
                        label: 'Bữa trưa hôm nay',
                        value: 'Chưa có dữ liệu',
                        color: colors.primary,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _parentInfoValue(
                        icon: Icons.calendar_month_outlined,
                        label: 'Số dư bữa',
                        value: 'Chưa có dữ liệu',
                        color: colors.secondary,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _parentQuickAction({
    required String title,
    required String subtitle,
    required IconData icon,
    required SchoolOperation operation,
    required Color background,
    required Color foreground,
  }) {
    final section = sections
        .where((item) => item.operation == operation)
        .firstOrNull;
    return Card(
      color: background,
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(20),
        onTap: section == null
            ? null
            : () {
                if (widget.auth.user!.roles.contains('PARENT') &&
                    operation == SchoolOperation.absences) {
                  setState(() {
                    tab = 1;
                    primaryVisited = true;
                  });
                } else {
                  open(section);
                }
              },
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, color: foreground, size: 24),
              const SizedBox(height: 14),
              Text(
                title,
                style: Theme.of(
                  context,
                ).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 3),
              Text(
                subtitle,
                maxLines: 2,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _parentMealCard() {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final now = DateTime.now();
    const weekdayNames = [
      'Thứ Hai',
      'Thứ Ba',
      'Thứ Tư',
      'Thứ Năm',
      'Thứ Sáu',
      'Thứ Bảy',
      'Chủ nhật',
    ];
    final date =
        '${weekdayNames[now.weekday - 1]}, ${now.day.toString().padLeft(2, '0')}/${now.month.toString().padLeft(2, '0')}';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.ramen_dining_outlined, color: colors.secondary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Bữa trưa hôm nay',
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                _parentBadge(
                  date,
                  colors.secondaryContainer,
                  colors.onSecondaryContainer,
                ),
              ],
            ),
            const SizedBox(height: 14),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16),
                gradient: LinearGradient(
                  colors: theme.brightness == Brightness.dark
                      ? [MealTraceColors.darkMealTile, colors.surfaceContainer]
                      : [MealTraceColors.mealTile, colors.surface],
                ),
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.soup_kitchen_outlined,
                    size: 30,
                    color: theme.brightness == Brightness.dark
                        ? MealTraceColors.darkMealTileInk
                        : MealTraceColors.mealTileInk,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'THỰC ĐƠN',
                          style: theme.textTheme.labelSmall?.copyWith(
                            letterSpacing: 1.1,
                            fontWeight: FontWeight.w800,
                            color: colors.onSurfaceVariant,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'Thông tin bữa ăn',
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 3),
                        Text(
                          'Hiện chưa có dữ liệu thực đơn trên ứng dụng.',
                          style: theme.textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Icon(
                  Icons.verified_user_outlined,
                  size: 17,
                  color: colors.tertiary,
                ),
                const SizedBox(width: 7),
                Expanded(
                  child: Text(
                    'Thông tin bữa ăn do nhà trường cập nhật.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _parentBadge(String label, Color background, Color foreground) =>
      Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(30),
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
            color: foreground,
            fontWeight: FontWeight.w700,
          ),
        ),
      );

  Widget _parentInfoValue({
    required IconData icon,
    required String label,
    required String value,
    required Color color,
  }) => Row(
    children: [
      Icon(icon, size: 18, color: color),
      const SizedBox(width: 8),
      Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              value,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                fontWeight: FontWeight.w700,
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    ],
  );

  Widget profile() {
    final user = widget.auth.user!;
    if (user.roles.contains('PARENT')) return parentProfile(user);
    if (user.roles.contains('TEACHER')) return teacherProfile(user);

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
        if (widget.onThemeModeChanged != null) ...[
          Card(
            child: ThemeModeSelector(
              themeMode: widget.themeMode,
              onChanged: widget.onThemeModeChanged!,
            ),
          ),
          const SizedBox(height: 12),
        ],
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

  Widget teacherProfile(CurrentUser user) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final initial = user.fullName.trim().isEmpty
        ? '?'
        : user.fullName.trim().substring(0, 1).toUpperCase();
    final roleLabel = user.roles
        .map((role) => roleLabels[role] ?? role)
        .join(' · ');

    return ListView(
      key: const PageStorageKey('teacher-profile'),
      padding: const EdgeInsets.fromLTRB(18, 14, 18, 22),
      children: [
        Column(
          children: [
            CircleAvatar(
              radius: 38,
              backgroundColor: colors.secondaryContainer,
              foregroundColor: colors.onSecondaryContainer,
              child: Text(
                initial,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            const SizedBox(height: 9),
            Text(
              user.fullName,
              textAlign: TextAlign.center,
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 6),
            _profilePill(
              'Giáo viên',
              colors.secondaryContainer,
              colors.onSecondaryContainer,
            ),
          ],
        ),
        const SizedBox(height: 20),
        _profileSectionLabel('THÔNG TIN LIÊN HỆ & ĐỊNH DANH'),
        Card(
          child: Column(
            children: [
              if (user.phoneNumber?.isNotEmpty == true)
                ListTile(
                  leading: _profileIcon(Icons.phone_outlined),
                  title: const Text('Số điện thoại'),
                  subtitle: Text(user.phoneNumber!),
                ),
              if (user.email.isNotEmpty)
                ListTile(
                  leading: _profileIcon(Icons.mail_outline),
                  title: const Text('Email đăng nhập'),
                  subtitle: Text(user.email),
                ),
              ListTile(
                leading: _profileIcon(Icons.badge_outlined),
                title: const Text('Vai trò hệ thống'),
                subtitle: Text(roleLabel),
                trailing: _profilePill(
                  'Giáo viên',
                  colors.secondaryContainer,
                  colors.onSecondaryContainer,
                ),
              ),
              if (user.inspectorAccessUntil != null)
                ListTile(
                  leading: _profileIcon(Icons.verified_user_outlined),
                  title: const Text('Quyền thanh tra đến'),
                  subtitle: Text(user.inspectorAccessUntil!),
                ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        _profileSectionLabel('CÀI ĐẶT ỨNG DỤNG'),
        Card(
          child: Column(
            children: [
              if (widget.onThemeModeChanged != null)
                ThemeModeSelector(
                  themeMode: widget.themeMode,
                  onChanged: widget.onThemeModeChanged!,
                ),
              ListTile(
                leading: _profileIcon(Icons.translate_outlined),
                title: const Text('Ngôn ngữ'),
                subtitle: const Text('Khu vực: Việt Nam'),
                trailing: _profilePill(
                  'Tiếng Việt',
                  colors.secondaryContainer,
                  colors.onSecondaryContainer,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        _profileSectionLabel('BẢO MẬT & QUYỀN RIÊNG TƯ'),
        Card(
          child: Column(
            children: [
              ListTile(
                leading: _profileIcon(Icons.lock_outline),
                title: const Text('Đổi mật khẩu'),
                subtitle: const Text('Cập nhật mật khẩu tài khoản'),
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
                leading: Icon(Icons.logout, color: colors.error),
                title: const Text('Đăng xuất tài khoản'),
                subtitle: const Text('Thoát phiên đăng nhập trên thiết bị'),
                trailing: Icon(Icons.logout, color: colors.error),
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
        const SizedBox(height: 14),
        Center(
          child: Text(
            'MealTrace School Portal · Giáo viên',
            style: theme.textTheme.labelSmall?.copyWith(
              color: colors.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }

  Widget parentProfile(CurrentUser user) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final initial = user.fullName.trim().isEmpty
        ? '?'
        : user.fullName.trim().substring(0, 1).toUpperCase();
    return ListView(
      key: const PageStorageKey('parent-profile'),
      padding: const EdgeInsets.fromLTRB(18, 14, 18, 22),
      children: [
        Column(
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                CircleAvatar(
                  radius: 37,
                  backgroundColor: colors.secondaryContainer,
                  foregroundColor: colors.onSecondaryContainer,
                  child: Text(
                    initial,
                    style: theme.textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                Positioned(
                  right: 0,
                  bottom: 1,
                  child: Container(
                    width: 15,
                    height: 15,
                    decoration: BoxDecoration(
                      color: colors.tertiary,
                      shape: BoxShape.circle,
                      border: Border.all(color: colors.surface, width: 3),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              user.fullName,
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              'Phụ huynh',
              style: theme.textTheme.bodySmall?.copyWith(
                color: colors.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 6),
            FutureBuilder<SchoolResult>(
              future: _parentChildren,
              builder: (context, snapshot) {
                final children =
                    snapshot.data?.records ?? const <SchoolRecord>[];
                if (children.isEmpty) {
                  return Text(
                    snapshot.connectionState == ConnectionState.waiting
                        ? 'Đang tải thông tin trẻ…'
                        : user.phoneNumber ?? user.email,
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  );
                }
                final child =
                    children[_selectedChildIndex.clamp(0, children.length - 1)];
                return Column(
                  children: [
                    Text(
                      child.text('fullName'),
                      textAlign: TextAlign.center,
                      style: theme.textTheme.labelMedium?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Wrap(
                      alignment: WrapAlignment.center,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      spacing: 5,
                      runSpacing: 3,
                      children: [
                        Icon(
                          Icons.check_circle,
                          size: 12,
                          color: colors.tertiary,
                        ),
                        Text(
                          'Đã liên kết',
                          style: theme.textTheme.labelSmall?.copyWith(
                            color: colors.tertiary,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        if (child.text('className').isNotEmpty)
                          Text(
                            'Lớp ${child.text('className')}',
                            style: theme.textTheme.labelSmall?.copyWith(
                              color: colors.onSurfaceVariant,
                            ),
                          ),
                        if (child.text('studentCode').isNotEmpty)
                          Text(
                            'Mã HS: ${child.text('studentCode')}',
                            style: theme.textTheme.labelSmall?.copyWith(
                              color: colors.onSurfaceVariant,
                            ),
                          ),
                      ],
                    ),
                  ],
                );
              },
            ),
          ],
        ),
        const SizedBox(height: 18),
        _profileSectionLabel('THÔNG TIN LIÊN HỆ & ĐỊNH DANH'),
        Card(
          child: Column(
            children: [
              if (user.phoneNumber?.isNotEmpty == true)
                ListTile(
                  leading: _profileIcon(Icons.phone_outlined),
                  title: const Text('Số điện thoại'),
                  subtitle: Text(user.phoneNumber!),
                ),
              if (user.email.isNotEmpty)
                ListTile(
                  leading: _profileIcon(Icons.mail_outline),
                  title: const Text('Email đăng nhập'),
                  subtitle: Text(user.email),
                ),
              ListTile(
                leading: _profileIcon(Icons.badge_outlined),
                title: const Text('Vai trò hệ thống'),
                subtitle: const Text('Phụ huynh'),
                trailing: _profilePill(
                  'Phụ huynh',
                  colors.secondaryContainer,
                  colors.onSecondaryContainer,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        _profileSectionLabel('CÀI ĐẶT ỨNG DỤNG'),
        Card(
          child: Column(
            children: [
              if (widget.onThemeModeChanged != null)
                ThemeModeSelector(
                  themeMode: widget.themeMode,
                  onChanged: widget.onThemeModeChanged!,
                ),
              ListTile(
                leading: _profileIcon(Icons.translate_outlined),
                title: const Text('Ngôn ngữ'),
                subtitle: const Text('Khu vực: Việt Nam'),
                trailing: _profilePill(
                  'Tiếng Việt',
                  colors.secondaryContainer,
                  colors.onSecondaryContainer,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        _profileSectionLabel('BẢO MẬT & QUYỀN RIÊNG TƯ'),
        Card(
          child: Column(
            children: [
              ListTile(
                leading: _profileIcon(Icons.lock_outline),
                title: const Text('Đổi mật khẩu'),
                subtitle: const Text('Cập nhật mật khẩu tài khoản'),
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
                leading: Icon(Icons.logout, color: colors.error),
                title: const Text('Đăng xuất tài khoản'),
                subtitle: const Text('Thoát phiên đăng nhập trên thiết bị'),
                trailing: Icon(Icons.logout, color: colors.error),
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
        const SizedBox(height: 14),
        Center(
          child: Text(
            'MealTrace School Portal · Phụ huynh',
            style: theme.textTheme.labelSmall?.copyWith(
              color: colors.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }

  Widget _profileSectionLabel(String label) => Padding(
    padding: const EdgeInsets.fromLTRB(4, 0, 4, 6),
    child: Text(
      label,
      style: Theme.of(context).textTheme.labelSmall?.copyWith(
        color: Theme.of(context).colorScheme.onSurfaceVariant,
        letterSpacing: .7,
        fontWeight: FontWeight.w800,
      ),
    ),
  );

  Widget _profileIcon(IconData icon) => CircleAvatar(
    radius: 17,
    backgroundColor: Theme.of(context).colorScheme.surfaceContainerHigh,
    foregroundColor: Theme.of(context).colorScheme.secondary,
    child: Icon(icon, size: 17),
  );

  Widget _profilePill(String label, Color background, Color foreground) =>
      Container(
        padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(20),
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
            color: foreground,
            fontWeight: FontWeight.w700,
          ),
        ),
      );

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
                title:
                    widget.auth.user!.roles.contains('PARENT') &&
                        (tab == 0 || tab == 3)
                    ? const MealTraceBrand(iconSize: 32, fontSize: 19)
                    : widget.auth.user!.roles.contains('TEACHER') &&
                          (tab == 0 || tab == 3)
                    ? const MealTraceBrand(iconSize: 32, fontSize: 19)
                    : tab == 3
                    ? const Text('Hồ sơ')
                    : const MealTraceBrand(iconSize: 34, fontSize: 20),
                actions: [
                  if (widget.auth.user!.roles.contains('PARENT') &&
                      (tab == 0 || tab == 3))
                    Padding(
                      padding: const EdgeInsets.only(right: 2),
                      child: Stack(
                        clipBehavior: Clip.none,
                        children: [
                          Icon(
                            Icons.notifications_none,
                            color: Theme.of(context).colorScheme.secondary,
                          ),
                          Positioned(
                            right: -1,
                            top: -1,
                            child: Container(
                              width: 7,
                              height: 7,
                              decoration: BoxDecoration(
                                color: Theme.of(context).colorScheme.tertiary,
                                shape: BoxShape.circle,
                                border: Border.all(
                                  color: Theme.of(context).colorScheme.surface,
                                  width: 1,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  if (widget.auth.user!.roles.contains('PARENT') &&
                      (tab == 0 || tab == 3))
                    Padding(
                      padding: const EdgeInsets.only(right: 12),
                      child: IconButton.filledTonal(
                        tooltip: 'Hồ sơ',
                        onPressed: () => setState(() => tab = 3),
                        icon: Text(
                          widget.auth.user!.fullName.trim().isEmpty
                              ? '?'
                              : widget.auth.user!.fullName
                                    .trim()
                                    .substring(0, 1)
                                    .toUpperCase(),
                          style: const TextStyle(fontWeight: FontWeight.w800),
                        ),
                      ),
                    ),
                  if (widget.auth.user!.roles.contains('TEACHER') &&
                      (tab == 0 || tab == 3))
                    Padding(
                      padding: const EdgeInsets.only(right: 12),
                      child: Semantics(
                        label: 'Thông báo',
                        child: Stack(
                          clipBehavior: Clip.none,
                          children: [
                            Icon(
                              Icons.notifications_none_rounded,
                              color: Theme.of(context).colorScheme.secondary,
                              size: 21,
                            ),
                            Positioned(
                              right: -1,
                              top: -1,
                              child: Container(
                                width: 7,
                                height: 7,
                                decoration: BoxDecoration(
                                  color: Theme.of(context).colorScheme.tertiary,
                                  shape: BoxShape.circle,
                                  border: Border.all(
                                    color: Theme.of(
                                      context,
                                    ).colorScheme.surface,
                                    width: 1,
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  if (widget.auth.user!.roles.contains('TEACHER') &&
                      (tab == 0 || tab == 3))
                    Padding(
                      padding: const EdgeInsets.only(right: 14),
                      child: CircleAvatar(
                        radius: 16,
                        backgroundColor: Theme.of(
                          context,
                        ).colorScheme.secondaryContainer,
                        foregroundColor: Theme.of(
                          context,
                        ).colorScheme.onSecondaryContainer,
                        child: Text(
                          widget.auth.user!.fullName.trim().isEmpty
                              ? '?'
                              : widget.auth.user!.fullName
                                    .trim()
                                    .substring(0, 1)
                                    .toUpperCase(),
                          style: const TextStyle(fontWeight: FontWeight.w800),
                        ),
                      ),
                    ),
                ],
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
                      parentName: widget.auth.user!.fullName,
                      onOpenParentProfile:
                          widget.auth.user!.roles.contains('PARENT')
                          ? () => setState(() => tab = 3)
                          : null,
                      onParentTabSelected:
                          widget.auth.user!.roles.contains('PARENT')
                          ? (index) {
                              if (index == 2) {
                                setState(() => tab = 0);
                                WidgetsBinding.instance.addPostFrameCallback((
                                  _,
                                ) {
                                  if (mounted) showFunctions();
                                });
                              } else {
                                setState(() {
                                  tab = index;
                                  if (index == 1) primaryVisited = true;
                                });
                              }
                            }
                          : null,
                    )
                  : const SizedBox(),
              profile(),
            ],
          ),
        ),
        bottomNavigationBar: Container(
          margin: const EdgeInsets.fromLTRB(12, 4, 12, 8),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainer,
            borderRadius: BorderRadius.circular(24),
            border: Border.all(
              color: Theme.of(context).colorScheme.outlineVariant,
            ),
          ),
          clipBehavior: Clip.antiAlias,
          child: NavigationBar(
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
      ),
    );
  }
}

({Color background, Color foreground}) _quickAccessPalette(
  SchoolOperation operation,
  bool dark,
) {
  if (const {
    SchoolOperation.workflowDays,
    SchoolOperation.portions,
    SchoolOperation.settle,
    SchoolOperation.decisions,
    SchoolOperation.amendments,
    SchoolOperation.requestAmendment,
    SchoolOperation.reviewAmendment,
    SchoolOperation.meals,
    SchoolOperation.mealDetail,
  }.contains(operation)) {
    return (
      background: dark
          ? MealTraceColors.darkMealTile
          : MealTraceColors.mealTile,
      foreground: dark
          ? MealTraceColors.darkMealTileInk
          : MealTraceColors.mealTileInk,
    );
  }
  if (const {
    SchoolOperation.absences,
    SchoolOperation.reportAbsence,
    SchoolOperation.replaceAbsence,
    SchoolOperation.cancelAbsence,
  }.contains(operation)) {
    return (
      background: dark
          ? MealTraceColors.darkAbsenceTile
          : MealTraceColors.absenceTile,
      foreground: dark
          ? MealTraceColors.darkAbsenceTileInk
          : MealTraceColors.absenceTileInk,
    );
  }
  if (const {
    SchoolOperation.children,
    SchoolOperation.students,
    SchoolOperation.scopedStudents,
    SchoolOperation.classes,
    SchoolOperation.assignedClasses,
    SchoolOperation.classStudents,
  }.contains(operation)) {
    return (
      background: dark
          ? MealTraceColors.darkPeopleTile
          : MealTraceColors.peopleTile,
      foreground: dark
          ? MealTraceColors.darkPeopleTileInk
          : MealTraceColors.peopleTileInk,
    );
  }
  if (const {
    SchoolOperation.users,
    SchoolOperation.parentLinks,
    SchoolOperation.parentLinkClasses,
    SchoolOperation.reviewLinkClasses,
    SchoolOperation.reviewableParentLinks,
    SchoolOperation.createParentLink,
    SchoolOperation.reviewParentLink,
    SchoolOperation.bulkReviewParentLinks,
    SchoolOperation.revokeParentLink,
    SchoolOperation.cancelParentLink,
    SchoolOperation.linkParent,
  }.contains(operation)) {
    return (
      background: dark
          ? MealTraceColors.darkAccountTile
          : MealTraceColors.accountTile,
      foreground: dark
          ? MealTraceColors.darkAccountTileInk
          : MealTraceColors.accountTileInk,
    );
  }
  return (
    background: dark
        ? MealTraceColors.darkCalendarTile
        : MealTraceColors.calendarTile,
    foreground: dark
        ? MealTraceColors.darkCalendarTileInk
        : MealTraceColors.calendarTileInk,
  );
}

import 'package:flutter/material.dart';

import '../../../core/presentation/meal_widgets.dart';
import '../domain/school_models.dart';
import 'kitchen_menu_editor.dart';
import 'kitchen_menu_insights.dart';
import 'kitchen_workflow_widgets.dart';
import 'school_controller.dart';

class KitchenMenusPage extends StatefulWidget {
  const KitchenMenusPage({super.key, required this.controller});
  final SchoolController controller;

  @override
  State<KitchenMenusPage> createState() => _KitchenMenusPageState();
}

class _KitchenMenusPageState extends State<KitchenMenusPage> {
  late Future<SchoolResult> request;
  String from = schoolToday();
  String to = '';
  int page = 1;

  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    SchoolOperation.menus,
    input: {'from': from, 'to': to, 'page': page, 'pageSize': 20},
  );

  void reload([int? value]) {
    if (from.isNotEmpty && to.isNotEmpty && from.compareTo(to) > 0) {
      workflowNotice(context, 'Đến ngày phải từ ngày bắt đầu trở đi.');
      return;
    }
    setState(() {
      page = value ?? page;
      request = load();
    });
  }

  Future<void> create() async {
    final saved = await openWorkflow<bool>(
      context,
      KitchenMenuEditor(controller: widget.controller),
    );
    if (mounted && saved == true) {
      workflowNotice(context, 'Đã lưu bản nháp.');
      reload();
    }
  }

  Future<void> details(SchoolRecord menu) async {
    await openWorkflow<void>(
      context,
      _MenuDetail(controller: widget.controller, id: menu.text('id')),
    );
    if (mounted) reload();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Thực đơn')),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          FilledButton.icon(
            onPressed: create,
            icon: const Icon(Icons.add),
            label: const Text('Tạo thực đơn'),
          ),
          ExpansionTile(
            title: const Text('Bộ lọc ngày'),
            children: [
              WorkflowDate(
                label: 'Từ ngày',
                value: from,
                emptyAllowed: true,
                onChanged: (value) => setState(() => from = value),
              ),
              WorkflowDate(
                label: 'Đến ngày',
                value: to,
                emptyAllowed: true,
                onChanged: (value) => setState(() => to = value),
              ),
              TextButton(
                onPressed: () => reload(1),
                child: const Text('Áp dụng'),
              ),
            ],
          ),
          FutureBuilder<SchoolResult>(
            future: request,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: reload,
              builder: (result) => Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (result.records.isEmpty)
                    const MealEmpty('Chưa có thực đơn trong khoảng ngày này.'),
                  for (final item in result.records)
                    WorkflowCard(
                      title:
                          "${mealName(item.text('mealType'))} · ${shortDate(item.text('date'))}",
                      subtitle: menuStateLabel(
                        item.record('latest')?.text('state') ?? '',
                      ),
                      onTap: () => details(item),
                    ),
                  WorkflowPager(result: result, page: page, onChange: reload),
                ],
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class _MenuDetail extends StatefulWidget {
  const _MenuDetail({required this.controller, required this.id});
  final SchoolController controller;
  final String id;

  @override
  State<_MenuDetail> createState() => _MenuDetailState();
}

class _MenuDetailState extends State<_MenuDetail> {
  late Future<SchoolResult> request;

  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    SchoolOperation.menuDetail,
    context: {'id': widget.id},
  );
  void reload() => setState(() {
    request = load();
  });

  Future<void> edit(SchoolRecord menu) async {
    final saved = await openWorkflow<bool>(
      context,
      KitchenMenuEditor(controller: widget.controller, menu: menu),
    );
    if (mounted && saved == true) {
      workflowNotice(context, 'Đã lưu bản nháp mới.');
      reload();
    }
  }

  Future<void> transition(
    SchoolOperation operation,
    int version, {
    String? decision,
  }) async {
    final saved = await openWorkflow<bool>(
      context,
      _MenuTransition(
        controller: widget.controller,
        operation: operation,
        id: widget.id,
        version: version,
        decision: decision,
      ),
    );
    if (mounted && saved == true) {
      workflowNotice(context, 'Đã cập nhật thực đơn.');
      reload();
    }
  }

  List<MealAction> actions(SchoolRecord menu, SchoolRecord latest) {
    final state = latest.text('state');
    final version = latest.number('version');
    return [
      if (state != 'submitted') MealAction('Tạo bản mới', () => edit(menu)),
      if (state == 'draft')
        MealAction(
          'Gửi duyệt',
          () => transition(SchoolOperation.submitMenu, version),
          primary: true,
        ),
      if (state == 'submitted' &&
          canExecute(SchoolOperation.reviewMenu, widget.controller.roles)) ...[
        MealAction(
          'Từ chối',
          () => transition(
            SchoolOperation.reviewMenu,
            version,
            decision: 'reject',
          ),
        ),
        MealAction(
          'Duyệt',
          () => transition(
            SchoolOperation.reviewMenu,
            version,
            decision: 'approve',
          ),
          primary: true,
        ),
      ],
      if (state == 'approved' &&
          canExecute(SchoolOperation.publishMenu, widget.controller.roles))
        MealAction(
          'Công bố',
          () => transition(SchoolOperation.publishMenu, version),
          primary: true,
        ),
    ];
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Chi tiết thực đơn'),
      actions: [
        IconButton(
          tooltip: 'Tải lại',
          onPressed: reload,
          icon: const Icon(Icons.refresh),
        ),
      ],
    ),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          FutureBuilder<SchoolResult>(
            future: request,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: reload,
              builder: (result) {
                final menu = result.summary;
                final latest = menu.record('latest');
                if (latest == null) {
                  return const MealEmpty('Chưa có thông tin phiên bản.');
                }
                final published = menu.number('publishedVersion');
                final state = latest.text('state');
                final version = latest.number('version');
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    MealSectionTitle(
                      "${mealName(menu.text('mealType'))} · ${shortDate(menu.text('date'))}",
                      subtitle: 'Bản $version',
                    ),
                    MealStatus(
                      menuStateLabel(state),
                      positive: state == 'published' || state == 'approved',
                    ),
                    if (published > 0 && published != version)
                      Text('Phụ huynh đang xem bản $published.'),
                    if (latest.text('reason').isNotEmpty)
                      Text(latest.text('reason')),
                    for (final item in latest.records('dishes'))
                      WorkflowCard(
                        title: item.text('recipeName'),
                        subtitle:
                            "Công thức bản ${item.number('recipeVersion')}${item['plannedPortions'] == null ? '' : " · ${item.number('plannedPortions')} suất dự kiến"}",
                      ),
                    const SizedBox(height: 12),
                    MealActions(
                      actions: actions(menu, latest),
                      menuLabel: 'Thao tác thực đơn',
                    ),
                    const SizedBox(height: 12),
                    WorkflowCard(
                      title: 'Lịch sử thực đơn',
                      onTap: () => openWorkflow<void>(
                        context,
                        _MenuHistory(
                          controller: widget.controller,
                          id: widget.id,
                        ),
                      ),
                    ),
                    if (published > 0) ...[
                      WorkflowCard(
                        title: 'Dinh dưỡng',
                        subtitle: 'Xem bản đã công bố hoặc tính lại',
                        onTap: () => openWorkflow<void>(
                          context,
                          KitchenMenuNutrition(
                            controller: widget.controller,
                            id: widget.id,
                            version: published,
                          ),
                        ),
                      ),
                      WorkflowCard(
                        title: 'Dự trù nguyên liệu',
                        subtitle: 'Dựa trên số suất đã chốt',
                        onTap: () => openWorkflow<void>(
                          context,
                          KitchenMenuEstimate(
                            controller: widget.controller,
                            menu: menu,
                          ),
                        ),
                      ),
                    ],
                  ],
                );
              },
            ),
          ),
        ],
      ),
    ),
  );
}

class _MenuTransition extends StatefulWidget {
  const _MenuTransition({
    required this.controller,
    required this.operation,
    required this.id,
    required this.version,
    this.decision,
  });
  final SchoolController controller;
  final SchoolOperation operation;
  final String id;
  final int version;
  final String? decision;

  @override
  State<_MenuTransition> createState() => _MenuTransitionState();
}

class _MenuTransitionState extends State<_MenuTransition> {
  final text = TextEditingController();
  @override
  void dispose() {
    text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final publishing = widget.operation == SchoolOperation.publishMenu;
    final title = switch (widget.operation) {
      SchoolOperation.submitMenu => 'Gửi duyệt',
      SchoolOperation.publishMenu => 'Công bố',
      _ => widget.decision == 'reject' ? 'Từ chối' : 'Duyệt',
    };
    return WorkflowSavePage(
      title: '$title bản ${widget.version}',
      buttonLabel: title,
      save: () async {
        await widget.controller.execute(
          widget.operation,
          context: {'id': widget.id},
          input: {
            'expectedVersion': widget.version,
            if (widget.decision != null) ...{
              'decision': widget.decision,
              'reason': optionalWorkflowText(text.text),
            },
            if (publishing) 'ageGroup': optionalWorkflowText(text.text),
          },
        );
        return true;
      },
      children: [
        if (widget.decision != null || publishing)
          WorkflowField(
            label: publishing
                ? 'Nhóm tuổi (tùy chọn)'
                : (widget.decision == 'reject'
                      ? 'Lý do từ chối'
                      : 'Ghi chú duyệt'),
            controller: text,
            maxLength: publishing ? 50 : 500,
            requiredField: widget.decision == 'reject',
          ),
      ],
    );
  }
}

class _MenuHistory extends StatefulWidget {
  const _MenuHistory({required this.controller, required this.id});
  final SchoolController controller;
  final String id;

  @override
  State<_MenuHistory> createState() => _MenuHistoryState();
}

class _MenuHistoryState extends State<_MenuHistory> {
  late Future<SchoolResult> request;
  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    SchoolOperation.menuHistory,
    context: {'id': widget.id},
  );

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Lịch sử thực đơn')),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          FutureBuilder<SchoolResult>(
            future: request,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: () => setState(() {
                request = load();
              }),
              builder: (result) => Column(
                children: [
                  if (result.records.isEmpty)
                    const MealEmpty('Chưa có lịch sử thực đơn.'),
                  for (final version in result.records.reversed)
                    Card(
                      child: ExpansionTile(
                        title: Text(
                          "Bản ${version.number('version')} · ${menuStateLabel(version.text('state'))}",
                        ),
                        subtitle: Text(
                          schoolTimestamp(version.text('recordedAt')),
                        ),
                        children: [
                          if (version.text('reason').isNotEmpty)
                            ListTile(title: Text(version.text('reason'))),
                          for (final item in version.records('dishes'))
                            ListTile(
                              title: Text(item.text('recipeName')),
                              subtitle: Text(
                                "Công thức bản ${item.number('recipeVersion')}",
                              ),
                            ),
                          for (final item in version.records('transitions'))
                            ListTile(
                              title: Text(menuStateLabel(item.text('state'))),
                              subtitle: Text(
                                "${schoolTimestamp(item.text('at'))}${item.text('reason').isEmpty ? '' : " · ${item.text('reason')}"}",
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
      ),
    ),
  );
}

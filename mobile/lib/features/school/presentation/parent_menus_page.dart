import 'package:flutter/material.dart';

import '../../../core/presentation/meal_widgets.dart';
import '../domain/school_models.dart';
import 'kitchen_workflow_widgets.dart';
import 'school_controller.dart';

class ParentMenusPage extends StatefulWidget {
  const ParentMenusPage({super.key, required this.controller});
  final SchoolController controller;

  @override
  State<ParentMenusPage> createState() => _ParentMenusPageState();
}

class _ParentMenusPageState extends State<ParentMenusPage> {
  late Future<SchoolResult> roster;
  Future<SchoolResult>? request;
  SchoolRecord? child;
  String from = schoolToday();
  late String to = DateTime.parse(
    '${from}T00:00:00Z',
  ).add(const Duration(days: 6)).toIso8601String().substring(0, 10);
  late String appliedFrom = from;
  late String appliedTo = to;

  @override
  void initState() {
    super.initState();
    roster = loadChildren();
  }

  Future<SchoolResult> loadChildren() async {
    final result = await widget.controller.execute(SchoolOperation.children);
    if (mounted) {
      setState(() {
        child = result.records.firstOrNull;
        request = child == null ? null : loadMenus();
      });
    }
    return result;
  }

  Future<SchoolResult> loadMenus() => widget.controller.execute(
    SchoolOperation.parentMenus,
    context: {'studentId': child!.text('studentId')},
    input: {'from': appliedFrom, 'to': appliedTo},
  );

  void filter() {
    final days =
        DateTime.parse(
          '${to}T00:00:00Z',
        ).difference(DateTime.parse('${from}T00:00:00Z')).inDays +
        1;
    if (days < 1 || days > 62) {
      workflowNotice(context, 'Chọn khoảng từ 1 đến 62 ngày.');
      return;
    }
    setState(() {
      appliedFrom = from;
      appliedTo = to;
      request = loadMenus();
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Thực đơn của trẻ')),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          FutureBuilder<SchoolResult>(
            future: roster,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: () => setState(() {
                roster = loadChildren();
              }),
              builder: (data) {
                if (data.records.isEmpty || child == null) {
                  return const MealEmpty(
                    'Chưa có trẻ được liên kết. Vào Liên kết trẻ để gửi yêu cầu.',
                  );
                }
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    DropdownButtonFormField<String>(
                      initialValue: child!.text('studentId'),
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Trẻ'),
                      items: [
                        for (final item in data.records)
                          DropdownMenuItem(
                            value: item.text('studentId'),
                            child: Text(
                              item.title,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: (id) => setState(() {
                        child = data.records.firstWhere(
                          (item) => item.text('studentId') == id,
                        );
                        request = loadMenus();
                      }),
                    ),
                    ExpansionTile(
                      title: const Text('Khoảng ngày'),
                      children: [
                        WorkflowDate(
                          label: 'Từ ngày',
                          value: from,
                          onChanged: (value) => setState(() => from = value),
                        ),
                        WorkflowDate(
                          label: 'Đến ngày',
                          value: to,
                          onChanged: (value) => setState(() => to = value),
                        ),
                        TextButton(
                          onPressed: filter,
                          child: const Text('Xem thực đơn'),
                        ),
                      ],
                    ),
                    Text('${shortDate(appliedFrom)} – ${shortDate(appliedTo)}'),
                    if (request != null)
                      FutureBuilder<SchoolResult>(
                        future: request,
                        builder: (context, menuSnapshot) => WorkflowResultView(
                          snapshot: menuSnapshot,
                          onRetry: () => setState(() {
                            request = loadMenus();
                          }),
                          builder: (result) => Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              if (result.records.isEmpty)
                                const MealEmpty(
                                  'Chưa có thực đơn đã công bố trong khoảng ngày này.',
                                ),
                              for (final item in result.records)
                                WorkflowCard(
                                  title:
                                      "${mealName(item.text('mealType'))} · ${shortDate(item.text('date'))}",
                                  subtitle:
                                      "${item.records('dishes').length} món",
                                  onTap: () => openWorkflow<void>(
                                    context,
                                    _ParentMenuDetail(item),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ),
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

class _ParentMenuDetail extends StatelessWidget {
  const _ParentMenuDetail(this.item);
  final SchoolRecord item;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(mealName(item.text('mealType')))),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          MealSectionTitle('Món ăn', subtitle: shortDate(item.text('date'))),
          for (final dish in item.records('dishes'))
            WorkflowCard(title: dish.text('recipeName')),
          const SizedBox(height: 12),
          const MealSectionTitle('Dinh dưỡng mỗi suất'),
          WorkflowNutrition(item.record('nutrition')),
        ],
      ),
    ),
  );
}

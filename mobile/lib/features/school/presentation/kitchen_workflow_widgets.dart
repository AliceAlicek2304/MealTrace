import 'package:flutter/material.dart';

import '../../../core/presentation/meal_widgets.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';

typedef WorkflowInput = Map<String, Object?>;

String mealName(String value) => switch (value.toLowerCase()) {
  'breakfast' => 'Bữa sáng',
  'lunch' => 'Bữa trưa',
  'snack' => 'Bữa phụ',
  _ => value,
};

String menuStateLabel(String value) => switch (value) {
  'draft' => 'Bản nháp',
  'submitted' => 'Chờ duyệt',
  'approved' => 'Đã duyệt',
  'rejected' => 'Bị từ chối',
  'published' => 'Đã công bố',
  _ => 'Chưa rõ',
};

String shortDate(String value) {
  if (value.length < 10 || DateTime.tryParse(value) == null) return value;
  return '${value.substring(8, 10)}/${value.substring(5, 7)}/${value.substring(0, 4)}';
}

String schoolTimestamp(String value) {
  final date = DateTime.tryParse(value)?.toUtc().add(const Duration(hours: 7));
  if (date == null) return 'Chưa có';
  final stamp = date.toIso8601String();
  return '${shortDate(stamp)} · ${stamp.substring(11, 16)}';
}

String measured(Object? value, String unit) =>
    value is num ? '$value $unit' : 'Chưa có';

void workflowNotice(BuildContext context, String message) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
}

void workflowError(BuildContext context, Object error) {
  workflowNotice(
    context,
    error is SchoolFailure
        ? error.message
        : 'Chưa xử lý được. Vui lòng thử lại.',
  );
}

Future<T?> openWorkflow<T>(BuildContext context, Widget page) =>
    Navigator.of(context).push<T>(MaterialPageRoute(builder: (_) => page));

class WorkflowResultView extends StatelessWidget {
  const WorkflowResultView({
    super.key,
    required this.snapshot,
    required this.onRetry,
    required this.builder,
  });

  final AsyncSnapshot<SchoolResult> snapshot;
  final VoidCallback onRetry;
  final Widget Function(SchoolResult result) builder;

  @override
  Widget build(BuildContext context) {
    if (snapshot.connectionState != ConnectionState.done) {
      return const LinearProgressIndicator();
    }
    if (snapshot.hasError) {
      final error = snapshot.error;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            error is SchoolFailure ? error.message : 'Chưa tải được dữ liệu.',
          ),
          TextButton(onPressed: onRetry, child: const Text('Thử lại')),
        ],
      );
    }
    return builder(snapshot.requireData);
  }
}

class WorkflowCard extends StatelessWidget {
  const WorkflowCard({
    super.key,
    required this.title,
    this.subtitle,
    this.onTap,
  });

  final String title;
  final String? subtitle;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Card(
    child: ListTile(
      title: Text(title, maxLines: 2, overflow: TextOverflow.ellipsis),
      subtitle: subtitle == null ? null : Text(subtitle!),
      trailing: onTap == null ? null : const Icon(Icons.chevron_right),
      onTap: onTap,
    ),
  );
}

class WorkflowDate extends StatelessWidget {
  const WorkflowDate({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
    this.emptyAllowed = false,
    this.enabled = true,
  });

  final String label;
  final String value;
  final ValueChanged<String> onChanged;
  final bool emptyAllowed;
  final bool enabled;

  @override
  Widget build(BuildContext context) => ListTile(
    contentPadding: EdgeInsets.zero,
    enabled: enabled,
    title: Text(label),
    subtitle: Text(value.isEmpty ? 'Chưa chọn' : shortDate(value)),
    trailing: emptyAllowed && value.isNotEmpty
        ? IconButton(
            icon: const Icon(Icons.clear),
            tooltip: 'Bỏ $label',
            onPressed: enabled ? () => onChanged('') : null,
          )
        : const Icon(Icons.event_outlined),
    onTap: () async {
      final date = await showDatePicker(
        context: context,
        initialDate: DateTime.tryParse(value) ?? DateTime.parse(schoolToday()),
        firstDate: DateTime(1900),
        lastDate: DateTime(2100, 12, 31),
      );
      if (context.mounted && date != null) {
        onChanged(date.toIso8601String().substring(0, 10));
      }
    },
  );
}

class WorkflowChooser extends StatefulWidget {
  const WorkflowChooser({
    super.key,
    required this.controller,
    required this.operation,
    required this.title,
    required this.label,
    this.subtitle,
    this.filter = const {},
    this.contextValues = const {},
    this.searchable = false,
    this.paged = true,
    this.accept,
  });

  final SchoolController controller;
  final SchoolOperation operation;
  final String title;
  final String Function(SchoolRecord) label;
  final String Function(SchoolRecord)? subtitle;
  final WorkflowInput filter;
  final WorkflowInput contextValues;
  final bool searchable;
  final bool paged;
  final bool Function(SchoolRecord)? accept;

  @override
  State<WorkflowChooser> createState() => _WorkflowChooserState();
}

class _WorkflowChooserState extends State<WorkflowChooser> {
  final search = TextEditingController();
  late Future<SchoolResult> request;
  var page = 1;

  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    widget.operation,
    context: widget.contextValues,
    input: {
      ...widget.filter,
      if (widget.searchable) 'search': search.text.trim(),
      if (widget.paged) ...{'page': page, 'pageSize': 20},
    },
  );

  void reload([int? value]) => setState(() {
    page = value ?? page;
    request = load();
  });

  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(widget.title)),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (widget.searchable) ...[
            TextField(
              controller: search,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => reload(1),
              decoration: InputDecoration(
                labelText: 'Tìm kiếm',
                suffixIcon: IconButton(
                  tooltip: 'Tìm',
                  onPressed: () => reload(1),
                  icon: const Icon(Icons.search),
                ),
              ),
            ),
            const SizedBox(height: 12),
          ],
          FutureBuilder<SchoolResult>(
            future: request,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: reload,
              builder: (result) {
                final items = result.records.where(
                  (item) => widget.accept?.call(item) ?? true,
                );
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (items.isEmpty)
                      const MealEmpty('Không có dữ liệu phù hợp.'),
                    for (final item in items)
                      WorkflowCard(
                        title: widget.label(item),
                        subtitle: widget.subtitle?.call(item),
                        onTap: () => Navigator.of(context).pop(item),
                      ),
                    if (widget.paged)
                      WorkflowPager(
                        result: result,
                        page: page,
                        onChange: reload,
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

class WorkflowPager extends StatelessWidget {
  const WorkflowPager({
    super.key,
    required this.result,
    required this.page,
    required this.onChange,
  });
  final SchoolResult result;
  final int page;
  final ValueChanged<int> onChange;

  @override
  Widget build(BuildContext context) {
    final pages = (result.total / result.pageSize).ceil();
    if (pages < 2 && page == 1) return const SizedBox.shrink();
    return Row(
      children: [
        IconButton(
          tooltip: 'Trang trước',
          onPressed: page > 1 ? () => onChange(page - 1) : null,
          icon: const Icon(Icons.chevron_left),
        ),
        Expanded(
          child: Text(
            'Trang $page/${pages < 1 ? 1 : pages}',
            textAlign: TextAlign.center,
          ),
        ),
        IconButton(
          tooltip: 'Trang sau',
          onPressed: page < pages ? () => onChange(page + 1) : null,
          icon: const Icon(Icons.chevron_right),
        ),
      ],
    );
  }
}

class WorkflowSavePage extends StatefulWidget {
  const WorkflowSavePage({
    super.key,
    required this.title,
    required this.children,
    required this.save,
    this.buttonLabel = 'Lưu',
  });
  final String title;
  final List<Widget> children;
  final Future<bool> Function() save;
  final String buttonLabel;

  @override
  State<WorkflowSavePage> createState() => _WorkflowSavePageState();
}

class _WorkflowSavePageState extends State<WorkflowSavePage> {
  final form = GlobalKey<FormState>();
  bool busy = false;

  Future<void> submit() async {
    if (busy || !form.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    setState(() => busy = true);
    try {
      final done = await widget.save();
      if (mounted && done) Navigator.of(context).pop(true);
    } on SchoolFailure catch (error) {
      if (mounted) workflowError(context, error);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !busy,
    child: Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      body: SafeArea(
        child: Form(
          key: form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              for (final field in widget.children) ...[
                ExcludeFocus(
                  excluding: busy,
                  child: AbsorbPointer(absorbing: busy, child: field),
                ),
                const SizedBox(height: 12),
              ],
              if (busy) const LinearProgressIndicator(),
              FilledButton(
                onPressed: busy ? null : submit,
                child: Text(widget.buttonLabel),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}

class WorkflowField extends StatelessWidget {
  const WorkflowField({
    super.key,
    required this.label,
    required this.controller,
    required this.maxLength,
    this.requiredField = false,
    this.keyboardType,
  });
  final String label;
  final TextEditingController controller;
  final int maxLength;
  final bool requiredField;
  final TextInputType? keyboardType;

  @override
  Widget build(BuildContext context) => TextFormField(
    controller: controller,
    decoration: InputDecoration(labelText: label, counterText: ''),
    maxLength: maxLength,
    keyboardType: keyboardType,
    validator: requiredField
        ? (value) =>
              (value ?? '').trim().isEmpty ? 'Vui lòng nhập $label.' : null
        : null,
  );
}

String? optionalWorkflowText(String value) =>
    value.trim().isEmpty ? null : value.trim();

class WorkflowNutrition extends StatelessWidget {
  const WorkflowNutrition(this.totals, {super.key});
  final SchoolRecord? totals;

  @override
  Widget build(BuildContext context) {
    final value = totals;
    if (value == null) return const Text('Chưa có số liệu dinh dưỡng.');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        WorkflowCard(
          title: 'Năng lượng',
          subtitle: measured(value['kcal'], 'kcal'),
        ),
        WorkflowCard(title: 'Đạm', subtitle: measured(value['proteinG'], 'g')),
        WorkflowCard(title: 'Chất béo', subtitle: measured(value['fatG'], 'g')),
        WorkflowCard(
          title: 'Carbohydrate',
          subtitle: measured(value['carbG'], 'g'),
        ),
      ],
    );
  }
}

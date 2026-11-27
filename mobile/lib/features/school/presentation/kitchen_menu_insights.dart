import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/presentation/meal_widgets.dart';
import '../domain/school_models.dart';
import 'kitchen_workflow_widgets.dart';
import 'school_controller.dart';

typedef _RecomputeInput = ({
  String? ageGroup,
  String? validAt,
  String? knownAt,
});

class KitchenMenuNutrition extends StatefulWidget {
  const KitchenMenuNutrition({
    super.key,
    required this.controller,
    required this.id,
    required this.version,
  });
  final SchoolController controller;
  final String id;
  final int version;

  @override
  State<KitchenMenuNutrition> createState() => _KitchenMenuNutritionState();
}

class _KitchenMenuNutritionState extends State<KitchenMenuNutrition> {
  late int version = widget.version;
  late Future<SchoolResult> request;
  _RecomputeInput? filters;
  bool get recomputed => filters != null;

  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    SchoolOperation.menuNutrition,
    context: {'id': widget.id},
    input: {
      'version': version,
      'mode': recomputed ? 'recompute' : 'pinned',
      if (filters case final values?) ...{
        'ageGroup': values.ageGroup,
        'validAt': values.validAt,
        'knownAt': values.knownAt,
      },
    },
  );

  Future<void> selectVersion() async {
    final selected = await openWorkflow<SchoolRecord>(
      context,
      WorkflowChooser(
        controller: widget.controller,
        operation: SchoolOperation.menuHistory,
        contextValues: {'id': widget.id},
        paged: false,
        title: 'Chọn bản đã công bố',
        label: (item) => 'Bản ${item.number('version')}',
        accept: (item) => item.text('state') == 'published',
      ),
    );
    if (!mounted || selected == null) return;
    setState(() {
      version = selected.number('version');
      filters = null;
      request = load();
    });
  }

  Future<void> calculate() async {
    final selected = await showDialog<_RecomputeInput>(
      context: context,
      builder: (context) => const _RecomputeDialog(),
    );
    if (!mounted || selected == null) return;
    setState(() {
      filters = selected;
      request = load();
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Dinh dưỡng thực đơn')),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          WorkflowCard(
            title: 'Phiên bản đã công bố',
            subtitle: 'Bản $version',
            onTap: selectVersion,
          ),
          const SizedBox(height: 8),
          MealActions(
            actions: [
              if (recomputed)
                MealAction(
                  'Bản công bố',
                  () => setState(() {
                    filters = null;
                    request = load();
                  }),
                ),
              MealAction('Tính lại', calculate, primary: true),
            ],
          ),
          const SizedBox(height: 12),
          FutureBuilder<SchoolResult>(
            future: request,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: () => setState(() {
                request = load();
              }),
              builder: (result) => _NutritionResult(
                record: result.summary,
                recomputed: recomputed,
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class _RecomputeDialog extends StatefulWidget {
  const _RecomputeDialog();

  @override
  State<_RecomputeDialog> createState() => _RecomputeDialogState();
}

class _RecomputeDialogState extends State<_RecomputeDialog> {
  final text = TextEditingController();
  String? validAt;
  String? knownAt;

  Future<void> chooseTime({required bool known}) async {
    final date = await showDatePicker(
      context: context,
      initialDate: DateTime.parse(schoolToday()),
      firstDate: DateTime(1900),
      lastDate: DateTime(2100, 12, 31),
    );
    if (!mounted || date == null) return;
    final time = await showTimePicker(
      context: context,
      initialTime: const TimeOfDay(hour: 0, minute: 0),
    );
    if (!mounted || time == null) return;
    final day = date.toIso8601String().substring(0, 10);
    final hour = time.hour.toString().padLeft(2, '0');
    final minute = time.minute.toString().padLeft(2, '0');
    final instant = '${day}T$hour:$minute:00+07:00';
    setState(() {
      if (known) {
        knownAt = instant;
      } else {
        validAt = instant;
      }
    });
  }

  @override
  void dispose() {
    text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Dialog(
    insetPadding: const EdgeInsets.all(16),
    constraints: const BoxConstraints(maxWidth: 480),
    child: SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          const MealSectionTitle('Tính lại dinh dưỡng'),
          WorkflowField(
            label: 'Nhóm tuổi (tùy chọn)',
            controller: text,
            maxLength: 50,
          ),
          ExpansionTile(
            title: const Text('Mốc thời gian (giờ Việt Nam)'),
            children: [
              WorkflowCard(
                title: 'Thời điểm áp dụng',
                subtitle: validAt == null
                    ? 'Mặc định'
                    : schoolTimestamp(validAt!),
                onTap: () => chooseTime(known: false),
              ),
              WorkflowCard(
                title: 'Ghi nhận đến',
                subtitle: knownAt == null
                    ? 'Mặc định'
                    : schoolTimestamp(knownAt!),
                onTap: () => chooseTime(known: true),
              ),
              if (validAt != null || knownAt != null)
                TextButton(
                  onPressed: () => setState(() {
                    validAt = null;
                    knownAt = null;
                  }),
                  child: const Text('Về mốc mặc định'),
                ),
            ],
          ),
          const SizedBox(height: 12),
          MealActions(
            actions: [
              MealAction('Hủy', () => Navigator.pop(context)),
              MealAction('Tính lại', () {
                FocusScope.of(context).unfocus();
                Navigator.pop(context, (
                  ageGroup: optionalWorkflowText(text.text),
                  validAt: validAt,
                  knownAt: knownAt,
                ));
              }, primary: true),
            ],
          ),
        ],
      ),
    ),
  );
}

class _NutritionResult extends StatelessWidget {
  const _NutritionResult({required this.record, required this.recomputed});
  final SchoolRecord record;
  final bool recomputed;

  @override
  Widget build(BuildContext context) {
    final payload = record.record('payload');
    if (payload == null) return const MealEmpty('Chưa có số liệu dinh dưỡng.');
    final comparison = payload.record('ageComparison');
    final frozen = record.record('asPublished');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        MealSectionTitle(
          recomputed ? 'Kết quả tính lại' : 'Dinh dưỡng đã công bố',
          subtitle: 'Mỗi suất ăn',
        ),
        WorkflowNutrition(payload.record('total')),
        if (payload.text('ageGroup').isNotEmpty)
          ListTile(
            title: const Text('Nhóm tuổi'),
            subtitle: Text(payload.text('ageGroup')),
          ),
        if (payload.text('ageComparisonStatus') == 'reference_missing')
          const ListTile(
            title: Text('Chưa có khoảng tham chiếu cho nhóm tuổi này.'),
          ),
        if (payload.text('ageComparisonStatus') == 'reference_basis_mismatch')
          const ListTile(
            title: Text('Khoảng tham chiếu chưa phù hợp với bữa ăn.'),
          ),
        if (comparison != null)
          Card(
            child: ExpansionTile(
              title: const Text('Khoảng tham chiếu'),
              children: [
                for (final item in comparison.records('items'))
                  ListTile(
                    title: Text(_nutrientName(item.text('nutrient'))),
                    subtitle: Text(
                      "${item['value'] ?? 'Chưa có'} · Khoảng ${item['min'] ?? '—'} – ${item['max'] ?? '—'}"
                      "\n${_comparisonLabel(item.text('status'))} · ${item.text('source')}",
                    ),
                  ),
              ],
            ),
          ),
        if (frozen != null)
          Card(
            child: ExpansionTile(
              title: const Text('Đối chiếu bản đã công bố'),
              children: [
                const ListTile(title: Text('Đã công bố')),
                WorkflowNutrition(frozen.record('total')),
                const ListTile(title: Text('Chênh lệch')),
                WorkflowNutrition(record.record('deltaFromPublished')),
              ],
            ),
          ),
        for (final item in payload.records('dishes'))
          Card(
            child: ExpansionTile(
              title: Text(item.text('recipeName')),
              children: [WorkflowNutrition(item.record('perServing'))],
            ),
          ),
      ],
    );
  }
}

String _nutrientName(String value) => switch (value) {
  'kcal' => 'Năng lượng',
  'proteinG' => 'Đạm',
  'fatG' => 'Chất béo',
  'carbG' => 'Carbohydrate',
  _ => value,
};
String _comparisonLabel(String value) => switch (value) {
  'below' => 'Dưới khoảng',
  'within' => 'Trong khoảng',
  'above' => 'Vượt khoảng',
  _ => 'Chưa đủ dữ liệu',
};

class KitchenMenuEstimate extends StatefulWidget {
  const KitchenMenuEstimate({
    super.key,
    required this.controller,
    required this.menu,
  });
  final SchoolController controller;
  final SchoolRecord menu;

  @override
  State<KitchenMenuEstimate> createState() => _KitchenMenuEstimateState();
}

class _KitchenMenuEstimateState extends State<KitchenMenuEstimate> {
  late Future<SchoolResult> request;
  Future<SchoolResult>? result;
  String? roomId;
  String? rejectedToken;
  String estimateId = '';
  bool reviewed = false;
  bool busy = false;

  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    SchoolOperation.kitchenDiff,
    context: {'id': widget.menu.text('mealDayId')},
  );

  void reload() => setState(() {
    reviewed = false;
    roomId = null;
    request = load();
  });

  Future<void> create(String token) async {
    if (busy || !reviewed || token == rejectedToken) return;
    final menu = widget.menu;
    final dayId = menu.text('mealDayId');
    setState(() => busy = true);
    try {
      final created = await widget.controller.execute(
        SchoolOperation.createEstimate,
        input: {
          'menuVersionIds': [menu.text('publishedVersionId')],
          'mealDayIds': [dayId],
          'classIds': [if (roomId != null) roomId],
          'snapshotTokens': {dayId: token},
        },
      );
      if (!mounted) return;
      final id = created.summary.text('id');
      if (id.isEmpty) {
        throw const SchoolFailure('Máy chủ chưa trả về bản dự trù.');
      }
      setState(() {
        reviewed = false;
        estimateId = id;
        result = loadEstimate(id);
      });
      workflowNotice(context, 'Đã tạo dự trù.');
    } on SchoolFailure catch (error) {
      if (!mounted) return;
      setState(() {
        reviewed = false;
        if (error.status == 409) rejectedToken = token;
      });
      if (error.status == 409) {
        workflowNotice(
          context,
          'Số suất đã thay đổi. Tải lại và đối chiếu số suất mới.',
        );
      } else {
        workflowError(context, error);
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<SchoolResult> loadEstimate(String id) =>
      widget.controller.execute(SchoolOperation.estimate, context: {'id': id});

  Future<void> copyCsv(String id) async {
    if (busy) return;
    setState(() => busy = true);
    try {
      final response = await widget.controller.execute(
        SchoolOperation.estimateCsv,
        context: {'id': id},
        input: {'format': 'csv'},
      );
      if (!mounted) return;
      await Clipboard.setData(
        ClipboardData(text: response.summary.text('csv')),
      );
      if (mounted) workflowNotice(context, 'Đã sao chép bảng CSV.');
    } catch (error) {
      if (mounted) workflowError(context, error);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !busy,
    child: Scaffold(
      appBar: AppBar(title: const Text('Dự trù nguyên liệu')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            MealSectionTitle(
              "Thực đơn bản ${widget.menu.number('publishedVersion')}",
              subtitle: 'Số lượng dự kiến theo suất đã chốt',
            ),
            FutureBuilder<SchoolResult>(
              future: request,
              builder: (context, snapshot) => WorkflowResultView(
                snapshot: snapshot,
                onRetry: reload,
                builder: (data) => portions(data.summary),
              ),
            ),
            if (busy) const LinearProgressIndicator(),
            if (result != null)
              FutureBuilder<SchoolResult>(
                future: result,
                builder: (context, snapshot) => WorkflowResultView(
                  snapshot: snapshot,
                  onRetry: () => setState(() {
                    result = loadEstimate(estimateId);
                  }),
                  builder: (data) => estimate(data.summary),
                ),
              ),
          ],
        ),
      ),
    ),
  );

  Widget portions(SchoolRecord diff) {
    final classes = diff.records('classDiffs');
    final selected = classes
        .where((item) => item.text('classId') == roomId)
        .firstOrNull;
    final token = diff.text('snapshotToken');
    final valid =
        !busy &&
        classes.isNotEmpty &&
        token.isNotEmpty &&
        token != rejectedToken &&
        (roomId == null || selected != null);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        DropdownButtonFormField<String>(
          key: ValueKey((token, roomId)),
          initialValue: roomId ?? '',
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'Lớp'),
          items: [
            const DropdownMenuItem(value: '', child: Text('Tất cả lớp')),
            for (final item in classes)
              DropdownMenuItem(
                value: item.text('classId'),
                child: Text(
                  item.text('className'),
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: busy
              ? null
              : (value) => setState(() {
                  roomId = value == '' ? null : value;
                  reviewed = false;
                }),
        ),
        ListTile(
          title: Text(
            "${selected?.number('appliedCount') ?? diff.number('appliedTotal')} suất hiện hành",
          ),
        ),
        CheckboxListTile(
          title: const Text('Đã đối chiếu số suất'),
          contentPadding: EdgeInsets.zero,
          value: reviewed,
          onChanged: valid
              ? (value) => setState(() => reviewed = value ?? false)
              : null,
        ),
        MealActions(
          actions: [
            MealAction('Tải lại suất', busy ? null : reload),
            MealAction(
              'Tạo dự trù',
              valid && reviewed ? () => create(token) : null,
              primary: true,
            ),
          ],
        ),
      ],
    );
  }

  Widget estimate(SchoolRecord data) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const SizedBox(height: 16),
      MealSectionTitle(
        'Kết quả dự trù',
        subtitle: schoolTimestamp(data.text('createdAt')),
      ),
      OutlinedButton.icon(
        onPressed: busy ? null : () => copyCsv(data.text('id')),
        icon: const Icon(Icons.copy),
        label: const Text('Sao chép CSV'),
      ),
      if (data.records('lines').isEmpty)
        const MealEmpty('Không có nguyên liệu trong dự trù.'),
      for (final item in data.records('lines'))
        Card(
          child: ExpansionTile(
            title: Text(item.text('ingredientName')),
            subtitle: Text(measured(item['quantity'], item.text('unit'))),
            children: [
              ListTile(
                title: const Text('Phần ăn được'),
                subtitle: Text(measured(item['edibleGrams'], 'g')),
              ),
            ],
          ),
        ),
    ],
  );
}

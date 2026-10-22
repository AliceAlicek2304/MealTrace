import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../domain/kitchen_commands.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_widgets.dart';
import 'kitchen_editor_page.dart';
import '../../../core/presentation/meal_widgets.dart';

class RecipePage extends StatefulWidget {
  const RecipePage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<RecipePage> createState() => _RecipePageState();
}

class _RecipePageState extends State<RecipePage> {
  final id = TextEditingController(),
      version = TextEditingController(text: '1');
  final key = GlobalKey<FormState>();
  late final SchoolController controller;
  SchoolRecord? result;
  String? error;
  bool busy = false;
  DateTime? validAt, knownAt;
  String? loadedId;
  int? loadedVersion;
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
  }

  @override
  void dispose() {
    id.dispose();
    version.dispose();
    controller.dispose();
    super.dispose();
  }

  void clearResult() => setState(() {
    result = null;
    loadedId = null;
    loadedVersion = null;
    error = null;
  });
  Future<void> lookup() async {
    if (busy || !key.currentState!.validate()) return;
    setState(() {
      busy = true;
      error = null;
      result = null;
      loadedId = null;
      loadedVersion = null;
    });
    final requestedId = id.text.trim(),
        requestedVersion = int.parse(version.text.trim());
    try {
      final response = await controller.execute(
        SchoolOperation.nutrition,
        context: {'id': requestedId, 'v': requestedVersion},
        input: {
          if (validAt != null) 'validAt': validAt!.toIso8601String(),
          if (knownAt != null) 'knownAt': knownAt!.toIso8601String(),
        },
      );
      if (mounted) {
        setState(() {
          result = response.summary;
          loadedId = requestedId;
          loadedVersion = requestedVersion;
        });
      }
    } on SchoolFailure catch (failure) {
      if (mounted) setState(() => error = failure.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> edit(bool create) async {
    final saved = await Navigator.push<SchoolRecord>(
      context,
      MaterialPageRoute(
        builder: (_) => KitchenEditorPage(
          operation: create
              ? SchoolOperation.createRecipe
              : SchoolOperation.recipeVersion,
          repository: widget.repository,
          roles: widget.roles,
          onUnauthorized: widget.onUnauthorized,
          record: create ? null : SchoolRecord({'id': loadedId}),
          expectedVersion: create ? null : loadedVersion,
        ),
      ),
    );
    if (!mounted || saved == null) return;
    setState(() {
      id.text = saved.text('recipeId');
      version.text = saved.text('version');
      validAt = null;
      knownAt = null;
    });
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Đã lưu công thức.')));
    await lookup();
  }

  Future<void> chooseTime(bool known) async {
    final now = DateTime.now().toUtc().add(const Duration(hours: 7));
    final date = await showDatePicker(
      context: context,
      initialDate: now,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (!mounted || date == null) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: now.hour, minute: now.minute),
    );
    if (!mounted || time == null) return;
    clearResult();
    setState(() {
      final value = DateTime.utc(
        date.year,
        date.month,
        date.day,
        time.hour,
        time.minute,
      ).subtract(const Duration(hours: 7));
      if (known) {
        knownAt = value;
      } else {
        validAt = value;
      }
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Công thức & dinh dưỡng')),
    body: SafeArea(
      child: Form(
        key: key,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
          children: [
            OutlinedButton.icon(
              onPressed: busy ? null : () => edit(true),
              icon: const Icon(Icons.add),
              label: const Text('Tạo công thức'),
            ),
            const SizedBox(height: 24),
            const MealSectionTitle(
              'Định lượng & dinh dưỡng',
              subtitle: 'Tra cứu công thức và đối chiếu theo phiên bản',
            ),
            TextFormField(
              controller: id,
              enabled: !busy,
              onChanged: (_) => clearResult(),
              decoration: const InputDecoration(labelText: 'Mã công thức'),
              validator: (value) => isRecordId(value?.trim() ?? '')
                  ? null
                  : 'Nhập mã công thức hợp lệ.',
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: version,
              enabled: !busy,
              onChanged: (_) => clearResult(),
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Phiên bản'),
              validator: (value) {
                final n = int.tryParse(value?.trim() ?? '');
                return n != null && n > 0 && n <= 2147483647
                    ? null
                    : 'Nhập phiên bản nguyên dương.';
              },
            ),
            const SizedBox(height: 12),
            ExpansionTile(
              title: const Text('Thời điểm đối chiếu'),
              children: [
                ListTile(
                  title: const Text('Áp dụng tại (UTC+7)'),
                  subtitle: Text(
                    validAt == null
                        ? 'Hiện tại'
                        : displayValue(validAt!.toIso8601String()),
                  ),
                  onTap: busy ? null : () => chooseTime(false),
                ),
                ListTile(
                  title: const Text('Thông tin đã ghi nhận đến (UTC+7)'),
                  subtitle: Text(
                    knownAt == null
                        ? 'Hiện tại'
                        : displayValue(knownAt!.toIso8601String()),
                  ),
                  onTap: busy ? null : () => chooseTime(true),
                ),
                TextButton(
                  onPressed: busy
                      ? null
                      : () {
                          clearResult();
                          setState(() {
                            validAt = null;
                            knownAt = null;
                          });
                        },
                  child: const Text('Dùng thời điểm hiện tại'),
                ),
              ],
            ),
            FilledButton(
              onPressed: busy ? null : lookup,
              child: Text(busy ? 'Đang tra cứu…' : 'Xem dinh dưỡng'),
            ),
            if (error != null) ...[
              const SizedBox(height: 12),
              Text(error!),
              TextButton(
                onPressed: busy ? null : lookup,
                child: const Text('Thử lại'),
              ),
            ],
            if (result case final data?) ...[
              const SizedBox(height: 20),
              MealMetric(
                label: 'Dinh dưỡng mỗi suất',
                value:
                    '${data.record('perServing')?.text('kcal') ?? '—'} kcal · ${data.record('perServing')?.text('proteinG') ?? '—'} g đạm',
                detail: 'Phiên bản $loadedVersion',
                icon: Icons.monitor_heart_outlined,
              ),
              TextButton.icon(
                onPressed: () async {
                  await Clipboard.setData(ClipboardData(text: loadedId!));
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(
                        content: Text('Đã sao chép mã công thức.'),
                      ),
                    );
                  }
                },
                icon: const Icon(Icons.copy),
                label: const Text('Sao chép mã công thức'),
              ),
              OutlinedButton(
                onPressed: busy ? null : () => edit(false),
                child: const Text('Thêm bản công thức'),
              ),
              for (final line in data.records('lines'))
                Card(
                  child: ListTile(
                    title: Text(line.text('ingredientName')),
                    subtitle: Text(
                      '${line.text('gramsPerPortion')} mỗi suất\n${line.record('contribution')?.text('kcal') ?? '—'} kcal · ${line.record('contribution')?.text('proteinG') ?? '—'} g đạm',
                    ),
                  ),
                ),
              ExpansionTile(
                title: const Text('Căn cứ tính toán'),
                children: [
                  ListTile(
                    title: const Text('Thời điểm áp dụng'),
                    subtitle: Text(
                      displayValue(data.record('inputs')?['validAt']),
                    ),
                  ),
                  ListTile(
                    title: const Text('Thông tin ghi nhận đến'),
                    subtitle: Text(
                      displayValue(data.record('inputs')?['knownAt']),
                    ),
                  ),
                  ListTile(
                    title: const Text('Quy tắc làm tròn'),
                    subtitle: Text(
                      'Năng lượng: ${data.record('roundingRule')?.number('kcalDecimals')} chữ số · Đạm: ${data.record('roundingRule')?.number('proteinDecimals')} chữ số',
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    ),
  );
}

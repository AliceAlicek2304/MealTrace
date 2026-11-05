import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import '../../../core/presentation/meal_widgets.dart';
import 'school_widgets.dart';

class KitchenSettlementsPage extends StatefulWidget {
  const KitchenSettlementsPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<KitchenSettlementsPage> createState() => _KitchenSettlementsPageState();
}

class _KitchenSettlementsPageState extends State<KitchenSettlementsPage> {
  late final SchoolController controller;
  String? date = schoolToday();
  int page = 1;
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
    load();
  }

  Future<void> load() => controller.load(
    SchoolOperation.kitchenSettlements,
    {},
    {'date': date, 'page': page, 'pageSize': 20},
  );
  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> open(SchoolRecord day) async {
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => KitchenSettlementDetailPage(
          repository: widget.repository,
          roles: widget.roles,
          onUnauthorized: widget.onUnauthorized,
          day: day,
        ),
      ),
    );
    if (mounted) await load();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) => Scaffold(
      appBar: AppBar(
        title: const Text('Tiếp nhận suất ăn'),
        actions: [
          IconButton(
            tooltip: 'Tải lại',
            onPressed: controller.loading ? null : load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: load,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text(
                canExecute(SchoolOperation.acknowledgeKitchen, widget.roles)
                    ? 'Đối chiếu bản suất hiện hành trước khi xác nhận. Khi có bản mới, bếp cần tiếp nhận lại.'
                    : 'Theo dõi số suất hiện hành, thay đổi theo lớp và lịch sử tiếp nhận của bếp.',
              ),
              Wrap(
                spacing: 8,
                children: [
                  OutlinedButton.icon(
                    icon: const Icon(Icons.calendar_month),
                    label: Text(date ?? 'Tất cả ngày'),
                    onPressed: controller.loading
                        ? null
                        : () async {
                            final chosen = await showDatePicker(
                              context: context,
                              initialDate:
                                  DateTime.tryParse(date ?? '') ??
                                  DateTime.now(),
                              firstDate: DateTime(2000),
                              lastDate: DateTime(2100),
                            );
                            if (chosen != null && mounted) {
                              setState(() {
                                date = chosen.toIso8601String().substring(
                                  0,
                                  10,
                                );
                                page = 1;
                              });
                              await load();
                            }
                          },
                  ),
                  TextButton(
                    onPressed: controller.loading
                        ? null
                        : () {
                            setState(() {
                              date = null;
                              page = 1;
                            });
                            load();
                          },
                    child: const Text('Tất cả ngày'),
                  ),
                ],
              ),
              if (controller.loading) const LinearProgressIndicator(),
              if (controller.error != null)
                Column(
                  children: [
                    Text(controller.error!),
                    TextButton(onPressed: load, child: const Text('Thử lại')),
                  ],
                ),
              if (!controller.loading &&
                  controller.error == null &&
                  controller.result?.records.isEmpty == true)
                const MealEmpty(
                  'Chưa có phiên ăn đã chốt phù hợp.',
                  icon: Icons.restaurant_outlined,
                ),
              for (final day in controller.result?.records ?? <SchoolRecord>[])
                Card(
                  child: InkWell(
                    onTap: () => open(day),
                    child: Padding(
                      padding: const EdgeInsets.all(18),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Row(
                            children: [
                              const MealIcon(Icons.restaurant_outlined),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Text(
                                  '${day.text('sessionType')} · ${day.text('date')}',
                                  style: Theme.of(context).textTheme.titleSmall,
                                ),
                              ),
                              const Icon(
                                Icons.chevron_right_rounded,
                                color: MealColors.muted,
                              ),
                            ],
                          ),
                          const SizedBox(height: 18),
                          Text(
                            '${day.number('appliedPortions')} suất',
                            style: Theme.of(context).textTheme.headlineSmall
                                ?.copyWith(fontWeight: FontWeight.w800),
                          ),
                          Text(
                            'Bản gốc: ${day.number('originalPortions')} suất',
                            style: Theme.of(context).textTheme.bodySmall
                                ?.copyWith(color: MealColors.muted),
                          ),
                          const SizedBox(height: 14),
                          MealStatus(
                            day.flag('isAcknowledged')
                                ? 'Đã tiếp nhận bản hiện hành'
                                : 'Cần xác nhận',
                            positive: day.flag('isAcknowledged'),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              if (controller.result != null)
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    IconButton(
                      tooltip: 'Trang trước',
                      onPressed: controller.loading || page <= 1
                          ? null
                          : () {
                              page--;
                              load();
                            },
                      icon: const Icon(Icons.chevron_left),
                    ),
                    Flexible(
                      child: Text(
                        'Trang $page · ${controller.result!.total} phiên',
                      ),
                    ),
                    IconButton(
                      tooltip: 'Trang sau',
                      onPressed:
                          controller.loading ||
                              page * 20 >= controller.result!.total
                          ? null
                          : () {
                              page++;
                              load();
                            },
                      icon: const Icon(Icons.chevron_right),
                    ),
                  ],
                ),
            ],
          ),
        ),
      ),
    ),
  );
}

class KitchenSettlementDetailPage extends StatefulWidget {
  const KitchenSettlementDetailPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
    required this.day,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  final SchoolRecord day;
  @override
  State<KitchenSettlementDetailPage> createState() =>
      _KitchenSettlementDetailPageState();
}

class _KitchenSettlementDetailPageState
    extends State<KitchenSettlementDetailPage> {
  late final SchoolController controller;
  final note = TextEditingController();
  SchoolRecord? detail;
  List<SchoolRecord> history = [];
  bool loading = true, saving = false, reviewed = false;
  String? error;
  Map<String, Object?> get selected => {'id': widget.day.text('mealDayId')};
  bool get canAcknowledge =>
      canExecute(SchoolOperation.acknowledgeKitchen, widget.roles);
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
    load();
  }

  @override
  void dispose() {
    note.dispose();
    controller.dispose();
    super.dispose();
  }

  Future<void> load() async {
    setState(() {
      loading = true;
      reviewed = false;
      detail = null;
      error = null;
      history = [];
    });
    try {
      final responses = await Future.wait([
        controller.execute(SchoolOperation.kitchenDiff, context: selected),
        controller.execute(SchoolOperation.kitchenHistory, context: selected),
      ]);
      if (mounted) {
        setState(() {
          detail = responses[0].summary;
          history = responses[1].records;
        });
      }
    } on SchoolFailure catch (failure) {
      if (mounted) setState(() => error = failure.message);
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> acknowledge() async {
    if (!canAcknowledge ||
        saving ||
        loading ||
        !reviewed ||
        detail == null ||
        detail!.text('snapshotToken').isEmpty ||
        detail!.flag('isAcknowledged')) {
      return;
    }
    FocusScope.of(context).unfocus();
    final token = detail!.text('snapshotToken');
    setState(() => saving = true);
    try {
      await controller.execute(
        SchoolOperation.acknowledgeKitchen,
        context: selected,
        input: {
          'snapshotToken': token,
          'note': note.text.trim().isEmpty ? null : note.text.trim(),
        },
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Đã xác nhận tiếp nhận bản suất hiện hành.'),
        ),
      );
      note.clear();
      await load();
    } on SchoolFailure catch (failure) {
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(failure.message)));
      if (failure.status == 409) await load();
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !saving,
    child: Scaffold(
      bottomNavigationBar:
          canAcknowledge &&
              detail != null &&
              !detail!.flag('isAcknowledged') &&
              !loading
          ? AnimatedPadding(
              duration: const Duration(milliseconds: 150),
              padding: EdgeInsets.only(
                bottom: MediaQuery.viewInsetsOf(context).bottom,
              ),
              child: SafeArea(
                top: false,
                minimum: const EdgeInsets.all(16),
                child: FilledButton(
                  onPressed: reviewed && !saving ? acknowledge : null,
                  child: Text(saving ? 'Đang xác nhận…' : 'Xác nhận tiếp nhận'),
                ),
              ),
            )
          : null,
      appBar: AppBar(
        title: Text(
          '${widget.day.text('sessionType')} · ${widget.day.text('date')}',
        ),
        actions: [
          IconButton(
            tooltip: 'Tải lại bản suất',
            onPressed: loading || saving ? null : load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (loading) const LinearProgressIndicator(),
            if (error != null)
              Column(
                children: [
                  Text(error!),
                  TextButton(onPressed: load, child: const Text('Thử lại')),
                ],
              ),
            if (detail != null) ...[
              MealMetric(
                label: 'Số suất cần chuẩn bị',
                value: '${detail!.number('appliedTotal')} suất',
                detail:
                    'Gốc ${detail!.number('originalTotal')} · Chênh lệch ${detail!.number('totalDiff')}',
              ),
              for (final room in detail!.records('classDiffs'))
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          '${room.text('className')} · Bản ${room.number('currentVersion')}',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        Text(
                          '${room.number('originalCount')} → ${room.number('appliedCount')} suất · Điều chỉnh tổng: ${room.number('kitchenAdjustment')}',
                        ),
                        if (!room.flag('hasCompleteRoster'))
                          const Text('Chưa đủ dữ liệu lịch sử để đối chiếu trẻ')
                        else
                          ExpansionTile(
                            title: Text(
                              '+${room.records('addedStudents').length} / −${room.records('removedStudents').length} trẻ',
                            ),
                            children: [
                              Text(
                                'Thêm: ${names(room.records('addedStudents'))}',
                              ),
                              Text(
                                'Bớt: ${names(room.records('removedStudents'))}',
                              ),
                            ],
                          ),
                      ],
                    ),
                  ),
                ),
              if (detail!.flag('isAcknowledged'))
                const Text('Bếp đã tiếp nhận bản suất hiện hành.')
              else if (canAcknowledge) ...[
                CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text(
                    'Tôi đã đối chiếu số suất và thay đổi của bản đang hiển thị',
                  ),
                  value: reviewed,
                  onChanged: saving
                      ? null
                      : (value) => setState(() => reviewed = value == true),
                ),
                TextField(
                  controller: note,
                  enabled: !saving,
                  maxLength: 500,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    labelText: 'Ghi chú tiếp nhận',
                  ),
                ),
              ] else
                const Text('Bếp chưa tiếp nhận bản suất hiện hành.'),
              ExpansionTile(
                title: Text('Lịch sử tiếp nhận · ${history.length}'),
                children: [
                  if (history.isEmpty) const Text('Chưa có lịch sử tiếp nhận.'),
                  for (final entry in history)
                    ListTile(
                      title: Text(
                        '${entry.number('portionsCount')} suất · ${entry.text('acknowledgedByName')}',
                      ),
                      subtitle: Text(
                        '${displayValue(entry['acknowledgedAt'])}\n${entry.text('snapshotToken') == detail!.text('snapshotToken') ? 'Bản hiện hành' : 'Bản trước hoặc chưa có thông tin phiên bản'}\n${entry.text('note')}',
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
  String names(List<SchoolRecord> children) => children.isEmpty
      ? 'Không có'
      : children.map((child) => child.text('studentName')).join(', ');
}

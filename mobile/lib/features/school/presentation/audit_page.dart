import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_widgets.dart' show displayValue;

class AuditPage extends StatefulWidget {
  const AuditPage({super.key, required this.controller});
  final SchoolController controller;
  @override
  State<AuditPage> createState() => _AuditPageState();
}

class _AuditPageState extends State<AuditPage> {
  final type = TextEditingController(),
      id = TextEditingController(),
      action = TextEditingController();
  SchoolResult? result;
  bool busy = false;
  String? error;
  int page = 1;
  Map<String, Object?> get filters => {
    'entityType': type.text.trim(),
    'entityId': id.text.trim(),
    'action': action.text.trim(),
    'page': page,
    'pageSize': 25,
  };
  @override
  void initState() {
    super.initState();
    load();
  }

  @override
  void dispose() {
    type.dispose();
    id.dispose();
    action.dispose();
    super.dispose();
  }

  Future<void> load() async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final data = await widget.controller.execute(
        type.text.trim().isNotEmpty && id.text.trim().isNotEmpty
            ? SchoolOperation.auditEntity
            : SchoolOperation.auditLogs,
        context: filters,
      );
      if (mounted) setState(() => result = data);
    } on SchoolFailure catch (e) {
      if (mounted) setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> open(SchoolRecord record) async {
    if (busy) return;
    setState(() => busy = true);
    try {
      final data = await widget.controller.execute(
        SchoolOperation.auditDetail,
        context: {'id': record.text('id')},
      );
      if (!mounted) return;
      setState(() => busy = false);
      await showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (context) => SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Chi tiết xử lý',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                _AuditValues(value: data.summary),
              ],
            ),
          ),
        ),
      );
    } on SchoolFailure catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> export() async {
    setState(() => busy = true);
    try {
      final data = await widget.controller.execute(
        SchoolOperation.auditExport,
        context: {...filters, 'format': 'csv'},
      );
      await Clipboard.setData(ClipboardData(text: data.summary.text('csv')));
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('Đã sao chép bảng CSV.')));
      }
    } on SchoolFailure catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Lịch sử xử lý')),
    body: ListView(
      padding: const EdgeInsets.all(16),
      children: [
        ExpansionTile(
          title: const Text('Tìm kiếm & bộ lọc'),
          children: [
            TextField(
              controller: type,
              decoration: const InputDecoration(labelText: 'Loại hồ sơ'),
            ),
            TextField(
              controller: id,
              decoration: const InputDecoration(labelText: 'Mã hồ sơ'),
            ),
            TextField(
              controller: action,
              decoration: const InputDecoration(labelText: 'Thao tác'),
            ),
            FilledButton(
              onPressed: busy
                  ? null
                  : () {
                      page = 1;
                      load();
                    },
              child: const Text('Áp dụng'),
            ),
          ],
        ),
        OutlinedButton(
          onPressed: busy ? null : export,
          child: const Text('Sao chép CSV'),
        ),
        if (busy) const LinearProgressIndicator(),
        if (error != null) Text(error!),
        if (error != null)
          TextButton(
            onPressed: busy ? null : load,
            child: const Text('Thử lại'),
          ),
        if (!busy && error == null && result?.records.isEmpty == true)
          const Padding(
            padding: EdgeInsets.all(20),
            child: Text('Không có lịch sử phù hợp.'),
          ),
        for (final row in result?.records ?? <SchoolRecord>[])
          Card(
            child: ListTile(
              title: Text(row.text('action')),
              subtitle: Text(
                '${row.text('entityType')} · ${displayValue(row.text('occurredAt'))}',
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: busy ? null : () => open(row),
            ),
          ),
        Row(
          children: [
            Expanded(
              child: TextButton(
                onPressed: busy || page == 1
                    ? null
                    : () {
                        page--;
                        load();
                      },
                child: const Text('Trước'),
              ),
            ),
            Text('Trang $page'),
            Expanded(
              child: TextButton(
                onPressed: busy || page * 25 >= (result?.total ?? 0)
                    ? null
                    : () {
                        page++;
                        load();
                      },
                child: const Text('Sau'),
              ),
            ),
          ],
        ),
      ],
    ),
  );
}

class _AuditValues extends StatelessWidget {
  const _AuditValues({required this.value, this.depth = 0});
  final Object? value;
  final int depth;
  @override
  Widget build(BuildContext context) {
    if (depth > 5) return const Text('Xem thêm trong hồ sơ nguồn.');
    if (value is SchoolRecord) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final entry in (value as SchoolRecord).fields.entries)
            ExpansionTile(
              title: Text(
                const {
                      'before': 'Trước thay đổi',
                      'after': 'Sau thay đổi',
                      'actor': 'Người thực hiện',
                      'reason': 'Lý do',
                      'action': 'Thao tác',
                      'entityType': 'Loại hồ sơ',
                      'entityId': 'Mã hồ sơ',
                      'occurredAt': 'Thời điểm',
                    }[entry.key] ??
                    entry.key,
              ),
              children: [
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: _AuditValues(value: entry.value, depth: depth + 1),
                ),
              ],
            ),
        ],
      );
    }
    if (value is List) {
      return Column(
        children: [
          for (final item in value as List)
            _AuditValues(value: item, depth: depth + 1),
        ],
      );
    }
    return SelectableText(displayValue(value));
  }
}

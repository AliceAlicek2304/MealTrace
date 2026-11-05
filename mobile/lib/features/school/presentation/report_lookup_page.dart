import 'package:flutter/material.dart';
import '../domain/kitchen_commands.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_widgets.dart';

class ReportLookupPage extends StatefulWidget {
  const ReportLookupPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<ReportLookupPage> createState() => _ReportLookupPageState();
}

class _ReportLookupPageState extends State<ReportLookupPage> {
  final id = TextEditingController();
  final form = GlobalKey<FormState>();
  late final SchoolController controller;
  String? requestedId;
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
    controller.dispose();
    super.dispose();
  }

  Future<void> load() async {
    if (!controller.loading && form.currentState!.validate()) {
      requestedId = id.text.trim();
      await controller.load(SchoolOperation.reportLineage, {
        'id': id.text.trim(),
      }, const {});
    }
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final result = requestedId == id.text.trim()
          ? controller.result?.summary
          : null;
      return Scaffold(
        appBar: AppBar(title: const Text('Truy vết báo cáo')),
        body: SafeArea(
          child: Form(
            key: form,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                TextFormField(
                  controller: id,
                  onChanged: (_) => setState(() {}),
                  enabled: !controller.loading,
                  decoration: const InputDecoration(labelText: 'Mã báo cáo'),
                  validator: (value) => isRecordId(value?.trim() ?? '')
                      ? null
                      : 'Nhập mã báo cáo hợp lệ.',
                ),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: controller.loading ? null : load,
                  child: Text(
                    controller.loading ? 'Đang tra cứu…' : 'Xem nguồn báo cáo',
                  ),
                ),
                if (controller.error != null) ...[
                  const SizedBox(height: 16),
                  Text(controller.error!),
                  TextButton(onPressed: load, child: const Text('Thử lại')),
                ],
                if (result != null) ...[
                  const SizedBox(height: 20),
                  RecordDetails(record: result),
                  ListTile(
                    title: const Text('Mã báo cáo'),
                    subtitle: SelectableText(result.text('id')),
                  ),
                  ListTile(
                    title: const Text('Phiên ăn'),
                    subtitle: SelectableText(result.text('mealDayId')),
                  ),
                  ListTile(
                    title: const Text('Bản chốt làm căn cứ'),
                    subtitle: SelectableText(result.text('settlementId')),
                  ),
                  ExpansionTile(
                    title: const Text('Nguồn dữ liệu đã lưu'),
                    children: [ReportSource(source: result['source'])],
                  ),
                ],
              ],
            ),
          ),
        ),
      );
    },
  );
}

class ReportSource extends StatelessWidget {
  const ReportSource({super.key, required this.source});
  final Object? source;
  @override
  Widget build(BuildContext context) {
    if (source == null) {
      return const Padding(
        padding: EdgeInsets.all(16),
        child: Text('Chưa đọc được dữ liệu nguồn của báo cáo.'),
      );
    }
    var nodes = 0;
    Widget node(Object? value, int depth) {
      if (++nodes > 200) return const Text('Đã giới hạn số mục hiển thị.');
      if (depth > 6) return const Text('Dữ liệu lồng nhiều cấp.');
      if (value is SchoolRecord) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final entry in value.fields.entries.take(100))
              ExpansionTile(
                title: Text(fieldLabels[entry.key] ?? entry.key),
                childrenPadding: const EdgeInsets.all(12),
                children: [node(entry.value, depth + 1)],
              ),
          ],
        );
      }
      if (value is List) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (var i = 0; i < value.length && i < 100; i++)
              ExpansionTile(
                title: Text('Mục ${i + 1}'),
                childrenPadding: const EdgeInsets.all(12),
                children: [node(value[i], depth + 1)],
              ),
            if (value.length > 100) Text('Hiển thị 100 / ${value.length} mục.'),
          ],
        );
      }
      final display = displayValue(value);
      return Padding(
        padding: const EdgeInsets.all(12),
        child: SelectableText(
          display.length > 5000 ? '${display.substring(0, 5000)}…' : display,
        ),
      );
    }

    return node(source, 0);
  }
}

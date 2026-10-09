import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_widgets.dart';

class StudentImportHistoryPage extends StatefulWidget {
  const StudentImportHistoryPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<StudentImportHistoryPage> createState() =>
      _StudentImportHistoryPageState();
}

class _StudentImportHistoryPageState extends State<StudentImportHistoryPage> {
  late final SchoolController controller;
  List<SchoolRecord> classes = [];
  SchoolResult? history;
  String? classId, error;
  int page = 1;
  bool busy = true;
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
    load(loadClasses: true);
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> load({bool loadClasses = false}) async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      if (loadClasses) {
        classes = (await controller.execute(
          SchoolOperation.assignedClasses,
        )).records;
      }
      final result = await controller.execute(
        SchoolOperation.studentImportHistory,
        input: {'page': page, if (classId != null) 'classId': classId},
      );
      if (mounted) setState(() => history = result);
    } on SchoolFailure catch (failure) {
      if (mounted) setState(() => error = failure.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> detail(SchoolRecord batch) async {
    setState(() => busy = true);
    try {
      final result = await controller.execute(
        SchoolOperation.studentImportBatch,
        context: {'id': batch.text('id')},
      );
      if (!mounted) return;
      setState(() => busy = false);
      await showModalBottomSheet<void>(
        context: context,
        showDragHandle: true,
        isScrollControlled: true,
        builder: (context) => SafeArea(
          child: SizedBox(
            height: MediaQuery.sizeOf(context).height * .8,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  '${batch.text('className')} · ${batch.number('created')} trẻ',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                ExpansionTile(
                  title: const Text('Thông tin lần nhập'),
                  children: [RecordDetails(record: batch)],
                ),
                for (final row in result.summary.records('rows'))
                  Card(
                    child: ListTile(
                      title: Text(row.text('fullName')),
                      subtitle: Text('Dòng ${row.number('row')}'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => showDialog<void>(
                        context: context,
                        builder: (context) => AlertDialog(
                          title: const Text('Thông tin trẻ lúc nhập'),
                          content: SingleChildScrollView(
                            child: RecordDetails(record: row),
                          ),
                          actions: [
                            TextButton(
                              onPressed: () => Navigator.pop(context),
                              child: const Text('Đóng'),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      );
    } on SchoolFailure catch (failure) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(failure.message)));
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Lịch sử nhập trẻ')),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          ExpansionTile(
            title: const Text('Bộ lọc'),
            subtitle: classId == null ? null : const Text('Đang lọc theo lớp'),
            children: [
              DropdownButtonFormField<String>(
                isExpanded: true,
                initialValue: classId ?? '',
                decoration: const InputDecoration(labelText: 'Lớp'),
                items: [
                  const DropdownMenuItem(value: '', child: Text('Tất cả lớp')),
                  ...classes.map(
                    (room) => DropdownMenuItem(
                      value: room.text('id'),
                      child: Text(
                        '${room.text('name')} · ${room.text('schoolYear')}',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ),
                ],
                onChanged: busy
                    ? null
                    : (value) {
                        classId = value == '' ? null : value;
                        page = 1;
                        load();
                      },
              ),
            ],
          ),
          if (busy) const LinearProgressIndicator(),
          if (error != null) ...[
            Text(
              error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
            TextButton(
              onPressed: busy ? null : () => load(loadClasses: classes.isEmpty),
              child: const Text('Thử lại'),
            ),
          ] else if (!busy && history?.records.isEmpty == true)
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Chưa có lịch sử nhập phù hợp.'),
            ),
          for (final batch in history?.records ?? <SchoolRecord>[])
            Card(
              child: ListTile(
                title: Text(
                  '${batch.text('className')} · ${batch.number('created')} trẻ',
                ),
                subtitle: Text(
                  '${batch.text('fileName')}\n${displayValue(batch['importedAt'])} · ${batch.text('importedByName')}',
                ),
                isThreeLine: true,
                trailing: const Icon(Icons.chevron_right),
                onTap: busy ? null : () => detail(batch),
              ),
            ),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              TextButton(
                onPressed: busy || page == 1
                    ? null
                    : () {
                        page--;
                        load();
                      },
                child: const Text('Trước'),
              ),
              Text('Trang $page'),
              TextButton(
                onPressed:
                    busy || page * 20 >= (history?.summary.number('total') ?? 0)
                    ? null
                    : () {
                        page++;
                        load();
                      },
                child: const Text('Sau'),
              ),
            ],
          ),
        ],
      ),
    ),
  );
}

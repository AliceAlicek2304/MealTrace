import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_forms.dart';
import 'school_form_page.dart';
import 'school_page.dart';
import 'school_widgets.dart';

class ParentLinkReviewPage extends StatefulWidget {
  const ParentLinkReviewPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<ParentLinkReviewPage> createState() => _ParentLinkReviewPageState();
}

class _ParentLinkReviewPageState extends State<ParentLinkReviewPage> {
  late final SchoolController controller;
  List<SchoolRecord> classes = [];
  SchoolResult? result;
  String? year, classId, error;
  final selected = <String, int>{};
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
    loadClasses();
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> loadClasses() async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final response = await controller.execute(
        SchoolOperation.reviewLinkClasses,
      );
      if (!mounted) return;
      setState(() {
        classes = response.records;
        busy = false;
      });
    } on SchoolFailure catch (failure) {
      if (mounted) {
        setState(() {
          error = failure.message;
          busy = false;
        });
      }
    }
  }

  Future<void> load() async {
    setState(() {
      busy = true;
      error = null;
      selected.clear();
      result = null;
    });
    try {
      final response = await controller.execute(
        SchoolOperation.reviewableParentLinks,
        input: {
          'schoolYear': year,
          'classId': classId,
          'status': 'PENDING',
          'page': page,
          'pageSize': 100,
        },
      );
      if (!mounted) return;
      setState(() {
        result = response;
        busy = false;
      });
    } on SchoolFailure catch (failure) {
      if (mounted) {
        setState(() {
          error = failure.message;
          busy = false;
        });
      }
    }
  }

  Future<void> review() async {
    if (busy || (classId == null && year == null) || selected.isEmpty) return;
    final records = result!.records
        .where((x) => selected.containsKey(x.text('id')))
        .toList();
    setState(() => busy = true);
    await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => SchoolFormPage(
          form: schoolForms[SchoolOperation.bulkReviewParentLinks]!,
          controller: controller,
          contextValues: {
            'classId': classId,
            'schoolYear': year,
            'className': classes
                .where((x) => x.text('id') == classId)
                .firstOrNull
                ?.text('name'),
            'requestCount': selected.length,
            'selectedRequests': records
                .map(
                  (x) =>
                      '${x.text('studentName')} — ${x.text('parentName')} — ${x.text('parentPhone')}',
                )
                .toList(),
            'items': selected.entries
                .map((x) => {'id': x.key, 'revision': x.value})
                .toList(),
          },
        ),
      ),
    );
    if (mounted) await load();
  }

  Future<void> history() async {
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => SchoolPage(
          operation: SchoolOperation.reviewableParentLinks,
          title: 'Lịch sử / sửa liên kết',
          repository: widget.repository,
          roles: widget.roles,
          onUnauthorized: widget.onUnauthorized,
          contextValues: {'classId': classId, 'schoolYear': year},
        ),
      ),
    );
    if (mounted) await load();
  }

  @override
  Widget build(BuildContext context) {
    final years = classes.map((x) => x.text('schoolYear')).toSet().toList()
      ..sort((a, b) => b.compareTo(a));
    final rooms = classes
        .where((x) => year == null || x.text('schoolYear') == year)
        .toList();
    return Scaffold(
      appBar: AppBar(title: const Text('Duyệt liên kết theo lớp')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            const Text(
              'Lọc năm học và lớp, rà tên trẻ, phụ huynh và SĐT. Bỏ chọn trường hợp chưa rõ trước khi xác nhận. Mỗi lượt tối đa 100 yêu cầu đã chọn.',
            ),
            const SizedBox(height: 16),
            DropdownButtonFormField<String>(
              key: ValueKey('year:$year'),
              initialValue: year,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Năm học'),
              items: [
                const DropdownMenuItem(
                  value: '',
                  child: Text('Tất cả năm học'),
                ),
                ...years.map((x) => DropdownMenuItem(value: x, child: Text(x))),
              ],
              onChanged: busy
                  ? null
                  : (value) {
                      setState(() {
                        year = value == '' ? null : value;
                        classId = null;
                        page = 1;
                      });
                      load();
                    },
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              key: ValueKey('class:$classId:$year'),
              initialValue: classId,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Lớp phụ trách'),
              items: [
                const DropdownMenuItem(
                  value: '',
                  child: Text('Tất cả lớp được phân công'),
                ),
                ...rooms.map(
                  (x) => DropdownMenuItem(
                    value: x.text('id'),
                    child: Text('${x.text('name')} · ${x.text('schoolYear')}'),
                  ),
                ),
              ],
              onChanged: busy
                  ? null
                  : (value) {
                      setState(() {
                        classId = value == '' ? null : value;
                        page = 1;
                      });
                      load();
                    },
            ),
            Wrap(
              spacing: 8,
              children: [
                TextButton(
                  onPressed: busy ? null : load,
                  child: const Text('Tải lại danh sách'),
                ),
                TextButton(
                  onPressed: busy ? null : history,
                  child: const Text('Lịch sử / sửa liên kết'),
                ),
                TextButton(
                  onPressed: busy || (classId == null && year == null)
                      ? null
                      : () => setState(() {
                          selected.clear();
                          for (final row
                              in result?.records ?? <SchoolRecord>[]) {
                            if (row.text('status') == 'PENDING') {
                              selected[row.text('id')] = row.number('revision');
                            }
                          }
                        }),
                  child: const Text('Chọn yêu cầu trên trang'),
                ),
                TextButton(
                  onPressed: busy ? null : () => setState(selected.clear),
                  child: const Text('Bỏ chọn'),
                ),
              ],
            ),
            FilledButton(
              onPressed:
                  busy || (classId == null && year == null) || selected.isEmpty
                  ? null
                  : review,
              child: Text('Xử lý ${selected.length} yêu cầu đã chọn'),
            ),
            if (busy) const LinearProgressIndicator(),
            if (error != null)
              Column(
                children: [
                  Text(error!),
                  TextButton(
                    onPressed: busy
                        ? null
                        : () {
                            if (classes.isEmpty) {
                              loadClasses();
                            } else {
                              load();
                            }
                          },
                    child: const Text('Thử lại'),
                  ),
                ],
              ),
            if (!busy && result?.records.isEmpty == true)
              const Padding(
                padding: EdgeInsets.all(24),
                child: Text('Chưa có yêu cầu chờ duyệt.'),
              ),
            for (final row in result?.records ?? <SchoolRecord>[])
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      CheckboxListTile(
                        contentPadding: EdgeInsets.zero,
                        title: Text(row.text('studentName')),
                        subtitle: Text(
                          '${row.text('parentName')} · ${row.text('parentPhone')}',
                        ),
                        value: selected.containsKey(row.text('id')),
                        onChanged: busy || (classId == null && year == null)
                            ? null
                            : (checked) => setState(() {
                                if (checked == true) {
                                  selected[row.text('id')] = row.number(
                                    'revision',
                                  );
                                } else {
                                  selected.remove(row.text('id'));
                                }
                              }),
                      ),
                      RecordDetails(record: row, compact: true),
                    ],
                  ),
                ),
              ),
            if (result != null)
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  IconButton(
                    tooltip: 'Trang trước',
                    onPressed: busy || page <= 1
                        ? null
                        : () {
                            page--;
                            load();
                          },
                    icon: const Icon(Icons.chevron_left),
                  ),
                  Flexible(
                    child: Text('Trang $page · ${result!.total} yêu cầu'),
                  ),
                  IconButton(
                    tooltip: 'Trang sau',
                    onPressed: busy || page * 100 >= result!.total
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
    );
  }
}

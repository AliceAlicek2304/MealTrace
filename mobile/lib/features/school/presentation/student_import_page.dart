import 'dart:typed_data';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'student_import_history_page.dart';

class StudentImportPage extends StatefulWidget {
  const StudentImportPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
    this.pickFile,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  final Future<XFile?> Function()? pickFile;
  @override
  State<StudentImportPage> createState() => _StudentImportPageState();
}

class _StudentImportPageState extends State<StudentImportPage> {
  late final SchoolController controller;
  List<SchoolRecord> classes = [];
  SchoolResult? preview;
  String? classId, error, filename;
  String startDate = schoolToday();
  Uint8List? bytes;
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
    try {
      if (!widget.roles.contains('ADMIN')) {
        throw const SchoolFailure('Chỉ Admin được nhập trẻ từ Excel.');
      }
      final result = await controller.execute(SchoolOperation.assignedClasses);
      if (mounted) {
        setState(() {
          classes = result.records;
          busy = false;
        });
      }
    } on SchoolFailure catch (failure) {
      if (mounted) {
        setState(() {
          error = failure.message;
          busy = false;
        });
      }
    }
  }

  void reset() {
    preview = null;
    error = null;
  }

  void showChildDetail(SchoolRecord row) {
    showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Chi tiết trẻ nhập từ Excel'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                row.text('fullName').isEmpty
                    ? '(Thiếu họ tên)'
                    : row.text('fullName'),
              ),
              const SizedBox(height: 12),
              Text('Dòng trong file: ${row.number('row')}'),
              Text(
                'Ngày sinh: ${row.text('dateOfBirth').isEmpty ? 'Chưa có' : row.text('dateOfBirth').split('-').reversed.join('/')}',
              ),
              Text(
                'Giới tính: ${{'MALE': 'Nam', 'FEMALE': 'Nữ', 'OTHER': 'Khác'}[row.text('gender')] ?? 'Chưa có'}',
              ),
              Text(
                'SĐT phụ huynh: ${row.text('parentPhoneNumber').isEmpty ? 'Chưa có' : row.text('parentPhoneNumber')}',
              ),
              Text(
                'Kiểm tra: ${row.text('error').isEmpty ? 'Hợp lệ' : row.text('error')}',
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Quay lại danh sách'),
          ),
        ],
      ),
    );
  }

  Future<void> chooseFile() async {
    setState(() {
      busy = true;
      reset();
      bytes = null;
      filename = null;
    });
    try {
      final file =
          await (widget.pickFile?.call() ??
              openFile(
                acceptedTypeGroups: const [
                  XTypeGroup(label: 'Excel', extensions: ['xlsx']),
                ],
              ));
      if (file == null) return;
      if (!file.name.toLowerCase().endsWith('.xlsx') ||
          await file.length() > 5 * 1024 * 1024) {
        throw const SchoolFailure('Chọn file XLSX tối đa 5 MB.');
      }
      final data = await file.readAsBytes();
      if (data.isEmpty) throw const SchoolFailure('File đang trống.');
      if (mounted) {
        setState(() {
          bytes = data;
          filename = file.name;
        });
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Không đọc được file. Chọn XLSX không rỗng, tối đa 5 MB.',
            ),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> send(bool confirm) async {
    if (busy ||
        bytes == null ||
        classId == null ||
        !widget.roles.contains('ADMIN')) {
      return;
    }
    if (confirm) {
      if (preview?.summary.flag('canImport') != true) return;
      final accepted = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Xác nhận nhập trẻ'),
          content: Text(
            'Nhập ${preview!.records.length} trẻ vào lớp đã chọn, từ ngày $startDate? Lưu họ tên, ngày sinh, giới tính và ghi danh, không gửi tin.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Hủy'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Xác nhận'),
            ),
          ],
        ),
      );
      if (accepted != true || !mounted) return;
    }
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final result = await controller.execute(
        confirm
            ? SchoolOperation.confirmStudentImport
            : SchoolOperation.previewStudentImport,
        input: {
          'fileBytes': bytes!,
          'fileName': filename!,
          'classId': classId!,
          'startDate': startDate,
        },
      );
      if (!mounted) return;
      if (confirm) {
        setState(() {
          preview = null;
          bytes = null;
          filename = null;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Đã nhập ${result.summary.number('created')} trẻ.'),
          ),
        );
      } else {
        setState(() => preview = result);
      }
    } on SchoolFailure catch (failure) {
      if (mounted) {
        setState(() {
          preview = null;
        });
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(failure.message)));
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !busy,
    child: Scaffold(
      appBar: AppBar(
        title: const Text('Nhập trẻ từ Excel'),
        actions: [
          IconButton(
            tooltip: 'Lịch sử nhập trẻ',
            icon: const Icon(Icons.history),
            onPressed: busy
                ? null
                : () => Navigator.push<void>(
                    context,
                    MaterialPageRoute(
                      builder: (_) => StudentImportHistoryPage(
                        repository: widget.repository,
                        roles: widget.roles,
                        onUnauthorized: widget.onUnauthorized,
                      ),
                    ),
                  ),
          ),
        ],
      ),
      bottomNavigationBar: preview == null
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: busy ? null : () => setState(reset),
                        child: const Text('Chọn lại file'),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: FilledButton(
                        onPressed: busy || !preview!.summary.flag('canImport')
                            ? null
                            : () => send(true),
                        child: Text('Nhập ${preview!.records.length} trẻ'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (preview == null) ...[
              const Text(
                'Nhập họ tên, ngày sinh và giới tính có trong file; tự tạo mã trẻ. Admin chọn lớp và ngày bắt đầu. Không tạo phụ huynh hoặc gửi WhatsApp. Một sheet, tối đa 500 trẻ, 5 MB.',
              ),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                isExpanded: true,
                initialValue: classId,
                decoration: const InputDecoration(labelText: 'Lớp nhận trẻ'),
                items: classes
                    .map(
                      (room) => DropdownMenuItem(
                        value: room.text('id'),
                        child: Text(
                          '${room.text('name')} · ${room.text('schoolYear')}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                onChanged: busy
                    ? null
                    : (value) => setState(() {
                        classId = value;
                        reset();
                      }),
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: busy
                    ? null
                    : () async {
                        final today = DateTime.parse(schoolToday());
                        final date = await showDatePicker(
                          context: context,
                          initialDate: DateTime.parse(startDate),
                          firstDate: today,
                          lastDate: DateTime(2100),
                        );
                        if (date != null && mounted) {
                          setState(() {
                            startDate = date.toIso8601String().substring(0, 10);
                            reset();
                          });
                        }
                      },
                icon: const Icon(Icons.event),
                label: Text('Ngày bắt đầu: $startDate'),
              ),
              OutlinedButton.icon(
                onPressed: busy || !widget.roles.contains('ADMIN')
                    ? null
                    : chooseFile,
                icon: const Icon(Icons.upload_file),
                label: const Text('Chọn file XLSX'),
              ),
              if (filename != null) Text(filename!, softWrap: true),
              FilledButton(
                onPressed: busy || bytes == null || classId == null
                    ? null
                    : () => send(false),
                child: const Text('Xem trước danh sách'),
              ),
              if (busy) const LinearProgressIndicator(),
              if (error != null)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 12),
                  child: Text(
                    error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
            ],
            if (preview != null) ...[
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Text(
                  '${preview!.records.length} trẻ · ${preview!.summary.text('sheetName')}',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
              Text('Ngày bắt đầu: ${startDate.split('-').reversed.join('/')}'),
              ExpansionTile(
                title: const Text('Thông tin bổ sung'),
                children: [
                  Text(
                    'Các cột không nhập: ${preview!.summary.values('ignoredColumns').join(', ')}.',
                  ),
                ],
              ),
              if (busy) const LinearProgressIndicator(),
              if (!preview!.summary.flag('canImport'))
                const Text(
                  'Có dòng lỗi hoặc trùng. Sửa file rồi xem trước lại; chưa lưu trẻ nào.',
                ),
              for (final row in preview!.records)
                Card(
                  child: InkWell(
                    onTap: busy ? null : () => showChildDetail(row),
                    borderRadius: BorderRadius.circular(12),
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            row.text('fullName').isEmpty
                                ? '(Thiếu họ tên)'
                                : row.text('fullName'),
                          ),
                          Text(
                            'SĐT phụ huynh: ${row.text('parentPhoneNumber').isEmpty ? 'Chưa có' : row.text('parentPhoneNumber')}',
                          ),
                          Text(
                            row.text('error').isEmpty ? 'Hợp lệ' : 'Có lỗi',
                            style: TextStyle(
                              color: row.text('error').isEmpty
                                  ? Theme.of(context).colorScheme.primary
                                  : Theme.of(context).colorScheme.error,
                            ),
                          ),
                          const Text('Xem chi tiết ›'),
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          ],
        ),
      ),
    ),
  );
}

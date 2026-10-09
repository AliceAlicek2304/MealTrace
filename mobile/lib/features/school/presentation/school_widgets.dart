import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_forms.dart';

const fieldLabels = <String, String>{
  'requestCount': 'Số yêu cầu',
  'selectedRequests': 'Yêu cầu đã chọn',
  'revocationReason': 'Lý do thu hồi',
  'revokedAt': 'Ngày thu hồi',
  'parentName': 'Phụ huynh',
  'parentPhone': 'SĐT phụ huynh',
  'relationship': 'Quan hệ',
  'note': 'Ghi chú',
  'fullName': 'Họ tên',
  'studentName': 'Trẻ',
  'className': 'Lớp',
  'name': 'Tên',
  'code': 'Năm học',
  'studentCode': 'Mã trẻ',
  'email': 'Email',
  'phoneNumber': 'SĐT',
  'roles': 'Vai trò',
  'status': 'Trạng thái',
  'schoolYear': 'Năm học',
  'date': 'Ngày ăn',
  'mealType': 'Bữa ăn',
  'cutoffAt': 'Giờ chốt',
  'isSettled': 'Đã chốt',
  'isCancelled': 'Đã hủy',
  'cancellationReason': 'Lý do hủy',
  'isActive': 'Đang học',
  'studentCount': 'Trẻ đang học',
  'parents': 'Phụ huynh',
  'fromDate': 'Từ ngày',
  'toDate': 'Đến ngày',
  'startDate': 'Ngày bắt đầu',
  'endDate': 'Ngày kết thúc',
  'reason': 'Lý do',
  'endReason': 'Lý do kết thúc',
  'reportedAt': 'Ngày báo',
  'cancelledAt': 'Ngày hủy',
  'revision': 'Phiên bản',
  'expectedRevision': 'Phiên bản đối chiếu',
  'version': 'Bản suất',
  'count': 'Số suất',
  'originalCount': 'Suất gốc',
  'kitchenAdjustment': 'Điều chỉnh riêng cho bếp',
  'students': 'Trẻ có suất',
  'studentNames': 'Trẻ có suất',
  'decisions': 'Nguồn suất đã lưu',
  'willEat': 'Có suất',
  'wasEating': 'Suất trước điều chỉnh',
  'source': 'Nguồn',
  'original': 'Bản gốc',
  'current': 'Bản hiện hành',
  'before': 'Bản trước',
  'after': 'Bản sau',
  'added': 'Trẻ tăng suất',
  'removed': 'Trẻ giảm suất',
  'quantity': 'Số suất',
  'isQuantityOnly': 'Điều chỉnh riêng cho bếp',
  'baseVersion': 'Bản dùng lập phiếu',
  'requestedAt': 'Ngày yêu cầu',
  'requestedByName': 'Người yêu cầu',
  'reviewedAt': 'Ngày duyệt',
  'reviewedByName': 'Người duyệt',
  'reviewReason': 'Lý do quyết định',
  'action': 'Thao tác',
  'latestAction': 'Ngoại lệ hiện tại',
  'latestReason': 'Lý do ngoại lệ',
  'parentReportedAbsent': 'Phụ huynh báo không ăn',
  'recordedAt': 'Ngày ghi nhận',
  'actorName': 'Người ghi nhận',
  'isLegacy': 'Dữ liệu cũ',
  'sequence': 'Thứ tự',
  'inspectorAccessUntil': 'Quyền thanh tra đến',
  'isOpen': 'Có tổ chức ăn',
  'isException': 'Ngày đặc biệt',
  'locked': 'Đã khóa',
  'mealTypes': 'Bữa ăn',
  'weekdays': 'Ngày trong tuần',
  'from': 'Từ ngày',
  'to': 'Đến ngày',
  'sessions': 'Các phiên ăn',
  'kind': 'Loại',
  'publishedAt': 'Ngày công bố',
  'settledPortions': 'Suất đã chốt',
  'dishes': 'Món ăn',
  'settlements': 'Lịch sử bản chốt',
  'evidence': 'Kiểm thực',
  'description': 'Mô tả',
  'capturedAt': 'Ngày ghi nhận',
  'syncedAt': 'Ngày đồng bộ',
  'photoUrl': 'Ảnh minh chứng',
  'message': 'Thông báo',
  'enabled': 'Đã bật gửi tin',
  'channel': 'Kênh gửi',
  'templateOnly': 'Chỉ dùng mẫu cố định',
  'notification': 'Kết quả gửi tin',
  'created': 'Đã tạo',
  'restored': 'Khôi phục',
  'existing': 'Đã có',
  'createCount': 'Số phiên sẽ tạo',
  'restoreCount': 'Số phiên khôi phục',
  'existingCount': 'Phiên đã có',
  'sourceYearCode': 'Sao chép từ năm',
  'isConfigured': 'Đã cấu hình',
  'total': 'Tổng số',
  'hasCompleteRoster': 'Có đầy đủ danh sách trẻ',
  'hasOriginalSources': 'Có nguồn bản gốc',
  'canRequest': 'Có thể yêu cầu điều chỉnh',
  'canEdit': 'Có thể sửa trước chốt',
};
const valueLabels = {
  ...roleLabels,
  'ACTIVE': 'Hoạt động',
  'SUSPENDED': 'Tạm khóa',
  'PENDING': 'Chờ duyệt',
  'APPROVED': 'Đã duyệt',
  'REJECTED': 'Đã từ chối',
  'DEFAULT': 'Mặc định',
  'PARENT_ABSENCE': 'Phụ huynh báo không ăn',
  'STAFF_EAT': 'Ngoại lệ có suất',
  'STAFF_ABSENT': 'Ngoại lệ không có suất',
  'APPROVED_AMENDMENT': 'Điều chỉnh đã duyệt',
  'LEGACY_OR_CURRENT_ENROLLMENT': 'Ghi danh / nguồn cũ',
  'EAT': 'Có suất',
  'ABSENT': 'Không có suất',
  'OPEN': 'Có tổ chức ăn',
  'CLOSED': 'Ngày nghỉ',
  'CREATE': 'Tạo mới',
  'RESTORE': 'Khôi phục',
  'REVOKED': 'Đã thu hồi',
  'FATHER': 'Cha',
  'MOTHER': 'Mẹ',
  'GUARDIAN': 'Người giám hộ',
  'EXISTS': 'Đã có, bỏ qua',
  'LOCKED': 'Đã khóa, bỏ qua',
  'OTHER_YEAR': 'Thuộc năm khác',
  'SCHEDULE': 'Lịch tuần',
  'GENERATE': 'Tạo phiên',
  'ACCEPTED': 'Đã tiếp nhận gửi tin',
  'FAILED': 'Gửi thất bại',
  'DISABLED': 'Chưa bật',
  'UNKNOWN': 'Chưa xác định kết quả gửi',
};

String displayValue(Object? value) {
  if (value == null || value == '') return '—';
  if (value is bool) return value ? 'Có' : 'Không';
  if (value is List<Object?>) {
    return value.isEmpty ? 'Chưa có' : value.map(displayValue).join(' · ');
  }
  final text = value.toString();
  final parsed = text.contains('T') ? DateTime.tryParse(text) : null;
  if (parsed != null) {
    final time = parsed.toUtc().add(const Duration(hours: 7));
    return '${time.day.toString().padLeft(2, '0')}/${time.month.toString().padLeft(2, '0')}/${time.year} ${time.hour.toString().padLeft(2, '0')}:${time.minute.toString().padLeft(2, '0')}';
  }
  if (RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(text)) {
    return '${text.substring(8)}/${text.substring(5, 7)}/${text.substring(0, 4)}';
  }
  return valueLabels[text] ?? text;
}

class RecordDetails extends StatelessWidget {
  const RecordDetails({super.key, required this.record, this.compact = false});
  final SchoolRecord record;
  final bool compact;
  @override
  Widget build(BuildContext context) {
    final entries = record.fields.entries
        .where(
          (entry) => fieldLabels.containsKey(entry.key) && entry.value != null,
        )
        .toList();
    final primary = entries
        .where(
          (entry) => [
            'className',
            'schoolYear',
            'date',
            'mealType',
            'status',
            'isActive',
            'studentCount',
            'count',
            'version',
            'isSettled',
            'isCancelled',
            'willEat',
            'fromDate',
            'toDate',
            'startDate',
            'endDate',
          ].contains(entry.key),
        )
        .toList();
    Widget row(MapEntry<String, Object?> entry) {
      final value = entry.value;
      if (entry.key == 'photoUrl') {
        final uri = Uri.tryParse(value.toString());
        if (uri == null ||
            !['https', 'http'].contains(uri.scheme) ||
            uri.host.isEmpty ||
            uri.userInfo.isNotEmpty) {
          return const Text('Đường dẫn ảnh không hợp lệ.');
        }
        return TextButton.icon(
          icon: const Icon(Icons.image_outlined),
          label: const Text('Xem ảnh minh chứng'),
          onPressed: () => showDialog<void>(
            context: context,
            builder: (context) => Dialog.fullscreen(
              child: Scaffold(
                appBar: AppBar(title: const Text('Ảnh minh chứng')),
                body: Center(
                  child: InteractiveViewer(
                    child: Image.network(
                      uri.toString(),
                      fit: BoxFit.contain,
                      errorBuilder: (_, error, stackTrace) => const Padding(
                        padding: EdgeInsets.all(24),
                        child: Text(
                          'Không tải được ảnh. Kiểm tra kết nối rồi mở lại.',
                        ),
                      ),
                      loadingBuilder: (_, child, progress) => progress == null
                          ? child
                          : const Center(child: CircularProgressIndicator()),
                    ),
                  ),
                ),
              ),
            ),
          ),
        );
      }
      if (value is SchoolRecord) {
        return ExpansionTile(
          title: Text(fieldLabels[entry.key]!),
          children: [RecordDetails(record: value)],
        );
      }
      if (value is List<Object?> && value.any((x) => x is SchoolRecord)) {
        final records = value.whereType<SchoolRecord>().toList();
        return ExpansionTile(
          title: Text('${fieldLabels[entry.key]} (${records.length})'),
          children: [
            for (final record in records)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      record.title,
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                    RecordDetails(record: record),
                  ],
                ),
              ),
          ],
        );
      }
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              flex: 2,
              child: Text(
                fieldLabels[entry.key]!,
                style: TextStyle(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(flex: 3, child: Text(displayValue(value))),
          ],
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (!compact)
          for (final entry in entries) row(entry)
        else
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final entry in primary)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 7,
                  ),
                  decoration: BoxDecoration(
                    color: Theme.of(context).colorScheme.surfaceContainerLow,
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Text(
                    '${fieldLabels[entry.key]}: ${displayValue(entry.value)}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ),
            ],
          ),
        if (compact && record.records('parents').isNotEmpty)
          TextButton(
            onPressed: () => showDialog<void>(
              context: context,
              builder: (context) => AlertDialog(
                title: const Text('Phụ huynh'),
                content: SingleChildScrollView(
                  child: RecordDetails(
                    record: SchoolRecord({
                      'parents': record.records('parents'),
                    }),
                  ),
                ),
                actions: [
                  TextButton(
                    onPressed: () => Navigator.pop(context),
                    child: const Text('Đóng'),
                  ),
                ],
              ),
            ),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                '${record.records('parents').first.title}${record.records('parents').length > 1 ? ' …' : ''}',
              ),
            ),
          ),
        if (compact && entries.length > primary.length)
          ExpansionTile(
            title: const Text('Thông tin thêm'),
            children: [
              for (final entry in entries.where(
                (x) => !primary.contains(x) && x.key != 'parents',
              ))
                row(entry),
            ],
          ),
      ],
    );
  }
}

class ReferencePicker extends StatefulWidget {
  const ReferencePicker({
    super.key,
    required this.field,
    required this.controller,
    required this.contextValues,
    required this.selected,
    this.onSelected,
  });
  final SchoolField field;
  final SchoolController controller;
  final Map<String, Object?> contextValues;
  final List<String> selected;
  final void Function(List<String> ids, List<String> labels)? onSelected;
  @override
  State<ReferencePicker> createState() => _ReferencePickerState();
}

class _ReferencePickerState extends State<ReferencePicker> {
  late final Set<String> selected = widget.selected.toSet();
  final search = TextEditingController();
  final selectedLabels = <String, String>{};
  List<SchoolRecord> records = [];
  int page = 1, total = 0, pageSize = 25, generation = 0;
  bool busy = true;
  String? error;
  @override
  void initState() {
    super.initState();
    load();
  }

  String id(SchoolRecord record) => record.text('id').isNotEmpty
      ? record.text('id')
      : record.text('studentId').isNotEmpty
      ? record.text('studentId')
      : record.text('code');
  Future<void> load() async {
    final current = ++generation;
    setState(() {
      busy = true;
      error = null;
    });
    final op = widget.field.reference!;
    final input = <String, Object?>{
      'page': page,
      'pageSize': 25,
      'search': search.text.trim(),
    };
    if (op == SchoolOperation.scopes) {
      input.addAll({
        'classPage': page,
        'studentPage': page,
        'selectedClassIds': widget.field.collection == 'classes'
            ? selected.join(',')
            : '',
        'selectedStudentIds': widget.field.collection == 'students'
            ? selected.join(',')
            : '',
      });
    }
    if (op == SchoolOperation.amendments) {
      input.addAll({'candidatePage': page, 'q': search.text.trim()});
    }
    try {
      final result = await widget.controller.execute(
        op,
        context: op == SchoolOperation.amendments
            ? {
                'dayId': widget.contextValues['dayId'],
                'classId': widget.contextValues['classId'],
              }
            : const {},
        input: input,
      );
      if (!mounted || current != generation) return;
      var rows = widget.field.collection == null
          ? result.records
          : result.summary.records(widget.field.collection!);
      if (op == SchoolOperation.parentLinkClasses &&
          widget.contextValues['schoolYear'] != null) {
        rows = rows
            .where(
              (x) => x.text('schoolYear') == widget.contextValues['schoolYear'],
            )
            .toList();
      }
      final totalKey = op == SchoolOperation.amendments
          ? 'candidateTotal'
          : widget.field.collection == 'classes'
          ? 'classTotal'
          : 'studentTotal';
      final serverPaged =
          op != SchoolOperation.assignedClasses &&
          op != SchoolOperation.years &&
          op != SchoolOperation.children &&
          op != SchoolOperation.parentLinkClasses &&
          op != SchoolOperation.reviewLinkClasses;
      total = serverPaged
          ? result.summary.number(totalKey, result.total)
          : rows.length;
      pageSize = result.pageSize;
      if (!serverPaged && search.text.trim().isNotEmpty) {
        rows = rows
            .where(
              (row) => row.title.toLowerCase().contains(
                search.text.trim().toLowerCase(),
              ),
            )
            .toList();
      }
      if (!serverPaged) {
        total = rows.length;
        rows = rows.skip((page - 1) * pageSize).take(pageSize).toList();
      }
      setState(() {
        records = rows;
        for (final row in rows) {
          selectedLabels[id(row)] = row.title;
        }
        busy = false;
      });
    } on SchoolFailure catch (failure) {
      if (mounted && current == generation) {
        setState(() {
          error = failure.message;
          busy = false;
        });
      }
    }
  }

  @override
  void dispose() {
    generation++;
    search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final multi = widget.field.kind == FieldKind.references;
    return Scaffold(
      appBar: AppBar(title: Text(widget.field.label)),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: TextField(
                controller: search,
                decoration: InputDecoration(
                  labelText: 'Tìm kiếm',
                  suffixIcon: IconButton(
                    icon: const Icon(Icons.search),
                    onPressed: () {
                      page = 1;
                      load();
                    },
                  ),
                ),
                onSubmitted: (_) {
                  page = 1;
                  load();
                },
              ),
            ),
            if (error != null)
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    Text(error!),
                    TextButton(onPressed: load, child: const Text('Thử lại')),
                  ],
                ),
              ),
            if (busy) const LinearProgressIndicator(),
            Expanded(
              child: ListView(
                children: [
                  if (!busy && error == null && records.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Text('Không có dữ liệu phù hợp.'),
                    ),
                  for (final record in records)
                    CheckboxListTile(
                      title: Text(record.title),
                      subtitle: record.text('schoolYear').isNotEmpty
                          ? Text(record.text('schoolYear'))
                          : record.text('className').isNotEmpty
                          ? Text(record.text('className'))
                          : null,
                      value: selected.contains(id(record)),
                      onChanged: busy
                          ? null
                          : (checked) {
                              if (!multi) {
                                widget.onSelected?.call(
                                  [id(record)],
                                  [record.title],
                                );
                                Navigator.pop(context, <String>[id(record)]);
                                return;
                              }
                              setState(() {
                                if (checked == true) {
                                  selected.add(id(record));
                                } else {
                                  selected.remove(id(record));
                                }
                              });
                            },
                    ),
                ],
              ),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                IconButton(
                  tooltip: 'Trang trước',
                  onPressed: !busy && page > 1
                      ? () {
                          page--;
                          load();
                        }
                      : null,
                  icon: const Icon(Icons.chevron_left),
                ),
                Text('Trang $page · $total mục'),
                IconButton(
                  tooltip: 'Trang sau',
                  onPressed: !busy && page * pageSize < total
                      ? () {
                          page++;
                          load();
                        }
                      : null,
                  icon: const Icon(Icons.chevron_right),
                ),
              ],
            ),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Wrap(
                spacing: 12,
                children: [
                  if (!widget.field.required)
                    TextButton(
                      onPressed: () {
                        widget.onSelected?.call([], []);
                        Navigator.pop(context, <String>[]);
                      },
                      child: const Text('Bỏ chọn'),
                    ),
                  if (multi)
                    FilledButton(
                      onPressed: () {
                        widget.onSelected?.call(
                          selected.toList(),
                          selected
                              .map((id) => selectedLabels[id] ?? 'Mục đã chọn')
                              .toList(),
                        );
                        Navigator.pop(context, selected.toList());
                      },
                      child: Text('Chọn ${selected.length} mục'),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

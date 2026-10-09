import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_form_page.dart';
import 'school_forms.dart';
import 'school_widgets.dart';

class SchoolPage extends StatefulWidget {
  const SchoolPage({
    super.key,
    required this.operation,
    required this.title,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
    this.contextValues = const {},
  });
  final SchoolOperation operation;
  final String title;
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  final Map<String, Object?> contextValues;
  @override
  State<SchoolPage> createState() => _SchoolPageState();
}

class _SchoolPageState extends State<SchoolPage> {
  late final SchoolController controller;
  final search = TextEditingController();
  final filters = <String, Object?>{};
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

  @override
  void dispose() {
    controller.dispose();
    search.dispose();
    super.dispose();
  }

  Future<void> load() async {
    if (widget.operation == SchoolOperation.calendar &&
        filters['code'] == null &&
        widget.contextValues['code'] == null) {
      return;
    }
    await controller.load(widget.operation, widget.contextValues, {
      ...filters,
      'page': page,
      if (widget.operation == SchoolOperation.amendments)
        'q': search.text.trim()
      else
        'search': search.text.trim(),
    });
  }

  Map<String, Object?> contextFor([SchoolRecord? record]) {
    final summary = controller.result?.summary;
    return {
      ...widget.contextValues,
      if (widget.operation == SchoolOperation.calendar && summary != null) ...{
        'from': summary['from'],
        'to': summary['to'],
      },
      ...filters,
      if (summary != null) 'expectedRevision': summary['revision'],
      if (summary != null) 'earliestChangeDate': summary['earliestChangeDate'],
      if (summary?.record('current') != null)
        'baseSettlementId': summary!.record('current')!['id'],
      if (record != null) ...record.fields,
      if (record?.text('id').isNotEmpty == true) 'id': record!['id'],
      if ([
            SchoolOperation.workflowDays,
            SchoolOperation.meals,
          ].contains(widget.operation) &&
          record != null)
        'dayId': record['id'],
      if ([
            SchoolOperation.students,
            SchoolOperation.scopedStudents,
          ].contains(widget.operation) &&
          record != null)
        'studentId': record['id'],
      if (widget.operation == SchoolOperation.decisions && record != null)
        'expectedEventId': record['latestEventId'],
      if (widget.operation == SchoolOperation.amendments && record != null)
        'requestId': record['id'],
      if (widget.operation == SchoolOperation.classes && record != null)
        'classId': record['id'],
    };
  }

  Future<void> openForm(
    SchoolOperation operation, [
    SchoolRecord? record,
    Map<String, Object?>? extra,
  ]) async {
    if (!canExecute(operation, widget.roles)) return;
    final done = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => SchoolFormPage(
          form: schoolForms[operation]!,
          controller: controller,
          contextValues: {
            ...contextFor(record),
            ...?extra,
            if (operation == SchoolOperation.linkParent) 'fullName': '',
            if (operation == SchoolOperation.linkParent) 'phoneNumber': '',
            if (operation == SchoolOperation.changeEnrollment)
              'effectiveDate':
                  controller.result?.summary['earliestChangeDate'] ??
                  schoolToday(),
            if (operation == SchoolOperation.calendarDay)
              'mode': record?.flag('isOpen') == true ? 'OPEN' : 'CLOSED',
            if (operation == SchoolOperation.calendarDay)
              'mealTypes': record?.values('mealTypes').isNotEmpty == true
                  ? record!.values('mealTypes')
                  : controller.result?.summary.values('mealTypes') ??
                        ['Bữa trưa'],
          },
        ),
      ),
    );
    if (done == true && mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Đã lưu thành công.')));
      await load();
    }
  }

  Future<void> openPage(
    SchoolOperation operation,
    String title, [
    SchoolRecord? record,
    Map<String, Object?>? extra,
  ]) async {
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => SchoolPage(
          operation: operation,
          title: title,
          repository: widget.repository,
          roles: widget.roles,
          onUnauthorized: widget.onUnauthorized,
          contextValues: {...contextFor(record), ...?extra},
        ),
      ),
    );
    if (mounted) await load();
  }

  Future<void> nextYear(SchoolRecord record) async {
    try {
      final result = await controller.execute(
        SchoolOperation.nextYear,
        context: {'code': record['code']},
      );
      if (!mounted) return;
      if (result.summary.flag('isConfigured')) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Năm học tiếp theo đã thiết lập.')),
        );
        return;
      }
      await openForm(SchoolOperation.saveYear, result.summary);
    } on SchoolFailure catch (failure) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(failure.message)));
      }
    }
  }

  List<({String title, VoidCallback run})> recordActions(SchoolRecord record) {
    final actions = <({String title, VoidCallback run})>[];
    void edit(SchoolOperation operation, String label, {bool enabled = true}) {
      if (enabled && canExecute(operation, widget.roles)) {
        actions.add((title: label, run: () => openForm(operation, record)));
      }
    }

    void read(
      SchoolOperation operation,
      String label, {
      Map<String, Object?>? extra,
    }) {
      if (canExecute(operation, widget.roles)) {
        actions.add((
          title: label,
          run: () => openPage(operation, label, record, extra),
        ));
      }
    }

    switch (widget.operation) {
      case SchoolOperation.users:
        edit(SchoolOperation.updateUser, 'Sửa tài khoản');
        edit(SchoolOperation.resetPassword, 'Đặt lại mật khẩu');
      case SchoolOperation.classes:
        read(
          SchoolOperation.students,
          'Trẻ trong lớp',
          extra: {'classId': record['id']},
        );
        edit(SchoolOperation.editClass, 'Sửa lớp');
      case SchoolOperation.students || SchoolOperation.scopedStudents:
        edit(SchoolOperation.editStudent, 'Sửa hồ sơ');
        edit(
          SchoolOperation.changeEnrollment,
          'Ghi danh / chuyển lớp / ngừng học',
        );
        read(SchoolOperation.enrollments, 'Lịch sử ghi danh');
        edit(SchoolOperation.linkParent, 'Liên kết phụ huynh');
      case SchoolOperation.yearConfiguration:
        if (record.text('startDate').isEmpty) {
          edit(SchoolOperation.saveYear, 'Thiết lập ngày');
        } else {
          actions.add((
            title: 'Tạo năm tiếp theo',
            run: () => nextYear(record),
          ));
        }
      case SchoolOperation.absences:
        final active =
            record['cancelledAt'] == null &&
            record.text('toDate').compareTo(schoolToday()) >= 0;
        edit(
          SchoolOperation.replaceAbsence,
          'Sửa khoảng ngày',
          enabled: active,
        );
        edit(SchoolOperation.cancelAbsence, 'Hủy / ăn lại', enabled: active);
      case SchoolOperation.workflowDays:
        read(
          SchoolOperation.portions,
          '${record.text('mealType')} · ${displayValue(record['date'])}',
        );
      case SchoolOperation.portions:
        read(
          SchoolOperation.amendments,
          'Điều chỉnh sau chốt',
          extra: {'classId': record['classId']},
        );
      case SchoolOperation.decisions:
        final summary = controller.result?.summary;
        final cutoff = DateTime.tryParse(summary?.text('cutoffAt') ?? '');
        edit(
          SchoolOperation.recordException,
          'Sửa dự kiến suất',
          enabled:
              summary?.flag('canEdit') == true &&
              cutoff != null &&
              DateTime.now().isBefore(cutoff),
        );
        read(SchoolOperation.exceptionHistory, 'Lịch sử nguồn suất');
      case SchoolOperation.amendments:
        read(SchoolOperation.amendmentDetail, 'Đối chiếu bản trước / sau');
        edit(
          SchoolOperation.reviewAmendment,
          'Duyệt / từ chối',
          enabled:
              record.text('status') == 'PENDING' &&
              record['baseSettlementId'] ==
                  controller.result?.summary.record('current')?['id'],
        );
      case SchoolOperation.calendar:
        edit(
          SchoolOperation.calendarDay,
          'Sửa ngày ăn',
          enabled: !record.flag('locked'),
        );
      case SchoolOperation.meals:
        read(SchoolOperation.mealDetail, 'Hồ sơ ngày ăn');
      default:
        break;
    }
    return actions;
  }

  List<SchoolOperation> get createActions => switch (widget.operation) {
    SchoolOperation.users => [SchoolOperation.createUser],
    SchoolOperation.classes => [SchoolOperation.createClass],
    SchoolOperation.students ||
    SchoolOperation.scopedStudents => [SchoolOperation.createStudent],
    SchoolOperation.yearConfiguration => [SchoolOperation.saveYear],
    SchoolOperation.absences => [SchoolOperation.reportAbsence],
    SchoolOperation.workflowDays => [SchoolOperation.createMealDay],
    SchoolOperation.calendar =>
      controller.result == null
          ? []
          : [SchoolOperation.schedule, SchoolOperation.previewSessions],
    SchoolOperation.amendments =>
      controller.result?.summary.flag('canRequest') == true
          ? [SchoolOperation.requestAmendment]
          : [],
    _ => [],
  };

  List<SchoolField> get filterFields => switch (widget.operation) {
    SchoolOperation.users => [
      const SchoolField(
        'role',
        'Vai trò',
        kind: FieldKind.choice,
        required: false,
        options: roleLabels,
      ),
      const SchoolField(
        'classId',
        'Lớp giáo viên',
        kind: FieldKind.reference,
        required: false,
        reference: SchoolOperation.assignedClasses,
      ),
    ],
    SchoolOperation.students || SchoolOperation.scopedStudents => [
      const SchoolField(
        'classId',
        'Lớp',
        kind: FieldKind.reference,
        required: false,
        reference: SchoolOperation.assignedClasses,
      ),
      const SchoolField(
        'status',
        'Trạng thái',
        kind: FieldKind.choice,
        required: false,
        options: {'ACTIVE': 'Đang học', 'INACTIVE': 'Chưa học / đã ngừng'},
      ),
      const SchoolField(
        'parentStatus',
        'Phụ huynh',
        kind: FieldKind.choice,
        required: false,
        options: {'LINKED': 'Đã liên kết', 'UNLINKED': 'Chưa liên kết'},
      ),
    ],
    SchoolOperation.absences => [
      const SchoolField(
        'studentId',
        'Trẻ',
        kind: FieldKind.reference,
        required: false,
        reference: SchoolOperation.children,
      ),
      const SchoolField(
        'status',
        'Trạng thái',
        kind: FieldKind.choice,
        required: false,
        options: {
          'ACTIVE': 'Đang hiệu lực',
          'UPCOMING': 'Sắp áp dụng',
          'EXPIRED': 'Đã hết hạn',
          'CANCELLED': 'Đã hủy / thay thế',
        },
      ),
    ],
    SchoolOperation.workflowDays => [
      const SchoolField(
        'date',
        'Ngày ăn',
        kind: FieldKind.date,
        required: false,
      ),
    ],
    SchoolOperation.meals => [
      const SchoolField(
        'date',
        'Ngày ăn',
        kind: FieldKind.date,
        required: false,
      ),
      const SchoolField(
        'status',
        'Trạng thái',
        kind: FieldKind.choice,
        required: false,
        options: {
          'SETTLED': 'Đã chốt',
          'PENDING': 'Chưa chốt',
          'CANCELLED': 'Đã hủy',
        },
      ),
    ],
    SchoolOperation.decisions => [
      const SchoolField(
        'classId',
        'Lớp',
        kind: FieldKind.reference,
        required: false,
        reference: SchoolOperation.assignedClasses,
      ),
    ],
    SchoolOperation.calendar => [
      const SchoolField('from', 'Từ ngày', kind: FieldKind.date),
      const SchoolField('to', 'Đến ngày', kind: FieldKind.date),
    ],
    _ => [],
  };

  Widget filter(SchoolField field) {
    if (field.kind == FieldKind.choice) {
      return DropdownButtonFormField<String>(
        key: ValueKey('${field.key}:${filters[field.key]}'),
        initialValue: filters[field.key] as String?,
        isExpanded: true,
        decoration: InputDecoration(labelText: field.label),
        items: [
          const DropdownMenuItem(value: '', child: Text('Tất cả')),
          ...field.options.entries.map(
            (x) => DropdownMenuItem(value: x.key, child: Text(x.value)),
          ),
        ],
        onChanged: controller.loading
            ? null
            : (value) => setState(() => filters[field.key] = value),
      );
    }
    if (field.kind == FieldKind.reference) {
      return OutlinedButton.icon(
        onPressed: controller.loading
            ? null
            : () async {
                final values = await Navigator.push<List<String>>(
                  context,
                  MaterialPageRoute(
                    builder: (_) => ReferencePicker(
                      field: field,
                      controller: controller,
                      contextValues: const {},
                      selected: filters[field.key] == null
                          ? []
                          : [filters[field.key].toString()],
                    ),
                  ),
                );
                if (values != null && mounted) {
                  setState(
                    () => filters[field.key] = values.isEmpty
                        ? null
                        : values.first,
                  );
                }
              },
        icon: const Icon(Icons.filter_alt_outlined),
        label: Text(
          '${field.label} · ${filters[field.key] == null ? 'Tất cả' : 'Đã chọn'}',
        ),
      );
    }
    return OutlinedButton.icon(
      onPressed: controller.loading
          ? null
          : () async {
              final date = await showDatePicker(
                context: context,
                initialDate:
                    DateTime.tryParse(filters[field.key]?.toString() ?? '') ??
                    DateTime.now(),
                firstDate: DateTime(2000),
                lastDate: DateTime(2100),
              );
              if (date != null && mounted) {
                setState(
                  () => filters[field.key] =
                      '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}',
                );
              }
            },
      icon: const Icon(Icons.calendar_month),
      label: Text('${field.label}: ${displayValue(filters[field.key])}'),
    );
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final result = controller.result;
      final summary = result?.summary;
      final paged = [
        SchoolOperation.users,
        SchoolOperation.classes,
        SchoolOperation.students,
        SchoolOperation.scopedStudents,
        SchoolOperation.absences,
        SchoolOperation.workflowDays,
        SchoolOperation.decisions,
        SchoolOperation.exceptionHistory,
        SchoolOperation.amendments,
        SchoolOperation.meals,
      ].contains(widget.operation);
      final pageSize =
          summary?.number(
            'pageSize',
            widget.operation == SchoolOperation.classes ||
                    widget.operation == SchoolOperation.students ||
                    widget.operation == SchoolOperation.scopedStudents
                ? 20
                : 25,
          ) ??
          25;
      return Scaffold(
        appBar: AppBar(
          title: Text(widget.title),
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
                if (widget.operation == SchoolOperation.calendar)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: FilledButton.tonalIcon(
                      onPressed: () async {
                        final selected = await Navigator.push<List<String>>(
                          context,
                          MaterialPageRoute(
                            builder: (_) => ReferencePicker(
                              field: const SchoolField(
                                'code',
                                'Năm học',
                                kind: FieldKind.reference,
                                reference: SchoolOperation.years,
                              ),
                              controller: controller,
                              contextValues: const {},
                              selected: filters['code'] == null
                                  ? []
                                  : [filters['code'].toString()],
                            ),
                          ),
                        );
                        if (selected?.isNotEmpty == true && mounted) {
                          setState(() {
                            filters['code'] = selected!.first;
                            filters.remove('from');
                            filters.remove('to');
                            page = 1;
                          });
                          await load();
                        }
                      },
                      icon: const Icon(Icons.school),
                      label: Text(
                        'Năm học: ${filters['code'] ?? 'Chọn năm học'}',
                      ),
                    ),
                  ),
                if (paged ||
                    widget.operation == SchoolOperation.calendar ||
                    widget.operation == SchoolOperation.yearConfiguration)
                  Card(
                    child: ExpansionTile(
                      title: Text(
                        'Tìm kiếm & bộ lọc${filters.values.where((x) => x != null && x != '').isNotEmpty ? ' · đang áp dụng' : ''}',
                      ),
                      childrenPadding: const EdgeInsets.all(16),
                      children: [
                        if (widget.operation != SchoolOperation.calendar)
                          TextField(
                            controller: search,
                            decoration: const InputDecoration(
                              labelText: 'Tìm kiếm',
                            ),
                            onSubmitted: (_) {
                              page = 1;
                              load();
                            },
                          ),
                        for (final field in filterFields)
                          Padding(
                            padding: const EdgeInsets.only(top: 12),
                            child: filter(field),
                          ),
                        const SizedBox(height: 12),
                        Wrap(
                          spacing: 12,
                          children: [
                            FilledButton(
                              onPressed: controller.loading
                                  ? null
                                  : () {
                                      page = 1;
                                      load();
                                    },
                              child: const Text('Áp dụng'),
                            ),
                            TextButton(
                              onPressed: controller.loading
                                  ? null
                                  : () {
                                      setState(() {
                                        final code = filters['code'];
                                        filters.clear();
                                        if (code != null) {
                                          filters['code'] = code;
                                        }
                                        search.clear();
                                        page = 1;
                                      });
                                      load();
                                    },
                              child: const Text('Bỏ lọc'),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final operation in createActions.where(
                      (op) => canExecute(op, widget.roles),
                    ))
                      FilledButton(
                        onPressed: controller.loading
                            ? null
                            : () => openForm(
                                operation,
                                null,
                                widget.operation == SchoolOperation.calendar
                                    ? {
                                        'weekdays':
                                            summary
                                                    ?.values('weekdays')
                                                    .isNotEmpty ==
                                                true
                                            ? summary!.values('weekdays')
                                            : ['1', '2', '3', '4', '5'],
                                        'mealTypes':
                                            summary
                                                    ?.values('mealTypes')
                                                    .isNotEmpty ==
                                                true
                                            ? summary!.values('mealTypes')
                                            : ['Bữa trưa'],
                                      }
                                    : null,
                              ),
                        child: Text(schoolForms[operation]!.title),
                      ),
                  ],
                ),
                if (widget.operation == SchoolOperation.portions &&
                    summary != null)
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      if (canExecute(SchoolOperation.decisions, widget.roles))
                        OutlinedButton(
                          onPressed: () => openPage(
                            SchoolOperation.decisions,
                            'Nguồn trước điều chỉnh',
                          ),
                          child: const Text('Nguồn trước điều chỉnh'),
                        ),
                      if (!summary.flag('isSettled') &&
                          !summary.flag('isCancelled') &&
                          canExecute(SchoolOperation.settle, widget.roles))
                        FilledButton(
                          onPressed: () => openForm(SchoolOperation.settle),
                          child: const Text('Chốt suất'),
                        ),
                    ],
                  ),
                if (widget.operation == SchoolOperation.calendar &&
                    summary != null)
                  TextButton(
                    onPressed: () => openPage(
                      SchoolOperation.calendarHistory,
                      'Lịch sử lịch bữa ăn',
                    ),
                    child: const Text('Lịch sử lịch bữa ăn'),
                  ),
                if (controller.loading)
                  const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                if (controller.error != null)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(20),
                      child: Column(
                        children: [
                          Text(controller.error!),
                          TextButton(
                            onPressed: load,
                            child: const Text('Thử lại'),
                          ),
                        ],
                      ),
                    ),
                  ),
                if (summary != null &&
                    ![
                      SchoolOperation.users,
                      SchoolOperation.students,
                      SchoolOperation.scopedStudents,
                      SchoolOperation.classes,
                      SchoolOperation.absences,
                      SchoolOperation.workflowDays,
                      SchoolOperation.meals,
                    ].contains(widget.operation))
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: RecordDetails(record: summary),
                    ),
                  ),
                if (!controller.loading &&
                    result != null &&
                    result.records.isEmpty &&
                    paged)
                  const Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Không có dữ liệu phù hợp.'),
                  ),
                for (final record
                    in (widget.operation == SchoolOperation.yearConfiguration
                            ? result?.records.where(
                                (row) => row.title.contains(search.text.trim()),
                              )
                            : result?.records) ??
                        <SchoolRecord>[])
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  record.title,
                                  style: Theme.of(
                                    context,
                                  ).textTheme.titleMedium,
                                ),
                              ),
                              if (recordActions(record).isNotEmpty)
                                PopupMenuButton<int>(
                                  tooltip: 'Thao tác',
                                  onSelected: (index) =>
                                      recordActions(record)[index].run(),
                                  itemBuilder: (_) => [
                                    for (final (index, action) in recordActions(
                                      record,
                                    ).indexed)
                                      PopupMenuItem(
                                        value: index,
                                        child: Text(action.title),
                                      ),
                                  ],
                                ),
                            ],
                          ),
                          RecordDetails(record: record, compact: true),
                        ],
                      ),
                    ),
                  ),
                if (paged && result != null)
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      IconButton(
                        tooltip: 'Trang trước',
                        onPressed: page > 1 && !controller.loading
                            ? () {
                                page--;
                                load();
                              }
                            : null,
                        icon: const Icon(Icons.chevron_left),
                      ),
                      Flexible(
                        child: Text('Trang $page · ${result.total} mục'),
                      ),
                      IconButton(
                        tooltip: 'Trang sau',
                        onPressed:
                            page * pageSize < result.total &&
                                !controller.loading
                            ? () {
                                page++;
                                load();
                              }
                            : null,
                        icon: const Icon(Icons.chevron_right),
                      ),
                    ],
                  ),
                const SizedBox(height: 24),
              ],
            ),
          ),
        ),
      );
    },
  );
}

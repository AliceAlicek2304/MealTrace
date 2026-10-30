import 'dart:async';
import 'package:flutter/material.dart';
import '../../../app/mealtrace_brand.dart';
import '../../../app/theme.dart';
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
    this.embedded = false,
    this.parentName,
    this.onOpenParentProfile,
    this.onParentTabSelected,
  });
  final SchoolOperation operation;
  final String title;
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  final Map<String, Object?> contextValues;
  final bool embedded;
  final String? parentName;
  final VoidCallback? onOpenParentProfile;
  final ValueChanged<int>? onParentTabSelected;
  @override
  State<SchoolPage> createState() => _SchoolPageState();
}

class _SchoolPageState extends State<SchoolPage> {
  late final SchoolController controller;
  final search = TextEditingController();
  final filters = <String, Object?>{};
  int page = 1;
  bool _showParentFilters = false;
  Future<SchoolResult>? _parentChildren;
  Timer? searchDebounce;
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
    if (widget.operation == SchoolOperation.absences &&
        widget.roles.contains('PARENT')) {
      _parentChildren = controller.execute(SchoolOperation.children);
    }
    load();
  }

  @override
  void dispose() {
    searchDebounce?.cancel();
    controller.dispose();
    search.dispose();
    super.dispose();
  }

  Future<void> load() async {
    searchDebounce?.cancel();
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
          onParentTabSelected:
              operation == SchoolOperation.reportAbsence &&
                  widget.roles.contains('PARENT')
              ? widget.onParentTabSelected
              : null,
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
      case SchoolOperation.parentLinks:
        edit(
          SchoolOperation.cancelParentLink,
          'Hủy yêu cầu',
          enabled: record.text('status') == 'PENDING',
        );
      case SchoolOperation.reviewableParentLinks:
        edit(
          SchoolOperation.revokeParentLink,
          'Thu hồi liên kết sai',
          enabled: record.text('status') == 'APPROVED',
        );
        edit(
          SchoolOperation.reviewParentLink,
          'Duyệt / từ chối',
          enabled: record.text('status') == 'PENDING',
        );
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
    SchoolOperation.parentLinks => [SchoolOperation.createParentLink],
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
    SchoolOperation.parentLinks || SchoolOperation.reviewableParentLinks => [
      const SchoolField(
        'status',
        'Trạng thái',
        kind: FieldKind.choice,
        required: false,
        options: {
          'PENDING': 'Chờ duyệt',
          'APPROVED': 'Đã duyệt',
          'REJECTED': 'Từ chối',
          'CANCELLED': 'Đã hủy',
          'REVOKED': 'Đã thu hồi',
        },
      ),
    ],
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
          'REVOKED': 'Đã thu hồi',
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
  Widget build(BuildContext context) {
    final isParentAbsences =
        widget.operation == SchoolOperation.absences &&
        widget.roles.contains('PARENT');
    if (!isParentAbsences || Theme.of(context).brightness != Brightness.dark) {
      return _buildPage(context);
    }
    return Theme(
      data: buildParentDarkTheme(Theme.of(context)),
      child: Builder(builder: _buildPage),
    );
  }

  Widget _buildPage(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final result = controller.result;
      final summary = result?.summary;
      final teacherWorkflowDays =
          widget.operation == SchoolOperation.workflowDays &&
          widget.roles.contains('TEACHER');
      final availableActions = createActions
          .where((op) => canExecute(op, widget.roles))
          .toList();
      final paged = [
        SchoolOperation.parentLinks,
        SchoolOperation.reviewableParentLinks,
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
      if (widget.operation == SchoolOperation.absences &&
          widget.roles.contains('PARENT')) {
        return _parentAbsencesPage(context, result, availableActions, pageSize);
      }
      return Scaffold(
        floatingActionButton: availableActions.length == 1
            ? FloatingActionButton.extended(
                onPressed: controller.loading
                    ? null
                    : () => openForm(availableActions.single),
                icon: const Icon(Icons.add),
                label: Text(schoolForms[availableActions.single]!.title),
              )
            : null,
        appBar: AppBar(
          automaticallyImplyLeading: !widget.embedded,
          title: widget.roles.contains('TEACHER')
              ? const MealTraceBrand(iconSize: 26, fontSize: 16)
              : Text(widget.title),
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
              keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
              padding: EdgeInsets.fromLTRB(
                16,
                16,
                16,
                availableActions.length == 1 ? 96 : 16,
              ),
              children: [
                if (widget.roles.contains('TEACHER')) ...[
                  Padding(
                    padding: const EdgeInsets.only(bottom: 14),
                    child: Text(
                      widget.title,
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                  ),
                ],
                if (paged &&
                        ![
                          SchoolOperation.parentLinks,
                          SchoolOperation.reviewableParentLinks,
                          SchoolOperation.exceptionHistory,
                          SchoolOperation.workflowDays,
                        ].contains(widget.operation) ||
                    widget.operation == SchoolOperation.yearConfiguration)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: TextField(
                      controller: search,
                      textInputAction: TextInputAction.search,
                      decoration: InputDecoration(
                        hintText: 'Tìm trong ${widget.title.toLowerCase()}',
                        prefixIcon: const Icon(Icons.search),
                        suffixIcon: IconButton(
                          tooltip: 'Xóa tìm kiếm',
                          icon: const Icon(Icons.close),
                          onPressed: () {
                            searchDebounce?.cancel();
                            search.clear();
                            page = 1;
                            load();
                          },
                        ),
                      ),
                      onChanged: (_) {
                        searchDebounce?.cancel();
                        searchDebounce = Timer(
                          const Duration(milliseconds: 350),
                          () {
                            if (!mounted) return;
                            page = 1;
                            load();
                          },
                        );
                      },
                      onSubmitted: (_) {
                        searchDebounce?.cancel();
                        FocusScope.of(context).unfocus();
                        page = 1;
                        load();
                      },
                    ),
                  ),
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
                if (filterFields.isNotEmpty)
                  Card(
                    child: ExpansionTile(
                      leading: teacherWorkflowDays
                          ? Icon(
                              Icons.filter_alt_outlined,
                              color: Theme.of(context).colorScheme.secondary,
                            )
                          : null,
                      title: Text(
                        'Bộ lọc${filters.entries.where((x) => x.key != 'code' && x.value != null && x.value != '').isNotEmpty ? ' · đang áp dụng' : ''}',
                      ),
                      childrenPadding: const EdgeInsets.all(16),
                      children: [
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
                if (availableActions.length > 1)
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final operation in availableActions)
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
                    (paged || widget.operation == SchoolOperation.children))
                  Padding(
                    padding: const EdgeInsets.all(24),
                    child: Text(
                      widget.operation == SchoolOperation.children
                          ? 'Tài khoản chưa được liên kết với trẻ. Mở Chức năng → Liên kết trẻ để gửi yêu cầu cho nhà trường.'
                          : 'Không có dữ liệu phù hợp.',
                    ),
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
                                  style: Theme.of(context).textTheme.titleMedium
                                      ?.copyWith(
                                        fontWeight: teacherWorkflowDays
                                            ? FontWeight.w800
                                            : null,
                                      ),
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
                          if (teacherWorkflowDays)
                            _teacherWorkflowDayDetails(context, record)
                          else
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
                        child: Text(
                          'Trang $page / ${(result.total / pageSize).ceil().clamp(1, 999999)} · ${result.total} mục',
                        ),
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

  Widget _teacherWorkflowDayDetails(BuildContext context, SchoolRecord record) {
    final colors = Theme.of(context).colorScheme;
    final cancelled = record.flag('isCancelled');
    final settled = record.flag('isSettled');
    final badges =
        <
          ({
            String label,
            String value,
            IconData icon,
            Color background,
            Color foreground,
          })
        >[
          (
            label: 'Ngày ăn',
            value: displayValue(record['date']),
            icon: Icons.calendar_today_outlined,
            background: colors.secondaryContainer,
            foreground: colors.onSecondaryContainer,
          ),
          (
            label: 'Bữa ăn',
            value: displayValue(record['mealType']),
            icon: Icons.restaurant_outlined,
            background: colors.surfaceContainerLow,
            foreground: colors.onSurface,
          ),
          (
            label: 'Năm học',
            value: displayValue(record['schoolYear']),
            icon: Icons.school_outlined,
            background: colors.surfaceContainerLow,
            foreground: colors.onSurface,
          ),
          (
            label: 'Đã hủy',
            value: displayValue(cancelled),
            icon: cancelled
                ? Icons.cancel_outlined
                : Icons.check_circle_outline,
            background: cancelled
                ? colors.errorContainer
                : colors.surfaceContainerLow,
            foreground: cancelled
                ? colors.onErrorContainer
                : colors.onSurfaceVariant,
          ),
          (
            label: 'Đã chốt',
            value: displayValue(settled),
            icon: settled ? Icons.lock_outline : Icons.lock_open_outlined,
            background: settled
                ? colors.tertiaryContainer
                : colors.surfaceContainerLow,
            foreground: settled
                ? colors.onTertiaryContainer
                : colors.onSurfaceVariant,
          ),
        ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        LayoutBuilder(
          builder: (context, constraints) {
            final width = (constraints.maxWidth - 8) / 2;
            return Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final badge in badges)
                  SizedBox(
                    width: width,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 9,
                      ),
                      decoration: BoxDecoration(
                        color: badge.background,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Row(
                        children: [
                          Icon(badge.icon, size: 15, color: badge.foreground),
                          const SizedBox(width: 7),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  badge.label,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: Theme.of(context).textTheme.labelSmall
                                      ?.copyWith(
                                        color: colors.onSurfaceVariant,
                                      ),
                                ),
                                const SizedBox(height: 2),
                                Text(
                                  badge.value,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: Theme.of(context).textTheme.labelMedium
                                      ?.copyWith(
                                        color: badge.foreground,
                                        fontWeight: FontWeight.w800,
                                      ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
        Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            tilePadding: EdgeInsets.zero,
            childrenPadding: const EdgeInsets.fromLTRB(4, 0, 4, 10),
            leading: Icon(
              Icons.info_outline,
              size: 17,
              color: colors.secondary,
            ),
            title: Text(
              'Thông tin thêm',
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: colors.onSurfaceVariant,
                fontWeight: FontWeight.w700,
              ),
            ),
            children: [
              _teacherWorkflowDayInfoLine(
                context,
                'Giờ chốt',
                displayValue(record['cutoffAt']),
              ),
              if (record.text('cancellationReason').isNotEmpty)
                _teacherWorkflowDayInfoLine(
                  context,
                  'Lý do hủy',
                  record.text('cancellationReason'),
                ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _teacherWorkflowDayInfoLine(
    BuildContext context,
    String label,
    String value,
  ) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 92,
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
        ),
        Expanded(
          child: Text(value, style: Theme.of(context).textTheme.labelSmall),
        ),
      ],
    ),
  );

  Widget _parentAbsencesPage(
    BuildContext context,
    SchoolResult? result,
    List<SchoolOperation> availableActions,
    int pageSize,
  ) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final name = widget.parentName?.trim() ?? '';
    final initial = name.isEmpty ? 'P' : name.substring(0, 1).toUpperCase();
    return Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: !widget.embedded,
        title: const MealTraceBrand(iconSize: 25, fontSize: 15),
        actions: [
          Semantics(
            label: 'Thông báo',
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: Stack(
                clipBehavior: Clip.none,
                children: [
                  Icon(Icons.notifications_none, color: colors.onSurface),
                  Positioned(
                    right: -1,
                    top: -1,
                    child: Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: colors.error,
                        shape: BoxShape.circle,
                        border: Border.all(color: colors.surface, width: 1.5),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.only(right: 14),
            child: InkWell(
              customBorder: const CircleBorder(),
              onTap: widget.onOpenParentProfile,
              child: CircleAvatar(
                radius: 16,
                backgroundColor: colors.secondaryContainer,
                foregroundColor: colors.onSecondaryContainer,
                child: Text(
                  initial,
                  style: theme.textTheme.labelMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
      floatingActionButton: availableActions.length == 1
          ? FloatingActionButton.extended(
              onPressed: controller.loading
                  ? null
                  : () => openForm(availableActions.single),
              icon: const Icon(Icons.add),
              label: Text(schoolForms[availableActions.single]!.title),
            )
          : null,
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: load,
          child: ListView(
            keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 104),
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Báo vắng / Không ăn',
                          style: theme.textTheme.titleLarge?.copyWith(
                            color: colors.onSurface,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -.35,
                          ),
                        ),
                        const SizedBox(height: 3),
                        Text(
                          'Theo dõi các đợt nghỉ ăn đã đăng ký.',
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: colors.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    tooltip: 'Tải lại',
                    onPressed: controller.loading ? null : load,
                    icon: const Icon(Icons.refresh_rounded),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              TextField(
                controller: search,
                textInputAction: TextInputAction.search,
                decoration: InputDecoration(
                  hintText: 'Tìm trong báo vắng / không ăn',
                  prefixIcon: const Icon(Icons.search_rounded),
                  suffixIcon: IconButton(
                    tooltip: 'Xóa tìm kiếm',
                    icon: const Icon(Icons.close_rounded),
                    onPressed: () {
                      searchDebounce?.cancel();
                      search.clear();
                      page = 1;
                      load();
                    },
                  ),
                ),
                onChanged: (_) {
                  searchDebounce?.cancel();
                  searchDebounce = Timer(const Duration(milliseconds: 350), () {
                    if (!mounted) return;
                    page = 1;
                    load();
                  });
                },
                onSubmitted: (_) {
                  searchDebounce?.cancel();
                  FocusScope.of(context).unfocus();
                  page = 1;
                  load();
                },
              ),
              const SizedBox(height: 10),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    OutlinedButton.icon(
                      onPressed: () => setState(
                        () => _showParentFilters = !_showParentFilters,
                      ),
                      icon: const Icon(Icons.filter_alt_outlined, size: 16),
                      label: const Text('Bộ lọc'),
                    ),
                    const SizedBox(width: 8),
                    _parentStatusFilter(context),
                    const SizedBox(width: 8),
                    _parentStudentFilter(context),
                  ],
                ),
              ),
              if (_showParentFilters) ...[
                const SizedBox(height: 8),
                _parentFilterPanel(context),
              ],
              if (controller.loading)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 14),
                  child: LinearProgressIndicator(),
                ),
              if (controller.error != null)
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      children: [
                        Text(controller.error!),
                        TextButton.icon(
                          onPressed: controller.loading ? null : load,
                          icon: const Icon(Icons.refresh),
                          label: const Text('Thử tải lại'),
                        ),
                      ],
                    ),
                  ),
                ),
              if (!controller.loading &&
                  result != null &&
                  result.records.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 34),
                  child: Column(
                    children: [
                      Icon(
                        Icons.event_available_outlined,
                        size: 36,
                        color: colors.onSurfaceVariant,
                      ),
                      const SizedBox(height: 8),
                      Text(
                        'Chưa có bản ghi báo vắng phù hợp.',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: colors.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
              if (result != null && result.records.isNotEmpty) ...[
                const SizedBox(height: 4),
                _parentAbsenceCards(result.records),
              ],
              if (result != null) _parentAbsencePagination(result, pageSize),
            ],
          ),
        ),
      ),
    );
  }

  Widget _parentStatusFilter(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    const labels = {
      '': 'Tất cả trạng thái',
      'ACTIVE': 'Đang áp dụng',
      'UPCOMING': 'Chờ đến ngày',
      'EXPIRED': 'Hoàn thành',
      'CANCELLED': 'Đã hủy',
    };
    final value = filters['status']?.toString() ?? '';
    return PopupMenuButton<String>(
      tooltip: 'Lọc trạng thái',
      initialValue: value,
      onSelected: (selected) {
        setState(() {
          filters['status'] = selected.isEmpty ? null : selected;
          page = 1;
        });
        load();
      },
      itemBuilder: (context) => [
        for (final entry in labels.entries)
          PopupMenuItem(value: entry.key, child: Text(entry.value)),
      ],
      child: _parentFilterPill(
        context,
        label: labels[value] ?? labels['']!,
        icon: Icons.expand_more,
        color: value.isEmpty ? colors.onSurfaceVariant : colors.secondary,
      ),
    );
  }

  Widget _parentStudentFilter(BuildContext context) {
    final field = filterFields.firstWhere((item) => item.key == 'studentId');
    final colors = Theme.of(context).colorScheme;
    return InkWell(
      borderRadius: BorderRadius.circular(24),
      onTap: controller.loading
          ? null
          : () async {
              final selected = await Navigator.push<List<String>>(
                context,
                MaterialPageRoute(
                  builder: (_) => ReferencePicker(
                    field: field,
                    controller: controller,
                    contextValues: const {},
                    selected: filters['studentId'] == null
                        ? []
                        : [filters['studentId'].toString()],
                  ),
                ),
              );
              if (selected != null && mounted) {
                setState(() {
                  filters['studentId'] = selected.isEmpty
                      ? null
                      : selected.first;
                  page = 1;
                });
                load();
              }
            },
      child: _parentFilterPill(
        context,
        label: filters['studentId'] == null ? 'Tất cả trẻ' : 'Đã chọn trẻ',
        icon: Icons.expand_more,
        color: filters['studentId'] == null
            ? colors.onSurfaceVariant
            : colors.secondary,
      ),
    );
  }

  Widget _parentFilterPill(
    BuildContext context, {
    required String label,
    required IconData icon,
    required Color color,
  }) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
    decoration: BoxDecoration(
      color: Theme.of(context).colorScheme.surfaceContainer,
      borderRadius: BorderRadius.circular(24),
      border: Border.all(color: Theme.of(context).colorScheme.outlineVariant),
    ),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          label,
          style: Theme.of(context).textTheme.labelMedium?.copyWith(
            color: color,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(width: 3),
        Icon(icon, size: 16, color: color),
      ],
    ),
  );

  Widget _parentFilterPanel(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final childField = filterFields.firstWhere(
      (field) => field.key == 'studentId',
    );
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Lọc theo trẻ',
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: colors.onSurfaceVariant,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 8),
            filter(childField),
            const SizedBox(height: 10),
            Wrap(
              alignment: WrapAlignment.end,
              spacing: 8,
              children: [
                TextButton(
                  onPressed: controller.loading
                      ? null
                      : () {
                          setState(() {
                            filters.remove('studentId');
                            filters.remove('status');
                            page = 1;
                          });
                          load();
                        },
                  child: const Text('Xóa lọc'),
                ),
                FilledButton(
                  onPressed: controller.loading
                      ? null
                      : () {
                          page = 1;
                          load();
                        },
                  child: const Text('Áp dụng'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _parentAbsenceCards(List<SchoolRecord> records) =>
      FutureBuilder<SchoolResult>(
        future: _parentChildren,
        builder: (context, snapshot) {
          final childById = <String, SchoolRecord>{
            for (final child
                in snapshot.data?.records ?? const <SchoolRecord>[])
              child.text('studentId'): child,
          };
          return Column(
            children: [
              for (final record in records)
                _parentAbsenceCard(record, childById[record.text('studentId')]),
            ],
          );
        },
      );

  Widget _parentAbsenceCard(SchoolRecord record, SchoolRecord? child) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final name = record.text('studentName');
    final initial = name.trim().isEmpty
        ? '?'
        : name.trim().substring(0, 1).toUpperCase();
    final status = _parentAbsenceStatus(record);
    final actions = recordActions(record);
    final studentCode = child?.text('studentCode') ?? '';
    final className = child?.text('className') ?? '';
    return Card(
      margin: const EdgeInsets.only(top: 6, bottom: 8),
      clipBehavior: Clip.antiAlias,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 12, 10, 6),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 17,
                  backgroundColor: colors.secondaryContainer,
                  foregroundColor: colors.onSecondaryContainer,
                  child: Text(
                    initial,
                    style: theme.textTheme.labelLarge?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                const SizedBox(width: 9),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        crossAxisAlignment: WrapCrossAlignment.center,
                        spacing: 6,
                        runSpacing: 3,
                        children: [
                          Text(
                            name.isEmpty ? 'Học sinh' : name,
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.w800,
                              height: 1.1,
                            ),
                          ),
                          if (className.isNotEmpty)
                            _parentSmallBadge(
                              className,
                              colors.secondaryContainer,
                              colors.onSecondaryContainer,
                            ),
                        ],
                      ),
                      if (studentCode.isNotEmpty)
                        Padding(
                          padding: const EdgeInsets.only(top: 3),
                          child: Text(
                            'Mã HS: $studentCode',
                            style: theme.textTheme.labelSmall?.copyWith(
                              color: colors.onSurfaceVariant,
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(width: 6),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    _parentSmallBadge(
                      status.label,
                      status.background,
                      status.foreground,
                    ),
                    if (actions.isNotEmpty)
                      SizedBox(
                        height: 26,
                        child: PopupMenuButton<int>(
                          padding: EdgeInsets.zero,
                          tooltip: 'Thao tác',
                          onSelected: (index) => actions[index].run(),
                          itemBuilder: (_) => [
                            for (final (index, action) in actions.indexed)
                              PopupMenuItem(
                                value: index,
                                child: Text(action.title),
                              ),
                          ],
                          icon: Icon(
                            Icons.more_horiz,
                            size: 18,
                            color: colors.onSurfaceVariant,
                          ),
                        ),
                      ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: _parentDateMetric(
                    'Từ ngày',
                    displayValue(record['fromDate']),
                  ),
                ),
                const SizedBox(width: 7),
                Expanded(
                  child: _parentDateMetric(
                    'Đến ngày',
                    displayValue(record['toDate']),
                  ),
                ),
                const SizedBox(width: 7),
                Expanded(
                  child: _parentDateMetric(
                    'Năm học',
                    record.text('schoolYear').isEmpty
                        ? '—'
                        : record.text('schoolYear'),
                  ),
                ),
              ],
            ),
            Theme(
              data: theme.copyWith(dividerColor: Colors.transparent),
              child: ExpansionTile(
                tilePadding: EdgeInsets.zero,
                childrenPadding: const EdgeInsets.fromLTRB(4, 0, 4, 10),
                leading: Icon(
                  Icons.info_outline,
                  size: 16,
                  color: colors.secondary,
                ),
                title: Text(
                  'Thông tin thêm',
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: colors.onSurfaceVariant,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                children: [
                  _parentDetailLine('Lý do', record.text('reason')),
                  _parentDetailLine(
                    'Thời gian đăng ký',
                    displayValue(record['reportedAt']),
                  ),
                  if (record['cancelledAt'] != null)
                    _parentDetailLine(
                      'Ngày hủy',
                      displayValue(record['cancelledAt']),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  ({String label, Color background, Color foreground}) _parentAbsenceStatus(
    SchoolRecord record,
  ) {
    final colors = Theme.of(context).colorScheme;
    if (record['cancelledAt'] != null) {
      return (
        label: 'Đã hủy',
        background: colors.errorContainer,
        foreground: colors.onErrorContainer,
      );
    }
    final today = schoolToday();
    if (record.text('fromDate').compareTo(today) > 0) {
      return (
        label: 'Chờ đến ngày',
        background: const Color(0xFF49371E),
        foreground: const Color(0xFFFFD38B),
      );
    }
    if (record.text('toDate').compareTo(today) < 0) {
      return (
        label: 'Hoàn thành',
        background: colors.surfaceContainerHigh,
        foreground: colors.onSurfaceVariant,
      );
    }
    return (
      label: 'Đang áp dụng',
      background: colors.tertiaryContainer,
      foreground: colors.onTertiaryContainer,
    );
  }

  Widget _parentSmallBadge(String label, Color background, Color foreground) =>
      Container(
        padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(20),
        ),
        child: Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
            color: foreground,
            fontSize: 9,
            fontWeight: FontWeight.w700,
          ),
        ),
      );

  Widget _parentDateMetric(String label, String value) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 7),
    decoration: BoxDecoration(
      color: Theme.of(context).colorScheme.surfaceContainerLow,
      borderRadius: BorderRadius.circular(11),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
            color: Theme.of(context).colorScheme.onSurfaceVariant,
            fontSize: 9,
          ),
        ),
        const SizedBox(height: 3),
        Text(
          value,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
            color: Theme.of(context).colorScheme.onSurface,
            fontWeight: FontWeight.w800,
            fontSize: 10,
          ),
        ),
      ],
    ),
  );

  Widget _parentDetailLine(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 104,
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value.isEmpty ? '—' : value,
            style: Theme.of(context).textTheme.labelSmall,
          ),
        ),
      ],
    ),
  );

  Widget _parentAbsencePagination(SchoolResult result, int pageSize) {
    final maxPage = (result.total / pageSize).ceil().clamp(1, 999999);
    final colors = Theme.of(context).colorScheme;
    return Center(
      child: Container(
        margin: const EdgeInsets.only(top: 8, bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 4),
        decoration: BoxDecoration(
          color: colors.surfaceContainer,
          borderRadius: BorderRadius.circular(24),
          border: Border.all(color: colors.outlineVariant),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            IconButton(
              visualDensity: VisualDensity.compact,
              tooltip: 'Trang trước',
              onPressed: page > 1 && !controller.loading
                  ? () {
                      page--;
                      load();
                    }
                  : null,
              icon: const Icon(Icons.chevron_left, size: 18),
            ),
            Text(
              'Trang $page / $maxPage · ${result.total} mục',
              style: Theme.of(
                context,
              ).textTheme.labelSmall?.copyWith(fontWeight: FontWeight.w700),
            ),
            IconButton(
              visualDensity: VisualDensity.compact,
              tooltip: 'Trang sau',
              onPressed: page * pageSize < result.total && !controller.loading
                  ? () {
                      page++;
                      load();
                    }
                  : null,
              icon: const Icon(Icons.chevron_right, size: 18),
            ),
          ],
        ),
      ),
    );
  }
}

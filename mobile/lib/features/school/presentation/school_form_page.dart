import 'package:flutter/material.dart';
import '../../../app/mealtrace_brand.dart';
import '../../../app/theme.dart';
import '../domain/school_models.dart';
import '../domain/school_commands.dart';
import 'school_controller.dart';
import 'school_forms.dart';
import 'school_widgets.dart';

class SchoolFormPage extends StatefulWidget {
  const SchoolFormPage({
    super.key,
    required this.form,
    required this.controller,
    this.contextValues = const {},
    this.onParentTabSelected,
  });
  final SchoolForm form;
  final SchoolController controller;
  final Map<String, Object?> contextValues;
  final ValueChanged<int>? onParentTabSelected;
  @override
  State<SchoolFormPage> createState() => _SchoolFormPageState();
}

class _SchoolFormPageState extends State<SchoolFormPage> {
  final key = GlobalKey<FormState>();
  final values = <String, Object?>{};
  final editors = <String, TextEditingController>{};
  final referenceLabels = <String, List<String>>{};
  bool busy = false;
  bool confirming = false;
  String? error;
  SchoolResult? preview;
  Map<String, Object?>? previewInput;
  SchoolResult? comparison;
  SchoolResult? notifications;
  List<SchoolRecord> children = [];
  bool prerequisitesReady = true;
  bool reviewed = false;
  String _selectedAbsencePreset = 'custom';
  @override
  void initState() {
    super.initState();
    for (final field in widget.form.fields) {
      var value = widget.contextValues[field.key] ?? field.initial;
      if (field.kind == FieldKind.date && value == null && field.required) {
        value = schoolToday();
      }
      if (field.kind == FieldKind.multiple ||
          field.kind == FieldKind.references) {
        value = (value as List<Object?>? ?? [])
            .map((x) => x.toString())
            .toList();
      }
      if (field.kind == FieldKind.toggle) value ??= false;
      values[field.key] = value;
      if (![
        FieldKind.multiple,
        FieldKind.references,
        FieldKind.reference,
        FieldKind.toggle,
        FieldKind.choice,
      ].contains(field.kind)) {
        editors[field.key] = TextEditingController(
          text: field.kind == FieldKind.lines
              ? (value as List<Object?>? ?? []).join('\n')
              : value?.toString() ?? '',
        );
      }
    }
    if ([
      SchoolOperation.reviewAmendment,
      SchoolOperation.linkParent,
      SchoolOperation.reportAbsence,
    ].contains(widget.form.operation)) {
      prerequisitesReady = false;
      loadPrerequisites();
    }
  }

  Future<void> loadPrerequisites() async {
    try {
      switch (widget.form.operation) {
        case SchoolOperation.reviewAmendment:
          comparison = await widget.controller.execute(
            SchoolOperation.amendmentDetail,
            context: widget.contextValues,
          );
        case SchoolOperation.linkParent:
          notifications = await widget.controller.execute(
            SchoolOperation.notificationSettings,
          );
          if (!notifications!.summary.flag('enabled')) {
            values['sendRegistrationNotification'] = false;
          }
        case SchoolOperation.reportAbsence:
          children = (await widget.controller.execute(
            SchoolOperation.children,
          )).records;
        default:
          break;
      }
      if (mounted) setState(() => prerequisitesReady = true);
    } on SchoolFailure catch (failure) {
      if (mounted) setState(() => error = failure.message);
    }
  }

  @override
  void dispose() {
    for (final editor in editors.values) {
      editor.clear();
      editor.dispose();
    }
    values.clear();
    preview = null;
    previewInput = null;
    super.dispose();
  }

  Future<void> submit() async {
    if (busy ||
        confirming ||
        !prerequisitesReady ||
        !key.currentState!.validate()) {
      return;
    }
    if (widget.form.operation == SchoolOperation.reviewAmendment && !reviewed) {
      setState(
        () => error = 'Đối chiếu bản trước / sau và xác nhận đã kiểm tra.',
      );
      return;
    }
    for (final field in widget.form.fields) {
      final editor = editors[field.key];
      if (editor == null) continue;
      final text = field.kind == FieldKind.password
          ? editor.text
          : editor.text.trim();
      values[field.key] = switch (field.kind) {
        FieldKind.integer => text.isEmpty ? null : int.tryParse(text),
        FieldKind.lines =>
          text
              .split('\n')
              .map((x) => x.trim())
              .where((x) => x.isNotEmpty)
              .toList(),
        _ => text.isEmpty && !field.required ? null : text,
      };
    }
    final validation = validateForm(widget.form, values, widget.contextValues);
    if (validation != null) {
      setState(() => error = validation);
      return;
    }
    if (widget.form.operation == SchoolOperation.reportAbsence) {
      final matches = children.where(
        (child) => child.text('studentId') == values['studentId'],
      );
      final child = matches.isEmpty ? null : matches.first;
      final message = validateAbsenceYear(values, child);
      if (message != null) {
        setState(() => error = message);
        return;
      }
    }
    if (widget.form.confirm) {
      confirming = true;
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(widget.form.title),
          content: Text(widget.form.help),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Quay lại'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Xác nhận'),
            ),
          ],
        ),
      );
      confirming = false;
      if (confirmed != true || !mounted) return;
    }
    setState(() {
      busy = true;
      error = null;
    });
    final request = formInput(widget.form, values, widget.contextValues);
    try {
      final result = await widget.controller.execute(
        widget.form.operation,
        context: {
          ...widget.contextValues,
          if (widget.form.operation == SchoolOperation.saveYear)
            'code': values['code'],
        },
        input: request,
      );
      if (!mounted) return;
      if (widget.form.operation == SchoolOperation.previewSessions) {
        setState(() {
          preview = result;
          previewInput = {
            ...request,
            'previewToken': result.summary['previewToken'],
          };
        });
        return;
      }
      if (widget.form.operation == SchoolOperation.changePassword) {
        await widget.controller.onUnauthorized();
        return;
      }
      if (result.summary.text('temporaryPassword').isNotEmpty ||
          result.summary.record('notification') != null) {
        await showDialog<void>(
          context: context,
          barrierDismissible: false,
          builder: (context) => AlertDialog(
            title: const Text('Kết quả đăng ký'),
            content: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  RecordDetails(record: result.summary),
                  if (result.summary.text('temporaryPassword').isNotEmpty) ...[
                    const SizedBox(height: 16),
                    const Text('Mật khẩu tạm — chỉ hiển thị một lần'),
                    SelectableText(
                      result.summary.text('temporaryPassword'),
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                      ),
                    ),
                    const Text(
                      'Chuyển riêng qua kênh an toàn. Đóng cửa sổ sẽ không xem lại được.',
                    ),
                  ],
                ],
              ),
            ),
            actions: [
              FilledButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Đã lưu, đóng'),
              ),
            ],
          ),
        );
      }
      if (mounted) Navigator.pop(context, true);
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

  Widget fieldWidget(SchoolField field, {BuildContext? themeContext}) {
    final uiContext = themeContext ?? context;
    final value = values[field.key];
    final disabled =
        busy ||
        !prerequisitesReady ||
        (widget.form.operation == SchoolOperation.createParentLink &&
            field.key == 'classId' &&
            (values['schoolYear'] == null || values['schoolYear'] == '')) ||
        (field.key == 'sendRegistrationNotification' &&
            notifications?.summary.flag('enabled') != true);
    if (widget.form.operation == SchoolOperation.reportAbsence &&
        field.key == 'studentId') {
      return _parentChildSelector(field, disabled, uiContext);
    }
    if (field.kind == FieldKind.toggle) {
      return SwitchListTile(
        title: Text(field.label),
        value: value == true,
        onChanged: disabled
            ? null
            : (value) => setState(() => values[field.key] = value),
      );
    }
    if (field.kind == FieldKind.choice) {
      return DropdownButtonFormField<String>(
        initialValue: value as String? ?? (field.required ? null : ''),
        isExpanded: true,
        decoration: InputDecoration(labelText: field.label),
        items: [
          if (!field.required && !field.options.containsKey(''))
            DropdownMenuItem(
              value: '',
              child: Text(field.key == 'gender' ? 'Chưa có' : 'Chưa chọn'),
            ),
          ...field.options.entries.map(
            (x) => DropdownMenuItem(value: x.key, child: Text(x.value)),
          ),
        ],
        onChanged: disabled
            ? null
            : (value) => setState(
                () => values[field.key] = value == '' ? null : value,
              ),
        validator: (value) =>
            field.required && value == null ? 'Vui lòng chọn.' : null,
      );
    }
    if (field.kind == FieldKind.multiple) {
      final selected = (value as List<String>?) ?? [];
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(field.label),
          Wrap(
            spacing: 8,
            children: field.options.entries
                .map(
                  (entry) => FilterChip(
                    label: Text(entry.value),
                    selected: selected.contains(entry.key),
                    onSelected: disabled
                        ? null
                        : (checked) => setState(() {
                            values[field.key] = checked
                                ? [...selected, entry.key]
                                : selected
                                      .where((x) => x != entry.key)
                                      .toList();
                          }),
                  ),
                )
                .toList(),
          ),
        ],
      );
    }
    if (field.kind == FieldKind.reference ||
        field.kind == FieldKind.references) {
      final multi = field.kind == FieldKind.references;
      final selected = multi
          ? (value as List<String>? ?? [])
          : value == null || value == ''
          ? <String>[]
          : <String>[value.toString()];
      return OutlinedButton.icon(
        onPressed: disabled
            ? null
            : () async {
                final picked = await Navigator.push<List<String>>(
                  uiContext,
                  MaterialPageRoute(
                    builder: (_) => ReferencePicker(
                      field: field,
                      controller: widget.controller,
                      contextValues: {...widget.contextValues, ...values},
                      selected: selected,
                      onSelected: (_, labels) {
                        if (mounted) referenceLabels[field.key] = labels;
                      },
                    ),
                  ),
                );
                if (picked != null && mounted) {
                  setState(() {
                    if (widget.form.operation ==
                            SchoolOperation.createParentLink &&
                        field.key == 'schoolYear') {
                      values['classId'] = null;
                      referenceLabels.remove('classId');
                    }
                    values[field.key] = multi
                        ? picked
                        : picked.isEmpty
                        ? null
                        : picked.first;
                  });
                }
              },
        icon: const Icon(Icons.list_alt),
        label: Align(
          alignment: Alignment.centerLeft,
          child: Text(
            '${field.label}${field.required ? ' *' : ''} · ${selected.isEmpty ? 'Chưa chọn' : referenceLabels[field.key]?.join(' · ') ?? '${selected.length} đã chọn'}',
          ),
        ),
      );
    }
    final editor = editors[field.key]!;
    return TextFormField(
      controller: editor,
      enabled: !disabled,
      obscureText: field.kind == FieldKind.password,
      maxLength: field.maxLength,
      maxLines: field.kind == FieldKind.lines || field.key == 'reason' ? 3 : 1,
      readOnly: field.kind == FieldKind.date,
      onTap:
          widget.form.operation == SchoolOperation.reportAbsence &&
              ['fromDate', 'toDate'].contains(field.key)
          ? () => setState(() => _selectedAbsencePreset = 'custom')
          : null,
      onChanged:
          widget.form.operation == SchoolOperation.reportAbsence &&
              field.key == 'reason'
          ? (_) => setState(() {})
          : null,
      keyboardType: field.kind == FieldKind.lines || field.key == 'reason'
          ? TextInputType.multiline
          : switch (field.kind) {
              FieldKind.integer => TextInputType.number,
              FieldKind.email => TextInputType.emailAddress,
              FieldKind.phone => TextInputType.phone,
              _ => TextInputType.text,
            },
      textInputAction: field.kind == FieldKind.lines || field.key == 'reason'
          ? TextInputAction.newline
          : TextInputAction.next,
      onTapOutside: (_) => FocusManager.instance.primaryFocus?.unfocus(),
      decoration: InputDecoration(
        labelText:
            widget.form.operation == SchoolOperation.reportAbsence &&
                ['fromDate', 'toDate'].contains(field.key)
            ? null
            : '${field.label}${field.required ? ' *' : ''}',
        counterText: '',
        suffixIcon: field.kind == FieldKind.date
            ? Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (!field.required && editor.text.isNotEmpty)
                    IconButton(
                      tooltip: 'Xóa ${field.label.toLowerCase()}',
                      onPressed: disabled
                          ? null
                          : () => setState(() {
                              editor.clear();
                              values[field.key] = null;
                            }),
                      icon: const Icon(Icons.clear),
                    ),
                  IconButton(
                    tooltip: 'Chọn ${field.label.toLowerCase()}',
                    onPressed: disabled
                        ? null
                        : () async {
                            final date = await showDatePicker(
                              context: uiContext,
                              initialDate:
                                  DateTime.tryParse(editor.text) ??
                                  DateTime.now(),
                              firstDate: DateTime(1900),
                              lastDate: field.key == 'dateOfBirth'
                                  ? DateTime.parse(schoolToday())
                                  : DateTime(2100),
                            );
                            if (date != null && mounted) {
                              setState(() {
                                editor.text =
                                    '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
                                if (widget.form.operation ==
                                    SchoolOperation.reportAbsence) {
                                  _selectedAbsencePreset = 'custom';
                                }
                              });
                            }
                          },
                    icon: const Icon(Icons.calendar_month),
                  ),
                ],
              )
            : null,
      ),
      validator: (text) {
        if (field.required && (text == null || text.trim().isEmpty)) {
          return 'Vui lòng nhập thông tin.';
        }
        if (text?.isNotEmpty == true &&
            field.kind == FieldKind.integer &&
            int.tryParse(text!) == null) {
          return 'Nhập số nguyên.';
        }
        if (text?.isNotEmpty == true &&
            field.kind == FieldKind.email &&
            !RegExp(r'^[^\s@]+@[^\s@]+\.[^\s@]+$').hasMatch(text!)) {
          return 'Email chưa hợp lệ.';
        }
        if (text?.isNotEmpty == true &&
            field.kind == FieldKind.phone &&
            !RegExp(r'^\+?[0-9 ()-]{9,30}$').hasMatch(text!)) {
          return 'SĐT chưa hợp lệ.';
        }
        return null;
      },
    );
  }

  Widget _parentChildSelector(
    SchoolField field,
    bool disabled,
    BuildContext uiContext,
  ) {
    SchoolRecord? selected;
    for (final child in children) {
      if (child.text('studentId') == values[field.key]) {
        selected = child;
        break;
      }
    }
    final colors = Theme.of(uiContext).colorScheme;
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: disabled
          ? null
          : () async {
              final picked = await Navigator.push<List<String>>(
                uiContext,
                MaterialPageRoute(
                  builder: (_) => ReferencePicker(
                    field: field,
                    controller: widget.controller,
                    contextValues: widget.contextValues,
                    selected: values[field.key] == null
                        ? []
                        : [values[field.key].toString()],
                    onSelected: (_, labels) {
                      referenceLabels[field.key] = labels;
                    },
                  ),
                ),
              );
              if (picked != null && mounted) {
                setState(() {
                  values[field.key] = picked.isEmpty ? null : picked.first;
                  _selectedAbsencePreset = 'custom';
                });
              }
            },
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: colors.surfaceContainer,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: colors.outlineVariant),
        ),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: colors.tertiaryContainer,
              foregroundColor: colors.onTertiaryContainer,
              child: Text(
                selected == null || selected.text('fullName').isEmpty
                    ? '·'
                    : selected.text('fullName').trim()[0].toUpperCase(),
                style: const TextStyle(fontWeight: FontWeight.w800),
              ),
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    field.label,
                    style: Theme.of(uiContext).textTheme.labelSmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    selected?.text('fullName') ?? 'Chọn trẻ',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(uiContext).textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  if (selected != null)
                    Text(
                      [
                        if (selected.text('className').isNotEmpty)
                          'Lớp ${selected.text('className')}',
                        if (selected.text('studentCode').isNotEmpty)
                          'Mã HS: ${selected.text('studentCode')}',
                      ].join(' · '),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(uiContext).textTheme.labelSmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                  if (!prerequisitesReady)
                    Text(
                      'Đang tải danh sách trẻ…',
                      style: Theme.of(uiContext).textTheme.labelSmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                ],
              ),
            ),
            Icon(Icons.expand_more, color: colors.onSurfaceVariant),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final parentAbsence =
        widget.form.operation == SchoolOperation.reportAbsence &&
        widget.controller.roles.contains('PARENT');
    if (!parentAbsence) return _buildGenericPage(context);
    final theme = Theme.of(context);
    return Theme(
      data: theme.brightness == Brightness.dark
          ? buildParentDarkTheme(theme)
          : theme,
      child: Builder(builder: _buildParentAbsencePage),
    );
  }

  Widget _buildParentAbsencePage(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final childField = widget.form.fields.firstWhere(
      (field) => field.key == 'studentId',
    );
    final fromField = widget.form.fields.firstWhere(
      (field) => field.key == 'fromDate',
    );
    final toField = widget.form.fields.firstWhere(
      (field) => field.key == 'toDate',
    );
    final reasonField = widget.form.fields.firstWhere(
      (field) => field.key == 'reason',
    );
    final from = DateTime.tryParse(editors['fromDate']?.text ?? '');
    final to = DateTime.tryParse(editors['toDate']?.text ?? '');
    final dayCount = from != null && to != null && !to.isBefore(from)
        ? to.difference(from).inDays + 1
        : null;

    return PopScope(
      canPop: !busy,
      child: Scaffold(
        appBar: AppBar(
          title: const MealTraceBrand(iconSize: 25, fontSize: 15),
          actions: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8),
              child: Stack(
                clipBehavior: Clip.none,
                children: [
                  Icon(Icons.notifications_none, color: colors.onSurface),
                  Positioned(
                    right: -1,
                    top: -1,
                    child: Container(
                      width: 7,
                      height: 7,
                      decoration: BoxDecoration(
                        color: colors.error,
                        shape: BoxShape.circle,
                        border: Border.all(color: colors.surface, width: 1),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.only(right: 14),
              child: CircleAvatar(
                radius: 15,
                backgroundColor: colors.secondaryContainer,
                foregroundColor: colors.onSecondaryContainer,
                child: const Icon(Icons.person_outline, size: 17),
              ),
            ),
          ],
        ),
        bottomNavigationBar: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (preview == null)
              AnimatedPadding(
                duration: const Duration(milliseconds: 150),
                padding: EdgeInsets.only(
                  bottom: MediaQuery.viewInsetsOf(context).bottom,
                ),
                child: Material(
                  color: colors.surface,
                  child: SafeArea(
                    top: false,
                    minimum: const EdgeInsets.fromLTRB(16, 10, 16, 8),
                    child: SizedBox(
                      width: double.infinity,
                      child: FilledButton.icon(
                        style: FilledButton.styleFrom(
                          backgroundColor: const Color(0xFF10B981),
                          foregroundColor: const Color(0xFF06251B),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                        onPressed: busy || confirming || !prerequisitesReady
                            ? null
                            : submit,
                        icon: const Icon(Icons.event_available_outlined),
                        label: Text(busy ? 'Đang xử lý…' : 'Đăng ký không ăn'),
                      ),
                    ),
                  ),
                ),
              ),
            if (widget.onParentTabSelected != null)
              _parentFormNavigation(context),
          ],
        ),
        body: SafeArea(
          child: SingleChildScrollView(
            keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
            padding: const EdgeInsets.fromLTRB(14, 12, 14, 20),
            child: Form(
              key: key,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Đăng ký không ăn',
                              style: theme.textTheme.titleLarge?.copyWith(
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            const SizedBox(height: 3),
                            Text(
                              'Tạo lịch nghỉ ăn cho trẻ đã liên kết.',
                              style: theme.textTheme.bodySmall?.copyWith(
                                color: colors.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Icon(Icons.info_outline, color: colors.secondary),
                      const SizedBox(width: 4),
                      Container(
                        width: 8,
                        height: 8,
                        margin: const EdgeInsets.only(top: 6),
                        decoration: BoxDecoration(
                          color: colors.tertiary,
                          shape: BoxShape.circle,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Card(
                    margin: EdgeInsets.zero,
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(Icons.info_outline, color: colors.secondary),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              'Thời gian đăng ký được tính cả ngày bắt đầu và kết thúc. Báo vắng không thay đổi ghi danh hoặc suất đã chốt.',
                              style: theme.textTheme.bodySmall?.copyWith(
                                height: 1.4,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Chọn nhanh khoảng thời gian',
                          style: theme.textTheme.labelLarge?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      Text(
                        'Tùy chỉnh lịch',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: colors.secondary,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 7),
                  Wrap(
                    spacing: 7,
                    runSpacing: 7,
                    children: [
                      _absencePeriodChip(context, 'week', '1 tuần'),
                      _absencePeriodChip(context, 'month', '1 tháng'),
                      _absencePeriodChip(context, 'year', 'Hết năm học'),
                      _absencePeriodChip(context, 'custom', 'Tùy chỉnh'),
                    ],
                  ),
                  const SizedBox(height: 13),
                  _parentFormSectionTitle(
                    context,
                    'Trẻ / Học sinh *',
                    'Dữ liệu đã liên kết',
                  ),
                  const SizedBox(height: 7),
                  fieldWidget(childField, themeContext: context),
                  if (children.isEmpty && prerequisitesReady)
                    Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text(
                        'Chưa có trẻ được liên kết với tài khoản này.',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: colors.error,
                        ),
                      ),
                    ),
                  const SizedBox(height: 13),
                  Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            _parentFormSectionTitle(context, 'Từ ngày *', null),
                            const SizedBox(height: 6),
                            fieldWidget(fromField, themeContext: context),
                          ],
                        ),
                      ),
                      const SizedBox(width: 9),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            _parentFormSectionTitle(
                              context,
                              'Đến ngày *',
                              null,
                            ),
                            const SizedBox(height: 6),
                            fieldWidget(toField, themeContext: context),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 11,
                      vertical: 10,
                    ),
                    decoration: BoxDecoration(
                      color: colors.tertiaryContainer,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Row(
                      children: [
                        Icon(
                          Icons.event_available_outlined,
                          size: 17,
                          color: colors.tertiary,
                        ),
                        const SizedBox(width: 7),
                        Expanded(
                          child: Text(
                            dayCount == null
                                ? 'Chọn khoảng ngày hợp lệ'
                                : 'Tổng thời gian: $dayCount ngày',
                            style: theme.textTheme.labelSmall?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: colors.onTertiaryContainer,
                            ),
                          ),
                        ),
                        Text(
                          'Cả ngày',
                          style: theme.textTheme.labelSmall?.copyWith(
                            color: colors.onTertiaryContainer,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 14),
                  _parentFormSectionTitle(
                    context,
                    'Lý do vắng ăn *',
                    'Bắt buộc',
                  ),
                  const SizedBox(height: 7),
                  fieldWidget(reasonField, themeContext: context),
                  Align(
                    alignment: Alignment.centerRight,
                    child: Text(
                      '${editors['reason']?.text.length ?? 0}/${reasonField.maxLength}',
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                  ),
                  if (!prerequisitesReady) ...[
                    const SizedBox(height: 8),
                    const LinearProgressIndicator(),
                    TextButton(
                      onPressed: loadPrerequisites,
                      child: const Text('Tải lại thông tin trẻ'),
                    ),
                  ],
                  if (error != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        error!,
                        style: TextStyle(color: colors.error),
                      ),
                    ),
                  const SizedBox(height: 8),
                  Text(
                    'Bằng việc đăng ký, phụ huynh xác nhận thông tin ngày nghỉ chính xác.',
                    textAlign: TextAlign.center,
                    style: theme.textTheme.labelSmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _absencePeriodChip(BuildContext context, String key, String label) {
    final colors = Theme.of(context).colorScheme;
    return ChoiceChip(
      label: Text(label),
      selected: _selectedAbsencePreset == key,
      onSelected:
          busy ||
              !prerequisitesReady ||
              (key != 'custom' && values['studentId'] == null)
          ? null
          : (_) {
              setState(() {
                _selectedAbsencePreset = key;
                if (key == 'custom') return;
                final selectedChild = children.where(
                  (child) => child.text('studentId') == values['studentId'],
                );
                if (selectedChild.isEmpty ||
                    selectedChild.first.text('yearEndDate').isEmpty) {
                  return;
                }
                editors['toDate']!.text = absencePeriodEnd(
                  editors['fromDate']!.text,
                  key,
                  selectedChild.first.text('yearEndDate'),
                );
              });
            },
      selectedColor: colors.secondaryContainer,
      backgroundColor: colors.surfaceContainer,
      labelStyle: Theme.of(context).textTheme.labelSmall?.copyWith(
        color: _selectedAbsencePreset == key
            ? colors.onSecondaryContainer
            : colors.onSurfaceVariant,
        fontWeight: FontWeight.w700,
      ),
      side: BorderSide(
        color: _selectedAbsencePreset == key
            ? colors.secondary
            : colors.outlineVariant,
      ),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
    );
  }

  Widget _parentFormSectionTitle(
    BuildContext context,
    String title,
    String? trailing,
  ) => Row(
    children: [
      Expanded(
        child: Text(
          title,
          style: Theme.of(
            context,
          ).textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w700),
        ),
      ),
      if (trailing != null)
        Text(
          trailing,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
            color: Theme.of(context).colorScheme.tertiary,
          ),
        ),
    ],
  );

  Widget _parentFormNavigation(BuildContext context) => NavigationBar(
    height: 64,
    selectedIndex: 1,
    onDestinationSelected: (index) {
      if (index == 1) return;
      final onParentTabSelected = widget.onParentTabSelected;
      Navigator.pop(context);
      WidgetsBinding.instance.addPostFrameCallback((_) {
        onParentTabSelected?.call(index);
      });
    },
    destinations: const [
      NavigationDestination(
        icon: Icon(Icons.home_outlined),
        selectedIcon: Icon(Icons.home),
        label: 'Trang chủ',
      ),
      NavigationDestination(
        icon: Icon(Icons.event_busy_outlined),
        selectedIcon: Icon(Icons.event_busy),
        label: 'Không ăn',
      ),
      NavigationDestination(icon: Icon(Icons.apps), label: 'Chức năng'),
      NavigationDestination(
        icon: Icon(Icons.person_outline),
        selectedIcon: Icon(Icons.person),
        label: 'Hồ sơ',
      ),
    ],
  );

  Widget _buildGenericPage(BuildContext context) => PopScope(
    canPop: !busy,
    child: Scaffold(
      appBar: AppBar(
        title: widget.controller.roles.contains('TEACHER')
            ? const MealTraceBrand(iconSize: 26, fontSize: 16)
            : Text(widget.form.title),
      ),
      bottomNavigationBar: preview == null
          ? AnimatedPadding(
              duration: const Duration(milliseconds: 150),
              padding: EdgeInsets.only(
                bottom: MediaQuery.viewInsetsOf(context).bottom,
              ),
              child: Material(
                color: Theme.of(context).colorScheme.surface,
                child: SafeArea(
                  top: false,
                  minimum: const EdgeInsets.fromLTRB(16, 12, 16, 12),
                  child: FilledButton(
                    onPressed: busy || confirming || !prerequisitesReady
                        ? null
                        : submit,
                    child: Text(busy ? 'Đang xử lý…' : widget.form.title),
                  ),
                ),
              ),
            )
          : null,
      body: SafeArea(
        child: SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: const EdgeInsets.all(20),
          child: Form(
            key: key,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (widget.controller.roles.contains('TEACHER')) ...[
                  Text(
                    widget.form.title,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 12),
                ],
                if (widget.form.operation == SchoolOperation.reviewParentLink ||
                    widget.form.operation == SchoolOperation.cancelParentLink ||
                    widget.form.operation == SchoolOperation.revokeParentLink ||
                    widget.form.operation ==
                        SchoolOperation.bulkReviewParentLinks)
                  RecordDetails(record: SchoolRecord(widget.contextValues)),
                Text(widget.form.help),
                const SizedBox(height: 20),
                if (!prerequisitesReady) ...[
                  if (error == null) const LinearProgressIndicator(),
                  if (error != null) Text(error!),
                  TextButton(
                    onPressed: loadPrerequisites,
                    child: const Text('Tải lại thông tin'),
                  ),
                ],
                if (widget.form.operation == SchoolOperation.reportAbsence)
                  Wrap(
                    spacing: 8,
                    children: [
                      for (final period in {
                        'week': '1 tuần',
                        'month': '1 tháng',
                        'year': 'Hết năm học',
                      }.entries)
                        OutlinedButton(
                          onPressed: busy || values['studentId'] == null
                              ? null
                              : () {
                                  final matching = children.where(
                                    (child) =>
                                        child.text('studentId') ==
                                        values['studentId'],
                                  );
                                  if (matching.isEmpty ||
                                      matching.first
                                          .text('yearEndDate')
                                          .isEmpty) {
                                    return;
                                  }
                                  setState(
                                    () => editors['toDate']!.text =
                                        absencePeriodEnd(
                                          editors['fromDate']!.text,
                                          period.key,
                                          matching.first.text('yearEndDate'),
                                        ),
                                  );
                                },
                          child: Text(period.value),
                        ),
                    ],
                  ),
                if (comparison != null) ...[
                  RecordDetails(record: comparison!.summary),
                  CheckboxListTile(
                    title: const Text('Tôi đã đối chiếu bản trước / sau'),
                    value: reviewed,
                    onChanged: busy
                        ? null
                        : (value) => setState(() => reviewed = value == true),
                  ),
                ],
                if (preview == null) ...[
                  for (final field in widget.form.fields)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 18),
                      child: fieldWidget(field),
                    ),
                  if (error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 16),
                      child: Text(
                        error!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                ] else ...[
                  RecordDetails(record: preview!.summary),
                  for (final record in preview!.records)
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: RecordDetails(record: record),
                      ),
                    ),
                  FilledButton(
                    onPressed: busy
                        ? null
                        : () async {
                            final completed = await Navigator.push<bool>(
                              context,
                              MaterialPageRoute(
                                builder: (_) => SchoolFormPage(
                                  form:
                                      schoolForms[SchoolOperation
                                          .generateSessions]!,
                                  controller: widget.controller,
                                  contextValues: {
                                    ...widget.contextValues,
                                    ...previewInput!,
                                  },
                                ),
                              ),
                            );
                            if (completed == true && context.mounted) {
                              Navigator.pop(context, true);
                            }
                          },
                    child: const Text('Xác nhận tạo phiên'),
                  ),
                  TextButton(
                    onPressed: () => setState(() {
                      preview = null;
                      previewInput = null;
                    }),
                    child: const Text('Chỉnh lại khoảng ngày'),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    ),
  );
}

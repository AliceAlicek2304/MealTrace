import 'package:flutter/material.dart';
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
  });
  final SchoolForm form;
  final SchoolController controller;
  final Map<String, Object?> contextValues;
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
      if (mounted) setState(() => error = failure.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Widget fieldWidget(SchoolField field) {
    final value = values[field.key];
    final disabled =
        busy ||
        !prerequisitesReady ||
        (widget.form.operation == SchoolOperation.createParentLink &&
            field.key == 'classId' &&
            (values['schoolYear'] == null || values['schoolYear'] == '')) ||
        (field.key == 'sendRegistrationNotification' &&
            notifications?.summary.flag('enabled') != true);
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
        initialValue: value as String?,
        isExpanded: true,
        decoration: InputDecoration(labelText: field.label),
        items: field.options.entries
            .map((x) => DropdownMenuItem(value: x.key, child: Text(x.value)))
            .toList(),
        onChanged: disabled
            ? null
            : (value) => setState(() => values[field.key] = value),
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
                  context,
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
        labelText: '${field.label}${field.required ? ' *' : ''}',
        counterText: '',
        suffixIcon: field.kind == FieldKind.date
            ? IconButton(
                tooltip: 'Chọn ngày',
                onPressed: disabled
                    ? null
                    : () async {
                        final date = await showDatePicker(
                          context: context,
                          initialDate:
                              DateTime.tryParse(editor.text) ?? DateTime.now(),
                          firstDate: DateTime(2000),
                          lastDate: DateTime(2100),
                        );
                        if (date != null && mounted) {
                          setState(
                            () => editor.text =
                                '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}',
                          );
                        }
                      },
                icon: const Icon(Icons.calendar_month),
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

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !busy,
    child: Scaffold(
      appBar: AppBar(title: Text(widget.form.title)),
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

import 'package:flutter/material.dart';

import '../../../core/presentation/meal_widgets.dart';
import '../domain/kitchen_commands.dart';
import '../domain/school_models.dart';
import 'kitchen_workflow_widgets.dart';
import 'school_controller.dart';

class KitchenSourcesPage extends StatelessWidget {
  const KitchenSourcesPage({super.key, required this.controller});
  final SchoolController controller;

  @override
  Widget build(BuildContext context) => DefaultTabController(
    length: 2,
    child: Scaffold(
      appBar: AppBar(
        title: const Text('Nguồn nguyên liệu'),
        bottom: const TabBar(
          tabs: [
            Tab(text: 'Phiếu nguồn'),
            Tab(text: 'Nhà cung cấp'),
          ],
        ),
      ),
      body: SafeArea(
        child: TabBarView(
          children: [
            _SourcesList(controller: controller, suppliers: false),
            _SourcesList(controller: controller, suppliers: true),
          ],
        ),
      ),
    ),
  );
}

class _SourcesList extends StatefulWidget {
  const _SourcesList({required this.controller, required this.suppliers});
  final SchoolController controller;
  final bool suppliers;

  @override
  State<_SourcesList> createState() => _SourcesListState();
}

class _SourcesListState extends State<_SourcesList> {
  final search = TextEditingController();
  late Future<SchoolResult> request;
  var page = 1;
  var from = '';
  var to = '';
  SchoolRecord? ingredient;

  SchoolOperation get operation => widget.suppliers
      ? SchoolOperation.suppliers
      : SchoolOperation.sourceReceipts;

  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    operation,
    input: {
      'page': page,
      'pageSize': 20,
      if (widget.suppliers) 'search': search.text.trim(),
      if (!widget.suppliers) ...{
        'from': from,
        'to': to,
        'ingredientId': ingredient?.text('id'),
      },
    },
  );

  void reload([int? value]) {
    if (from.isNotEmpty && to.isNotEmpty && from.compareTo(to) > 0) {
      workflowNotice(context, 'Đến ngày phải từ ngày bắt đầu trở đi.');
      return;
    }
    setState(() {
      page = value ?? page;
      request = load();
    });
  }

  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  Future<void> edit([SchoolRecord? record]) async {
    final saved = await openWorkflow<bool>(
      context,
      widget.suppliers
          ? _SupplierForm(controller: widget.controller, record: record)
          : _ReceiptForm(controller: widget.controller, record: record),
    );
    if (mounted && saved == true) {
      workflowNotice(context, 'Đã lưu phiên bản.');
      reload();
    }
  }

  Future<void> details(SchoolRecord item) async {
    final edited = await openWorkflow<bool>(
      context,
      _SourceDetails(
        controller: widget.controller,
        suppliers: widget.suppliers,
        item: item,
      ),
    );
    if (mounted && edited == true) reload();
  }

  @override
  Widget build(BuildContext context) => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      FilledButton.icon(
        onPressed: () => edit(),
        icon: const Icon(Icons.add),
        label: Text(
          widget.suppliers ? 'Thêm nhà cung cấp' : 'Thêm phiếu nguồn',
        ),
      ),
      if (widget.suppliers)
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: TextField(
            controller: search,
            textInputAction: TextInputAction.search,
            onSubmitted: (_) => reload(1),
            decoration: InputDecoration(
              labelText: 'Tìm nhà cung cấp',
              suffixIcon: IconButton(
                tooltip: 'Tìm',
                onPressed: () => reload(1),
                icon: const Icon(Icons.search),
              ),
            ),
          ),
        )
      else
        ExpansionTile(
          title: const Text('Bộ lọc'),
          children: [
            WorkflowDate(
              label: 'Từ ngày',
              value: from,
              emptyAllowed: true,
              onChanged: (date) => setState(() => from = date),
            ),
            WorkflowDate(
              label: 'Đến ngày',
              value: to,
              emptyAllowed: true,
              onChanged: (date) => setState(() => to = date),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Nguyên liệu'),
              subtitle: Text(ingredient?.title ?? 'Tất cả'),
              trailing: ingredient == null
                  ? const Icon(Icons.chevron_right)
                  : IconButton(
                      tooltip: 'Bỏ lọc nguyên liệu',
                      onPressed: () => setState(() => ingredient = null),
                      icon: const Icon(Icons.clear),
                    ),
              onTap: () async {
                final picked = await pickIngredient(context, widget.controller);
                if (mounted && picked != null) {
                  setState(() => ingredient = picked);
                }
              },
            ),
            TextButton(
              onPressed: () => reload(1),
              child: const Text('Áp dụng'),
            ),
          ],
        ),
      FutureBuilder<SchoolResult>(
        future: request,
        builder: (context, snapshot) => WorkflowResultView(
          snapshot: snapshot,
          onRetry: reload,
          builder: (result) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (result.records.isEmpty)
                const MealEmpty('Chưa có bản ghi phù hợp.'),
              for (final item in result.records)
                WorkflowCard(
                  title: widget.suppliers
                      ? item.title
                      : item.text('ingredientName'),
                  subtitle: widget.suppliers
                      ? 'Bản ${item.number('version')}'
                      : "${shortDate(item.text('referenceDate'))} · ${measured(item['quantity'], item.text('unit'))}",
                  onTap: () => details(item),
                ),
              WorkflowPager(result: result, page: page, onChange: reload),
            ],
          ),
        ),
      ),
    ],
  );
}

Future<SchoolRecord?> pickIngredient(
  BuildContext context,
  SchoolController controller,
) => openWorkflow<SchoolRecord>(
  context,
  WorkflowChooser(
    controller: controller,
    operation: SchoolOperation.ingredients,
    title: 'Chọn nguyên liệu',
    label: (item) => item.title,
    subtitle: (item) => item.text('unit'),
    searchable: true,
  ),
);

class _SourceDetails extends StatefulWidget {
  const _SourceDetails({
    required this.controller,
    required this.suppliers,
    required this.item,
  });
  final SchoolController controller;
  final bool suppliers;
  final SchoolRecord item;

  @override
  State<_SourceDetails> createState() => _SourceDetailsState();
}

class _SourceDetailsState extends State<_SourceDetails> {
  late Future<SchoolResult> request;
  @override
  void initState() {
    super.initState();
    request = load();
  }

  Future<SchoolResult> load() => widget.controller.execute(
    widget.suppliers
        ? SchoolOperation.supplierHistory
        : SchoolOperation.sourceHistory,
    context: {'id': widget.item.text('id')},
  );

  Future<void> edit(SchoolRecord current) async {
    final saved = await openWorkflow<bool>(
      context,
      widget.suppliers
          ? _SupplierForm(controller: widget.controller, record: current)
          : _ReceiptForm(controller: widget.controller, record: current),
    );
    if (mounted && saved == true) Navigator.of(context).pop(true);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Lịch sử phiên bản'),
      actions: [
        IconButton(
          tooltip: 'Tải lại',
          onPressed: () => setState(() {
            request = load();
          }),
          icon: const Icon(Icons.refresh),
        ),
      ],
    ),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          MealSectionTitle(
            widget.suppliers
                ? widget.item.title
                : widget.item.text('ingredientName'),
          ),
          FutureBuilder<SchoolResult>(
            future: request,
            builder: (context, snapshot) => WorkflowResultView(
              snapshot: snapshot,
              onRetry: () => setState(() {
                request = load();
              }),
              builder: (result) {
                if (result.records.isEmpty) {
                  return const MealEmpty('Chưa có lịch sử.');
                }
                final versions = [...result.records]
                  ..sort(
                    (a, b) =>
                        b.number('version').compareTo(a.number('version')),
                  );
                final latest = versions.first;
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    FilledButton(
                      onPressed: () => edit(
                        SchoolRecord({...widget.item.fields, ...latest.fields}),
                      ),
                      child: Text(
                        widget.suppliers
                            ? 'Sửa nhà cung cấp'
                            : 'Sửa phiếu nguồn',
                      ),
                    ),
                    for (final version in versions)
                      Card(
                        child: ExpansionTile(
                          title: Text(
                            'Bản ${version.number('version')} · ${widget.suppliers ? version.title : version.text('supplierName')}',
                          ),
                          subtitle: Text(
                            schoolTimestamp(version.text('recordedAt')),
                          ),
                          childrenPadding: const EdgeInsets.all(12),
                          expandedCrossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (widget.suppliers) ...[
                              Text(
                                'Liên hệ: ${optionalWorkflowText(version.text('contact')) ?? 'Chưa có'}',
                              ),
                              Text(
                                'Mã tham chiếu: ${optionalWorkflowText(version.text('reference')) ?? 'Chưa có'}',
                              ),
                            ] else ...[
                              Text(
                                'Ngày nhận: ${shortDate(version.text('referenceDate'))}',
                              ),
                              Text(
                                'Số lượng: ${measured(version['quantity'], version.text('unit'))}',
                              ),
                              Text(
                                'Phiếu giao: ${optionalWorkflowText(version.text('deliveryRef')) ?? 'Chưa có'}',
                              ),
                              Text(
                                'Mã lô: ${optionalWorkflowText(version.text('lotRef')) ?? 'Chưa có'}',
                              ),
                            ],
                            if (version.text('reason').isNotEmpty)
                              Text('Lý do: ${version.text('reason')}'),
                          ],
                        ),
                      ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    ),
  );
}

class _SupplierForm extends StatefulWidget {
  const _SupplierForm({required this.controller, this.record});
  final SchoolController controller;
  final SchoolRecord? record;

  @override
  State<_SupplierForm> createState() => _SupplierFormState();
}

class _SupplierFormState extends State<_SupplierForm> {
  late final fields = {
    for (final key in ['name', 'contact', 'reference', 'reason'])
      key: TextEditingController(
        text: key == 'reason' ? '' : widget.record?.text(key),
      ),
  };

  @override
  void dispose() {
    for (final field in fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.record;
    return WorkflowSavePage(
      title: item == null ? 'Thêm nhà cung cấp' : 'Sửa nhà cung cấp',
      save: () async {
        await widget.controller.execute(
          item == null
              ? SchoolOperation.createSupplier
              : SchoolOperation.reviseSupplier,
          context: {if (item != null) 'id': item.text('id')},
          input: {
            'name': fields['name']!.text.trim(),
            'contact': optionalWorkflowText(fields['contact']!.text),
            'reference': optionalWorkflowText(fields['reference']!.text),
            if (item != null) ...{
              'expectedVersion': item.number('version'),
              'reason': fields['reason']!.text.trim(),
            },
          },
        );
        return true;
      },
      children: [
        WorkflowField(
          label: 'Tên nhà cung cấp',
          controller: fields['name']!,
          maxLength: 200,
          requiredField: true,
        ),
        WorkflowField(
          label: 'Liên hệ',
          controller: fields['contact']!,
          maxLength: 500,
        ),
        WorkflowField(
          label: 'Mã tham chiếu',
          controller: fields['reference']!,
          maxLength: 100,
        ),
        if (item != null)
          WorkflowField(
            label: 'Lý do sửa',
            controller: fields['reason']!,
            maxLength: 500,
            requiredField: true,
          ),
      ],
    );
  }
}

class _ReceiptForm extends StatefulWidget {
  const _ReceiptForm({required this.controller, this.record});
  final SchoolController controller;
  final SchoolRecord? record;

  @override
  State<_ReceiptForm> createState() => _ReceiptFormState();
}

class _ReceiptFormState extends State<_ReceiptForm> {
  late String date = widget.record?.text('referenceDate') ?? schoolToday();
  late SchoolRecord? ingredient = widget.record == null
      ? null
      : SchoolRecord({
          'id': widget.record!.text('ingredientId'),
          'name': widget.record!.text('ingredientName'),
          'unit': widget.record!.text('unit'),
        });
  late SchoolRecord? supplier = widget.record == null
      ? null
      : SchoolRecord({
          'versionId': widget.record!.text('supplierVersionId'),
          'name': widget.record!.text('supplierName'),
        });
  late final fields = {
    for (final key in ['quantity', 'deliveryRef', 'lotRef', 'reason'])
      key: TextEditingController(
        text: key == 'reason' ? '' : widget.record?.text(key),
      ),
  };

  @override
  void dispose() {
    for (final field in fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  Future<bool> save() async {
    final quantity = kitchenNumber(fields['quantity']!.text);
    if (ingredient == null ||
        supplier == null ||
        quantity == null ||
        quantity <= 0) {
      throw const SchoolFailure(
        'Chọn nguyên liệu, nhà cung cấp và nhập số lượng lớn hơn 0.',
      );
    }
    final item = widget.record;
    await widget.controller.execute(
      item == null
          ? SchoolOperation.createSourceReceipt
          : SchoolOperation.reviseSourceReceipt,
      context: {if (item != null) 'id': item.text('id')},
      input: {
        if (item == null) 'ingredientId': ingredient!.text('id'),
        'supplierVersionId': supplier!.text('versionId'),
        'referenceDate': date,
        'quantity': quantity,
        'unit': ingredient!.text('unit'),
        'deliveryRef': optionalWorkflowText(fields['deliveryRef']!.text),
        'lotRef': optionalWorkflowText(fields['lotRef']!.text),
        if (item != null) ...{
          'expectedVersion': item.number('version'),
          'reason': fields['reason']!.text.trim(),
        },
      },
    );
    return true;
  }

  Future<void> chooseSupplier() async {
    final chosen = await openWorkflow<SchoolRecord>(
      context,
      WorkflowChooser(
        controller: widget.controller,
        operation: SchoolOperation.suppliers,
        title: 'Chọn nhà cung cấp',
        searchable: true,
        label: (item) => item.title,
        subtitle: (item) => 'Bản ${item.number('version')}',
      ),
    );
    if (mounted && chosen != null) setState(() => supplier = chosen);
  }

  @override
  Widget build(BuildContext context) => WorkflowSavePage(
    title: widget.record == null ? 'Thêm phiếu nguồn' : 'Sửa phiếu nguồn',
    save: save,
    children: [
      WorkflowCard(
        title: 'Nguyên liệu',
        subtitle: ingredient?.title ?? 'Chọn nguyên liệu',
        onTap: widget.record == null
            ? () async {
                final chosen = await pickIngredient(context, widget.controller);
                if (mounted && chosen != null) {
                  setState(() => ingredient = chosen);
                }
              }
            : null,
      ),
      WorkflowCard(
        title: 'Nhà cung cấp',
        subtitle: supplier?.title ?? 'Chọn nhà cung cấp',
        onTap: chooseSupplier,
      ),
      WorkflowDate(
        label: 'Ngày nhận',
        value: date,
        onChanged: (value) => setState(() => date = value),
      ),
      WorkflowField(
        label: ingredient == null
            ? 'Số lượng'
            : "Số lượng (${ingredient!.text('unit')})",
        controller: fields['quantity']!,
        maxLength: 22,
        requiredField: true,
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
      ),
      WorkflowField(
        label: 'Mã phiếu giao',
        controller: fields['deliveryRef']!,
        maxLength: 100,
      ),
      WorkflowField(
        label: 'Mã lô',
        controller: fields['lotRef']!,
        maxLength: 100,
      ),
      if (widget.record != null)
        WorkflowField(
          label: 'Lý do sửa',
          controller: fields['reason']!,
          maxLength: 500,
          requiredField: true,
        ),
    ],
  );
}

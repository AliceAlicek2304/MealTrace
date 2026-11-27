import 'package:flutter/material.dart';

import '../domain/school_models.dart';
import 'kitchen_workflow_widgets.dart';
import 'school_controller.dart';

class KitchenMenuEditor extends StatefulWidget {
  const KitchenMenuEditor({super.key, required this.controller, this.menu});
  final SchoolController controller;
  final SchoolRecord? menu;

  @override
  State<KitchenMenuEditor> createState() => _KitchenMenuEditorState();
}

class _KitchenMenuEditorState extends State<KitchenMenuEditor> {
  final reason = TextEditingController();
  late final dishes =
      (widget.menu?.record('latest')?.records('dishes') ??
              const <SchoolRecord>[])
          .map(
            (dish) => _DraftDish(
              id: dish.text('recipeVersionId'),
              name: dish.text('recipeName'),
              version: dish.number('recipeVersion'),
              initialPortions: dish.text('plannedPortions'),
            ),
          )
          .toList();
  String date = schoolToday();
  SchoolRecord? day;

  @override
  void dispose() {
    reason.dispose();
    for (final dish in dishes) {
      dish.portions.dispose();
    }
    super.dispose();
  }

  Future<void> chooseDay() async {
    final picked = await openWorkflow<SchoolRecord>(
      context,
      WorkflowChooser(
        controller: widget.controller,
        operation: SchoolOperation.meals,
        title: 'Chọn phiên ăn',
        filter: {'date': date},
        label: (item) => mealName(item.text('mealType')),
        subtitle: (item) => shortDate(item.text('date')),
        accept: (item) => !item.flag('isCancelled'),
      ),
    );
    if (mounted && picked != null) setState(() => day = picked);
  }

  Future<void> addDish() async {
    final recipe = await openWorkflow<SchoolRecord>(
      context,
      WorkflowChooser(
        controller: widget.controller,
        operation: SchoolOperation.recipeCatalog,
        title: 'Chọn công thức',
        searchable: true,
        label: (item) => item.title,
      ),
    );
    if (!mounted || recipe == null) return;
    final version = await openWorkflow<SchoolRecord>(
      context,
      WorkflowChooser(
        controller: widget.controller,
        operation: SchoolOperation.recipeVersions,
        title: 'Chọn phiên bản công thức',
        contextValues: {'id': recipe.text('recipeId')},
        paged: false,
        label: (item) => 'Bản ${item.number('version')}',
        subtitle: (item) =>
            item
                .records('lines')
                .any((line) => line.flag('legacyConversionUnverified'))
            ? 'Chưa xác minh quy đổi'
            : schoolTimestamp(item.text('recordedAt')),
        accept: (item) =>
            !dishes.any((dish) => dish.id == item.text('versionId')),
      ),
    );
    if (!mounted || version == null) return;
    setState(
      () => dishes.add(
        _DraftDish(
          id: version.text('versionId'),
          name: recipe.title,
          version: version.number('version'),
        ),
      ),
    );
  }

  Future<bool> save() async {
    final menu = widget.menu;
    if ((menu == null && day == null) || dishes.isEmpty) {
      throw const SchoolFailure('Chọn phiên ăn và ít nhất một món.');
    }
    final payload = <WorkflowInput>[];
    for (final dish in dishes) {
      final raw = dish.portions.text.trim();
      final portions = int.tryParse(raw);
      if (raw.isNotEmpty &&
          (portions == null || portions < 1 || portions > 100000)) {
        throw const SchoolFailure(
          'Số suất dự kiến phải là số nguyên từ 1 đến 100.000.',
        );
      }
      payload.add({'recipeVersionId': dish.id, 'plannedPortions': portions});
    }
    await widget.controller.execute(
      menu == null ? SchoolOperation.createMenu : SchoolOperation.reviseMenu,
      context: {if (menu != null) 'id': menu.text('id')},
      input: {
        if (menu == null) 'mealDayId': day!.text('id'),
        if (menu != null)
          'expectedVersion': menu.record('latest')!.number('version'),
        'reason': optionalWorkflowText(reason.text),
        'dishes': payload,
      },
    );
    return true;
  }

  @override
  Widget build(BuildContext context) => WorkflowSavePage(
    title: widget.menu == null ? 'Tạo thực đơn' : 'Tạo phiên bản thực đơn',
    buttonLabel: 'Lưu bản nháp',
    save: save,
    children: [
      if (widget.menu == null) ...[
        WorkflowDate(
          label: 'Ngày ăn',
          value: date,
          onChanged: (value) => setState(() {
            date = value;
            day = null;
          }),
        ),
        WorkflowCard(
          title: 'Phiên ăn',
          subtitle: day == null
              ? 'Chọn phiên ăn'
              : mealName(day!.text('mealType')),
          onTap: chooseDay,
        ),
      ],
      for (final dish in dishes)
        Card(
          key: ValueKey(dish.id),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(dish.name),
                  subtitle: Text('Công thức bản ${dish.version}'),
                  trailing: IconButton(
                    tooltip: 'Bỏ món ${dish.name}',
                    icon: const Icon(Icons.remove_circle_outline),
                    onPressed: () {
                      setState(() => dishes.remove(dish));
                      WidgetsBinding.instance.addPostFrameCallback(
                        (_) => dish.portions.dispose(),
                      );
                    },
                  ),
                ),
                WorkflowField(
                  label: 'Số suất dự kiến',
                  controller: dish.portions,
                  maxLength: 6,
                  keyboardType: TextInputType.number,
                ),
              ],
            ),
          ),
        ),
      if (dishes.length < 30)
        OutlinedButton.icon(
          onPressed: addDish,
          icon: const Icon(Icons.add),
          label: const Text('Thêm món'),
        ),
      WorkflowField(
        label: widget.menu == null ? 'Ghi chú' : 'Lý do tạo bản mới',
        controller: reason,
        maxLength: 500,
        requiredField: widget.menu != null,
      ),
    ],
  );
}

class _DraftDish {
  _DraftDish({
    required this.id,
    required this.name,
    required this.version,
    String initialPortions = '',
  }) : portions = TextEditingController(text: initialPortions);
  final String id;
  final String name;
  final int version;
  final TextEditingController portions;
}

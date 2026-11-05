import 'package:flutter/material.dart';
import '../domain/kitchen_commands.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'school_forms.dart';
import 'school_widgets.dart';
import '../../../core/presentation/meal_widgets.dart';

class KitchenEditorPage extends StatefulWidget {
  const KitchenEditorPage({
    super.key,
    required this.operation,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
    this.record,
    this.expectedVersion,
  });
  final SchoolOperation operation;
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  final SchoolRecord? record;
  final int? expectedVersion;
  @override
  State<KitchenEditorPage> createState() => _KitchenEditorPageState();
}

class _KitchenEditorPageState extends State<KitchenEditorPage> {
  final form = GlobalKey<FormState>();
  final name = TextEditingController();
  final basis = TextEditingController(text: '1');
  final edible = TextEditingController(text: '100');
  final energy = TextEditingController();
  final protein = TextEditingController();
  final price = TextEditingController();
  final lines = <({SchoolRecord ingredient, TextEditingController quantity})>[];
  late final SchoolController controller;
  DateTime validFrom = DateTime.now().toUtc().add(const Duration(hours: 7));
  String unit = 'g';
  bool busy = false, confirming = false;
  bool get ingredient => widget.operation == SchoolOperation.createIngredient;
  bool get nutrient => widget.operation == SchoolOperation.ingredientVersion;
  String get title => ingredient
      ? 'Thêm nguyên liệu'
      : nutrient
      ? 'Cập nhật dinh dưỡng'
      : widget.operation == SchoolOperation.createRecipe
      ? 'Tạo công thức'
      : 'Thêm bản công thức';
  @override
  void initState() {
    super.initState();
    controller = SchoolController(
      widget.repository,
      widget.roles,
      widget.onUnauthorized,
    );
    final nutrition = widget.record?.record('nutrition');
    if (nutrition != null) {
      edible.text = nutrition.text('ediblePercent');
      energy.text = nutrition.text('energyKcalPer100G');
      protein.text = nutrition.text('proteinGPer100G');
    }
  }

  @override
  void dispose() {
    for (final editor in [
      name,
      basis,
      edible,
      energy,
      protein,
      price,
      ...lines.map((line) => line.quantity),
    ]) {
      editor.dispose();
    }
    controller.dispose();
    super.dispose();
  }

  Future<void> chooseIngredient() async {
    SchoolRecord? picked;
    await Navigator.push<List<String>>(
      context,
      MaterialPageRoute(
        builder: (_) => ReferencePicker(
          field: const SchoolField(
            'ingredientId',
            'Nguyên liệu',
            kind: FieldKind.reference,
            reference: SchoolOperation.ingredients,
          ),
          controller: controller,
          contextValues: const {},
          selected: const [],
          onSelectRecord: (record) => picked = record,
        ),
      ),
    );
    if (!mounted || picked == null) return;
    if (lines.any((line) => line.ingredient.text('id') == picked!.text('id'))) {
      message('Nguyên liệu này đã có trong công thức.');
      return;
    }
    if (lines.length >= 200) {
      message('Mỗi công thức tối đa 200 nguyên liệu.');
      return;
    }
    setState(
      () => lines.add((ingredient: picked!, quantity: TextEditingController())),
    );
  }

  void message(String text) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
  Future<void> pickTime() async {
    final date = await showDatePicker(
      context: context,
      initialDate: validFrom,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (!mounted || date == null) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(validFrom),
    );
    if (!mounted || time == null) return;
    // School input is always UTC+7, independent of the device timezone.
    setState(
      () => validFrom = DateTime(
        date.year,
        date.month,
        date.day,
        time.hour,
        time.minute,
      ),
    );
  }

  Future<void> save() async {
    if (busy || confirming || !form.currentState!.validate()) return;
    final input = ingredient
        ? <String, Object?>{'name': name.text.trim(), 'unit': unit}
        : nutrient
        ? <String, Object?>{
            'validFrom': DateTime.utc(
              validFrom.year,
              validFrom.month,
              validFrom.day,
              validFrom.hour,
              validFrom.minute,
            ).subtract(const Duration(hours: 7)).toIso8601String(),
            'ediblePercent': kitchenNumber(edible.text),
            'energyKcal': kitchenNumber(energy.text),
            'proteinG': kitchenNumber(protein.text),
            'pricePerKg': price.text.trim().isEmpty
                ? null
                : kitchenNumber(price.text),
          }
        : <String, Object?>{
            if (widget.operation == SchoolOperation.createRecipe)
              'name': name.text.trim()
            else
              'expectedVersion': widget.expectedVersion,
            'servingBasis': int.tryParse(basis.text.trim()),
            'lines': lines
                .map((line) => recipeLine(line.ingredient, line.quantity.text))
                .toList(),
          };
    final error = nutrient
        ? validateIngredientVersion(input)
        : ingredient
        ? null
        : validateRecipe(
            input,
            creating: widget.operation == SchoolOperation.createRecipe,
          );
    if (error != null) {
      message(error);
      return;
    }
    confirming = true;
    final yes = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: Text(
          nutrient
              ? 'Lưu bản dinh dưỡng mới cho ${widget.record!.title}? Bản cũ được giữ trong lịch sử.'
              : widget.operation == SchoolOperation.recipeVersion
              ? 'Lưu toàn bộ thành phần đã nhập thành bản mới, dựa trên bản ${widget.expectedVersion}?'
              : 'Lưu thông tin đã kiểm tra?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Quay lại'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Lưu'),
          ),
        ],
      ),
    );
    confirming = false;
    if (!mounted || yes != true) return;
    FocusScope.of(context).unfocus();
    setState(() => busy = true);
    try {
      final response = await controller.execute(
        widget.operation,
        context: {'id': widget.record?['id']},
        input: input,
      );
      if (mounted) Navigator.pop(context, response.summary);
    } on SchoolFailure catch (failure) {
      if (mounted) message(failure.message);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Widget field(
    TextEditingController editor,
    String label, {
    bool number = false,
    bool optional = false,
    int max = 30,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: TextFormField(
      controller: editor,
      enabled: !busy,
      maxLength: max,
      keyboardType: number
          ? const TextInputType.numberWithOptions(decimal: true)
          : TextInputType.text,
      decoration: InputDecoration(labelText: label),
      validator: (text) {
        if (text == null || text.trim().isEmpty) {
          return optional ? null : 'Vui lòng nhập $label.';
        }
        if (number && kitchenNumber(text) == null) return 'Nhập một số hợp lệ.';
        return null;
      },
    ),
  );

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !busy,
    child: Scaffold(
      appBar: AppBar(title: Text(title)),
      bottomNavigationBar: AnimatedPadding(
        duration: const Duration(milliseconds: 150),
        padding: EdgeInsets.only(
          bottom: MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SafeArea(
          top: false,
          minimum: const EdgeInsets.all(16),
          child: FilledButton(
            onPressed: busy ? null : save,
            child: Text(busy ? 'Đang lưu…' : 'Lưu'),
          ),
        ),
      ),
      body: SafeArea(
        child: Form(
          key: form,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              MealSectionTitle(
                ingredient
                    ? 'Thông tin nguyên liệu'
                    : nutrient
                    ? 'Giá trị dinh dưỡng'
                    : 'Thành phần công thức',
                subtitle: ingredient
                    ? 'Đặt tên dễ nhận biết và chọn đúng đơn vị.'
                    : nutrient
                    ? 'Lưu bản cập nhật để đối chiếu theo thời gian.'
                    : 'Định lượng rõ ràng cho từng bữa ăn.',
              ),
              if (ingredient ||
                  widget.operation == SchoolOperation.createRecipe)
                field(
                  name,
                  ingredient ? 'Tên nguyên liệu' : 'Tên công thức',
                  max: 200,
                ),
              if (ingredient)
                DropdownButtonFormField<String>(
                  initialValue: unit,
                  decoration: const InputDecoration(labelText: 'Đơn vị'),
                  items: const [
                    DropdownMenuItem(value: 'g', child: Text('Gam (g)')),
                    DropdownMenuItem(value: 'ml', child: Text('Mililít (ml)')),
                  ],
                  onChanged: busy
                      ? null
                      : (value) => setState(() => unit = value!),
                ),
              if (nutrient) ...[
                Text(
                  widget.record!.title,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 12),
                  child: Text(
                    'Giá trị dinh dưỡng tính trên 100 g hoặc 100 ml theo đơn vị nguyên liệu.',
                  ),
                ),
                OutlinedButton.icon(
                  onPressed: busy ? null : pickTime,
                  icon: const Icon(Icons.schedule),
                  label: Text(
                    'Áp dụng (UTC+7): ${validFrom.day}/${validFrom.month}/${validFrom.year} ${validFrom.hour.toString().padLeft(2, '0')}:${validFrom.minute.toString().padLeft(2, '0')}',
                  ),
                ),
                const SizedBox(height: 16),
                field(edible, 'Tỷ lệ ăn được (%)', number: true),
                field(energy, 'Năng lượng (kcal / 100)', number: true),
                field(protein, 'Chất đạm (g / 100)', number: true),
                field(price, 'Giá mỗi kg', number: true, optional: true),
              ],
              if (!ingredient && !nutrient) ...[
                if (widget.expectedVersion != null)
                  Text(
                    'Đối chiếu bản ${widget.expectedVersion}. Nhập đầy đủ thành phần cho bản mới.',
                  ),
                field(basis, 'Số suất của công thức', number: true),
                const Text(
                  'Khối lượng bên dưới là tổng cho toàn bộ số suất, không phải mỗi suất.',
                ),
                const SizedBox(height: 12),
                for (final line in lines)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            line.ingredient.title,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                          field(
                            line.quantity,
                            'Khối lượng (${line.ingredient.text('unit')})',
                            number: true,
                          ),
                          TextButton.icon(
                            onPressed: busy
                                ? null
                                : () => setState(() {
                                    lines.remove(line);
                                    line.quantity.dispose();
                                  }),
                            icon: const Icon(Icons.remove_circle_outline),
                            label: const Text('Bỏ nguyên liệu'),
                          ),
                        ],
                      ),
                    ),
                  ),
                OutlinedButton.icon(
                  onPressed: busy ? null : chooseIngredient,
                  icon: const Icon(Icons.add),
                  label: const Text('Chọn nguyên liệu'),
                ),
              ],
            ],
          ),
        ),
      ),
    ),
  );
}

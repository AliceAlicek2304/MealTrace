import 'dart:async';
import 'package:flutter/material.dart';
import '../domain/school_models.dart';
import 'school_controller.dart';
import 'kitchen_editor_page.dart';
import '../../../core/presentation/meal_widgets.dart';

class KitchenCatalogPage extends StatefulWidget {
  const KitchenCatalogPage({
    super.key,
    required this.repository,
    required this.roles,
    required this.onUnauthorized,
  });
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  @override
  State<KitchenCatalogPage> createState() => _KitchenCatalogPageState();
}

class _KitchenCatalogPageState extends State<KitchenCatalogPage> {
  late final SchoolController controller;
  final search = TextEditingController();
  Timer? debounce;
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

  Future<void> load() async {
    debounce?.cancel();
    await controller.load(SchoolOperation.ingredients, const {}, {
      'page': page,
      'pageSize': 20,
      'search': search.text.trim(),
    });
  }

  @override
  void dispose() {
    debounce?.cancel();
    search.dispose();
    controller.dispose();
    super.dispose();
  }

  Future<void> edit(SchoolOperation op, [SchoolRecord? record]) async {
    final saved = await Navigator.push<SchoolRecord>(
      context,
      MaterialPageRoute(
        builder: (_) => KitchenEditorPage(
          operation: op,
          record: record,
          repository: widget.repository,
          roles: widget.roles,
          onUnauthorized: widget.onUnauthorized,
        ),
      ),
    );
    if (!mounted || saved == null) return;
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Đã lưu nguyên liệu.')));
    await load();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final result = controller.result;
      return Scaffold(
        appBar: AppBar(
          title: const Text('Nguyên liệu'),
          actions: [
            IconButton(
              onPressed: controller.loading ? null : load,
              tooltip: 'Tải lại',
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        floatingActionButton:
            canExecute(SchoolOperation.createIngredient, widget.roles)
            ? FloatingActionButton.extended(
                onPressed: () => edit(SchoolOperation.createIngredient),
                icon: const Icon(Icons.add),
                label: const Text('Thêm nguyên liệu'),
              )
            : null,
        body: SafeArea(
          child: RefreshIndicator(
            onRefresh: load,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
              children: [
                const MealSectionTitle(
                  'Kho nguyên liệu',
                  subtitle: 'Theo dõi định lượng và giá trị dinh dưỡng',
                ),
                TextField(
                  controller: search,
                  decoration: const InputDecoration(
                    labelText: 'Tìm nguyên liệu',
                    prefixIcon: Icon(Icons.search),
                  ),
                  onChanged: (_) {
                    debounce?.cancel();
                    debounce = Timer(const Duration(milliseconds: 300), () {
                      page = 1;
                      load();
                    });
                  },
                ),
                const SizedBox(height: 16),
                if (controller.loading) const LinearProgressIndicator(),
                if (controller.error != null) ...[
                  Text(controller.error!),
                  TextButton(onPressed: load, child: const Text('Thử lại')),
                ],
                if (!controller.loading &&
                    result != null &&
                    result.records.isEmpty)
                  const MealEmpty(
                    'Chưa có nguyên liệu phù hợp.',
                    icon: Icons.kitchen_outlined,
                  ),
                for (final item in result?.records ?? <SchoolRecord>[])
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Row(
                            children: [
                              const MealIcon(
                                Icons.eco_outlined,
                                background: MealColors.mint,
                                color: MealColors.green,
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      item.title,
                                      style: Theme.of(
                                        context,
                                      ).textTheme.titleMedium,
                                    ),
                                    Text(
                                      'Đơn vị: ${item.text('unit')}',
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodySmall
                                          ?.copyWith(color: MealColors.muted),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 16),
                          if (item.record('nutrition')
                              case final nutrition?) ...[
                            Text(
                              'Bản ${nutrition.number('version')} · Tỷ lệ ăn được ${nutrition.text('ediblePercent')}%',
                            ),
                            Text(
                              '${nutrition.text('energyKcalPer100G')} kcal · ${nutrition.text('proteinGPer100G')} g đạm / 100 ${item.text('unit')}',
                            ),
                          ] else
                            const Text('Chưa có dữ liệu dinh dưỡng'),
                          const SizedBox(height: 16),
                          OutlinedButton(
                            onPressed: () =>
                                edit(SchoolOperation.ingredientVersion, item),
                            child: const Text('Cập nhật dinh dưỡng'),
                          ),
                        ],
                      ),
                    ),
                  ),
                if (result != null)
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
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
                      Expanded(
                        child: Text(
                          'Trang $page · ${result.total} nguyên liệu',
                          textAlign: TextAlign.center,
                        ),
                      ),
                      IconButton(
                        tooltip: 'Trang sau',
                        onPressed:
                            page * 20 < result.total && !controller.loading
                            ? () {
                                page++;
                                load();
                              }
                            : null,
                        icon: const Icon(Icons.chevron_right),
                      ),
                    ],
                  ),
              ],
            ),
          ),
        ),
      );
    },
  );
}

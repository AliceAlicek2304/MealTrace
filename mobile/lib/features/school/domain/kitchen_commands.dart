import 'school_models.dart';

bool isRecordId(String text) =>
    RegExp(
      r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
    ).hasMatch(text) &&
    text != '00000000-0000-0000-0000-000000000000';

double? kitchenNumber(String text) {
  final value = double.tryParse(text.trim().replaceAll(',', '.'));
  return value != null && value.isFinite ? value : null;
}

String? validateIngredientVersion(Map<String, Object?> values) {
  for (final entry in {
    'ediblePercent': 100,
    'energyKcal': 900,
    'proteinG': 100,
  }.entries) {
    final value = values[entry.key];
    if (value is! num || !value.isFinite || value < 0 || value > entry.value) {
      return 'Tỷ lệ ăn được 0–100%, năng lượng 0–900 kcal, đạm 0–100 g.';
    }
  }
  final price = values['pricePerKg'];
  if (price != null &&
      (price is! num || !price.isFinite || price < 0 || price > 999999999)) {
    return 'Giá phải từ 0 đến 999.999.999.';
  }
  if (DateTime.tryParse(values['validFrom']?.toString() ?? '') == null) {
    return 'Chọn thời điểm bắt đầu áp dụng.';
  }
  return null;
}

String? validateRecipe(Map<String, Object?> values, {required bool creating}) {
  final name = values['name']?.toString().trim() ?? '';
  if (creating && (name.isEmpty || name.length > 200)) {
    return 'Tên công thức cần từ 1 đến 200 ký tự.';
  }
  final basis = values['servingBasis'];
  if (basis is! int || basis < 1 || basis > 2147483647) {
    return 'Số suất của công thức phải là số nguyên dương.';
  }
  final version = values['expectedVersion'];
  if (!creating && (version is! int || version < 1 || version > 2147483647)) {
    return 'Chọn phiên bản đã đối chiếu.';
  }
  final lines = values['lines'];
  if (lines is! List || lines.isEmpty || lines.length > 200) {
    return 'Chọn từ 1 đến 200 nguyên liệu.';
  }
  final ids = <String>{};
  for (final item in lines) {
    if (item is! Map<String, Object?>) return 'Dòng nguyên liệu chưa hợp lệ.';
    final id = item['ingredientId']?.toString() ?? '';
    final amount = item['quantity'];
    if (!isRecordId(id) || !ids.add(id)) {
      return 'Chọn các nguyên liệu khác nhau.';
    }
    if (amount is! num ||
        !amount.isFinite ||
        amount <= 0 ||
        amount / basis < 0.0005) {
      return 'Khối lượng phải lớn hơn 0 và đủ ít nhất 0,001 cho mỗi suất.';
    }
    if (!['g', 'ml'].contains(item['unit'])) {
      return 'Đơn vị nguyên liệu chưa hợp lệ.';
    }
  }
  return null;
}

Map<String, Object?> recipeLine(SchoolRecord ingredient, String quantity) => {
  'ingredientId': ingredient['id'],
  'quantity': kitchenNumber(quantity),
  'unit': ingredient['unit'],
};

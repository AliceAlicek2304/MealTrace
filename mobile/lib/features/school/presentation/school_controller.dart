import 'package:flutter/foundation.dart';
import '../domain/school_models.dart';

class SchoolController extends ChangeNotifier {
  SchoolController(this.repository, List<String> roles, this.onUnauthorized)
    : roles = List.unmodifiable(roles);
  final SchoolRepository repository;
  final List<String> roles;
  final Future<void> Function() onUnauthorized;
  SchoolResult? _result;
  String? _error;
  bool _loading = false;
  SchoolResult? get result => _result;
  String? get error => _error;
  bool get loading => _loading;
  bool _closed = false;
  int _generation = 0;

  Future<void> load(
    SchoolOperation operation,
    Map<String, Object?> context,
    Map<String, Object?> filters,
  ) async {
    final generation = ++_generation;
    if (_closed) return;
    _loading = true;
    _error = null;
    _result = null;
    notifyListeners();
    try {
      final response = await execute(
        operation,
        context: context,
        input: filters,
      );
      if (!_closed && generation == _generation) _result = response;
    } on SchoolFailure catch (failure) {
      if (!_closed && generation == _generation) _error = failure.message;
    } finally {
      if (!_closed && generation == _generation) {
        _loading = false;
        notifyListeners();
      }
    }
  }

  Future<SchoolResult> execute(
    SchoolOperation operation, {
    Map<String, Object?> context = const {},
    Map<String, Object?> input = const {},
  }) async {
    if (!canExecute(operation, roles)) {
      throw const SchoolFailure(
        'Bạn không có quyền thực hiện thao tác này.',
        status: 403,
      );
    }
    try {
      return await repository.execute(
        operation,
        context: context,
        input: input,
      );
    } on SchoolFailure catch (failure) {
      if (failure.status == 401 && !_closed) await onUnauthorized();
      rethrow;
    }
  }

  @override
  void dispose() {
    _closed = true;
    _generation++;
    super.dispose();
  }
}

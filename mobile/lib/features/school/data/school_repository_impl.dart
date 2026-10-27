import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import '../domain/school_models.dart';

class ApiRoute {
  const ApiRoute(this.method, this.path);
  final String method;
  final String path;
}

const schoolRoutes = <SchoolOperation, ApiRoute>{
  SchoolOperation.ingredients: ApiRoute('GET', '/kitchen/ingredients'),
  SchoolOperation.createIngredient: ApiRoute('POST', '/kitchen/ingredients'),
  SchoolOperation.ingredientVersion: ApiRoute(
    'POST',
    '/kitchen/ingredients/{id}/ingredient-versions',
  ),
  SchoolOperation.createRecipe: ApiRoute('POST', '/kitchen/recipes'),
  SchoolOperation.recipeVersion: ApiRoute(
    'POST',
    '/kitchen/recipes/{id}/versions',
  ),
  SchoolOperation.nutrition: ApiRoute(
    'GET',
    '/kitchen/recipes/{id}/versions/{v}/nutrition',
  ),
  SchoolOperation.reportLineage: ApiRoute('GET', '/reports/{id}/lineage'),
  SchoolOperation.userDetail: ApiRoute('GET', '/admin/users/{id}'),
  SchoolOperation.sendTestNotification: ApiRoute(
    'POST',
    '/admin/notifications/test',
  ),
  SchoolOperation.kitchenSettlements: ApiRoute(
    'GET',
    '/kitchen/settlements/search',
  ),
  SchoolOperation.kitchenDiff: ApiRoute(
    'GET',
    '/kitchen/settlements/{id}/diff',
  ),
  SchoolOperation.kitchenHistory: ApiRoute(
    'GET',
    '/kitchen/settlements/{id}/acknowledgements',
  ),
  SchoolOperation.acknowledgeKitchen: ApiRoute(
    'POST',
    '/kitchen/settlements/{id}/acknowledge',
  ),
  SchoolOperation.studentImportHistory: ApiRoute(
    'GET',
    '/admin/students/import/history',
  ),
  SchoolOperation.studentImportBatch: ApiRoute(
    'GET',
    '/admin/students/import/history/{id}',
  ),
  SchoolOperation.previewStudentImport: ApiRoute(
    'POST',
    '/admin/students/import/preview',
  ),
  SchoolOperation.confirmStudentImport: ApiRoute(
    'POST',
    '/admin/students/import/confirm',
  ),
  SchoolOperation.parentLinkClasses: ApiRoute(
    'GET',
    '/parent/link-requests/classes',
  ),
  SchoolOperation.reviewLinkClasses: ApiRoute(
    'GET',
    '/student-link-requests/classes',
  ),
  SchoolOperation.bulkReviewParentLinks: ApiRoute(
    'POST',
    '/student-link-requests/bulk-review',
  ),
  SchoolOperation.revokeParentLink: ApiRoute(
    'POST',
    '/student-link-requests/{id}/revoke',
  ),
  SchoolOperation.parentLinks: ApiRoute('GET', '/parent/link-requests'),
  SchoolOperation.createParentLink: ApiRoute('POST', '/parent/link-requests'),
  SchoolOperation.cancelParentLink: ApiRoute(
    'POST',
    '/parent/link-requests/{id}/cancel',
  ),
  SchoolOperation.reviewableParentLinks: ApiRoute(
    'GET',
    '/student-link-requests',
  ),
  SchoolOperation.reviewParentLink: ApiRoute(
    'POST',
    '/student-link-requests/{id}/review',
  ),
  SchoolOperation.users: ApiRoute('GET', '/admin/users'),
  SchoolOperation.scopes: ApiRoute('GET', '/admin/scope-options'),
  SchoolOperation.createUser: ApiRoute('POST', '/admin/users'),
  SchoolOperation.updateUser: ApiRoute('PUT', '/admin/users/{id}'),
  SchoolOperation.resetPassword: ApiRoute(
    'POST',
    '/admin/users/{id}/reset-password',
  ),
  SchoolOperation.changePassword: ApiRoute('POST', '/auth/change-password'),
  SchoolOperation.classes: ApiRoute('GET', '/admin/classes'),
  SchoolOperation.assignedClasses: ApiRoute('GET', '/classes'),
  SchoolOperation.classStudents: ApiRoute('GET', '/classes/{classId}/students'),
  SchoolOperation.createClass: ApiRoute('POST', '/classes'),
  SchoolOperation.editClass: ApiRoute('PUT', '/admin/classes/{id}'),
  SchoolOperation.students: ApiRoute('GET', '/admin/students'),
  SchoolOperation.scopedStudents: ApiRoute('GET', '/students/search'),
  SchoolOperation.createStudent: ApiRoute('POST', '/students'),
  SchoolOperation.editStudent: ApiRoute('PUT', '/admin/students/{id}'),
  SchoolOperation.enrollments: ApiRoute(
    'GET',
    '/admin/students/{id}/enrollments',
  ),
  SchoolOperation.changeEnrollment: ApiRoute(
    'POST',
    '/admin/students/{id}/enrollments',
  ),
  SchoolOperation.linkParent: ApiRoute(
    'POST',
    '/admin/students/{studentId}/parents',
  ),
  SchoolOperation.notificationSettings: ApiRoute(
    'GET',
    '/notifications/settings',
  ),
  SchoolOperation.years: ApiRoute('GET', '/academic-years'),
  SchoolOperation.yearConfiguration: ApiRoute('GET', '/admin/academic-years'),
  SchoolOperation.nextYear: ApiRoute(
    'GET',
    '/admin/academic-years/{code}/next',
  ),
  SchoolOperation.saveYear: ApiRoute('PUT', '/admin/academic-years/{code}'),
  SchoolOperation.children: ApiRoute('GET', '/parent/students'),
  SchoolOperation.absences: ApiRoute('GET', '/parent/absences/search'),
  SchoolOperation.reportAbsence: ApiRoute('POST', '/parent/absences'),
  SchoolOperation.replaceAbsence: ApiRoute(
    'POST',
    '/parent/absences/{id}/replace',
  ),
  SchoolOperation.cancelAbsence: ApiRoute(
    'POST',
    '/parent/absences/{id}/cancel',
  ),
  SchoolOperation.workflowDays: ApiRoute('GET', '/meal-days/workflow'),
  SchoolOperation.createMealDay: ApiRoute('POST', '/meal-days'),
  SchoolOperation.portions: ApiRoute('GET', '/meal-days/{dayId}/portions'),
  SchoolOperation.settle: ApiRoute('POST', '/meal-days/{dayId}/settle'),
  SchoolOperation.decisions: ApiRoute('GET', '/meal-days/{dayId}/decisions'),
  SchoolOperation.exceptionHistory: ApiRoute(
    'GET',
    '/meal-days/{dayId}/students/{studentId}/exceptions',
  ),
  SchoolOperation.recordException: ApiRoute(
    'POST',
    '/meal-days/{dayId}/exceptions',
  ),
  SchoolOperation.amendments: ApiRoute('GET', '/meal-days/{dayId}/amendments'),
  SchoolOperation.requestAmendment: ApiRoute(
    'POST',
    '/meal-days/{dayId}/amendments',
  ),
  SchoolOperation.amendmentDetail: ApiRoute(
    'GET',
    '/meal-days/{dayId}/amendments/{requestId}',
  ),
  SchoolOperation.reviewAmendment: ApiRoute(
    'POST',
    '/meal-days/{dayId}/amendments/{requestId}/review',
  ),
  SchoolOperation.calendar: ApiRoute('GET', '/admin/meal-calendar/{code}'),
  SchoolOperation.schedule: ApiRoute(
    'PUT',
    '/admin/meal-calendar/{code}/schedule',
  ),
  SchoolOperation.calendarDay: ApiRoute(
    'PUT',
    '/admin/meal-calendar/{code}/days/{date}',
  ),
  SchoolOperation.previewSessions: ApiRoute(
    'POST',
    '/admin/meal-calendar/{code}/preview',
  ),
  SchoolOperation.generateSessions: ApiRoute(
    'POST',
    '/admin/meal-calendar/{code}/generate',
  ),
  SchoolOperation.calendarHistory: ApiRoute(
    'GET',
    '/admin/meal-calendar/{code}/history',
  ),
  SchoolOperation.meals: ApiRoute('GET', '/meal-days/search'),
  SchoolOperation.mealDetail: ApiRoute('GET', '/meal-days/{dayId}'),
};

const arrayOperations = {
  SchoolOperation.kitchenHistory,
  SchoolOperation.parentLinkClasses,
  SchoolOperation.reviewLinkClasses,
  SchoolOperation.assignedClasses,
  SchoolOperation.classStudents,
  SchoolOperation.enrollments,
  SchoolOperation.years,
  SchoolOperation.yearConfiguration,
  SchoolOperation.children,
  SchoolOperation.calendarHistory,
};
const pagedOperations = {
  SchoolOperation.ingredients,
  SchoolOperation.kitchenSettlements,
  SchoolOperation.studentImportHistory,
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
};

List<String> queryFields(SchoolOperation operation) => switch (operation) {
  SchoolOperation.ingredients => ['search', 'page', 'pageSize'],
  SchoolOperation.nutrition => ['validAt', 'knownAt'],
  SchoolOperation.parentLinks || SchoolOperation.reviewableParentLinks => [
    'status',
    'page',
    'pageSize',
    'classId',
    'schoolYear',
  ],
  SchoolOperation.users => ['page', 'pageSize', 'classId', 'search', 'role'],
  SchoolOperation.scopes => [
    'search',
    'classId',
    'classPage',
    'studentPage',
    'selectedClassIds',
    'selectedStudentIds',
  ],
  SchoolOperation.studentImportHistory => ['classId', 'page', 'pageSize'],
  SchoolOperation.classes => ['search', 'page', 'pageSize'],
  SchoolOperation.students || SchoolOperation.scopedStudents => [
    'classId',
    'search',
    'status',
    'parentStatus',
    'page',
    'pageSize',
  ],
  SchoolOperation.absences => [
    'studentId',
    'status',
    'search',
    'page',
    'pageSize',
  ],
  SchoolOperation.kitchenSettlements => ['date', 'page', 'pageSize'],
  SchoolOperation.workflowDays => ['date', 'page'],
  SchoolOperation.decisions => ['classId', 'search', 'page', 'pageSize'],
  SchoolOperation.exceptionHistory => ['page', 'pageSize'],
  SchoolOperation.amendments => ['classId', 'q', 'page', 'candidatePage'],
  SchoolOperation.calendar => ['from', 'to'],
  SchoolOperation.meals => ['date', 'status', 'search', 'page', 'pageSize'],
  _ => [],
};

class SchoolRepositoryImpl implements SchoolRepository {
  SchoolRepositoryImpl(this._client, this._baseUrl, this._token);
  final http.Client _client;
  final String _baseUrl;
  final String? Function() _token;
  bool _closed = false;
  @override
  Future<SchoolResult> execute(
    SchoolOperation operation, {
    Map<String, Object?> context = const {},
    Map<String, Object?> input = const {},
  }) async {
    if (_closed) throw const SchoolFailure('Yêu cầu đã kết thúc.');
    final token = _token();
    if (token == null) {
      throw const SchoolFailure('Vui lòng đăng nhập lại.', status: 401);
    }
    final route = schoolRoutes[operation]!;
    final path = route.path.replaceAllMapped(RegExp(r'\{(\w+)\}'), (match) {
      final value = context[match[1]] ?? input[match[1]];
      if (value == null || value.toString().isEmpty) {
        throw const SchoolFailure('Chưa chọn dữ liệu cần thao tác.');
      }
      return Uri.encodeComponent(value.toString());
    });
    final query = <String, String>{};
    if (route.method == 'GET') {
      for (final entry in {...context, ...input}.entries) {
        if (queryFields(operation).contains(entry.key) &&
            entry.value != null &&
            entry.value.toString().isNotEmpty) {
          query[entry.key] = entry.value.toString();
        }
      }
    }
    final uri = Uri.parse(
      '$_baseUrl$path',
    ).replace(queryParameters: query.isEmpty ? null : query);
    final uploading =
        operation == SchoolOperation.previewStudentImport ||
        operation == SchoolOperation.confirmStudentImport;
    final http.BaseRequest request;
    if (uploading) {
      final bytes = input['fileBytes'];
      final filename = input['fileName']?.toString() ?? '';
      if (bytes is! List<int> ||
          bytes.isEmpty ||
          bytes.length > 5 * 1024 * 1024 ||
          !filename.toLowerCase().endsWith('.xlsx') ||
          input['classId'] is! String ||
          input['startDate'] is! String) {
        throw const SchoolFailure(
          'Chọn lớp, ngày bắt đầu và file XLSX tối đa 5 MB.',
        );
      }
      request = http.MultipartRequest(route.method, uri)
        ..fields.addAll({
          'classId': input['classId'] as String,
          'startDate': input['startDate'] as String,
        })
        ..files.add(
          http.MultipartFile.fromBytes('file', bytes, filename: filename),
        );
    } else {
      final regular = http.Request(route.method, uri);
      if (route.method != 'GET' && input.isNotEmpty) {
        regular.headers['Content-Type'] = 'application/json';
        regular.body = jsonEncode(input);
      }
      request = regular;
    }
    request.headers.addAll({
      'Authorization': 'Bearer $token',
      'Accept': 'application/json',
    });
    try {
      final response = await _client
          .send(request)
          .then(http.Response.fromStream)
          .timeout(const Duration(seconds: 20));
      if (_closed || _token() != token) {
        throw const SchoolFailure(
          'Phiên đã thay đổi. Vui lòng tải lại.',
          // A late response must not invalidate a newer session.
        );
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw SchoolFailure(switch (response.statusCode) {
          401 => 'Phiên đã hết hạn. Vui lòng đăng nhập lại.',
          403 => 'Bạn không có quyền thực hiện thao tác này.',
          404 => 'Không tìm thấy dữ liệu. Kiểm tra mã hoặc tải lại danh sách.',
          409 =>
            'Dữ liệu vừa thay đổi hoặc đã tồn tại. Tải lại trước khi thao tác.',
          400 || 422 => _businessError(response.body),
          429 => 'Thao tác quá nhanh. Vui lòng chờ và thử lại.',
          _ => 'Máy chủ chưa xử lý được yêu cầu. Vui lòng thử lại.',
        }, status: response.statusCode);
      }
      if (response.statusCode == 204 || response.body.isEmpty) {
        if (route.method == 'GET') {
          throw const FormatException();
        }
        return SchoolResult(summary: SchoolRecord({}), records: []);
      }
      final Object? data = jsonDecode(utf8.decode(response.bodyBytes));
      if (arrayOperations.contains(operation) && data is! List ||
          !arrayOperations.contains(operation) && data is List) {
        throw const FormatException();
      }
      if (data is List) {
        return SchoolResult(
          summary: SchoolRecord({}),
          records: data.map(_record).toList(),
        );
      }
      var summary = _record(data);
      if (operation == SchoolOperation.reportLineage) {
        final rawSource = summary.text('sourceJson');
        Object? source;
        if (rawSource.length <= 100000) {
          try {
            source = jsonDecode(rawSource);
          } on FormatException {
            /* Keep report metadata readable. */
          }
        }
        summary = SchoolRecord({
          ...summary.fields,
          'source': source,
          'sourceUnavailable': source == null,
        });
      }
      if (pagedOperations.contains(operation) &&
          (summary['items'] is! List<Object?> ||
              summary['total'] is! int ||
              summary.number('total') < 0)) {
        throw const FormatException();
      }
      if (operation == SchoolOperation.portions &&
              summary['classes'] is! List<Object?> ||
          operation == SchoolOperation.calendar &&
              summary['days'] is! List<Object?>) {
        throw const FormatException();
      }
      final records = switch (operation) {
        SchoolOperation.previewStudentImport => summary.records('rows'),
        SchoolOperation.portions => summary.records('classes'),
        SchoolOperation.calendar => summary.records('days'),
        _ => summary.records('items'),
      };
      return SchoolResult(summary: summary, records: records);
    } on TimeoutException {
      throw const SchoolFailure(
        'Kết nối hết thời gian chờ. Tải lại để kiểm tra trước khi gửi lại.',
      );
    } on http.ClientException {
      throw const SchoolFailure(
        'Không kết nối được máy chủ. Kiểm tra mạng và thử lại.',
      );
    } on FormatException {
      throw const SchoolFailure('Dữ liệu phản hồi không hợp lệ.');
    } on TypeError {
      throw const SchoolFailure('Dữ liệu phản hồi không hợp lệ.');
    }
  }

  static SchoolRecord _record(Object? value) {
    if (value is! Map<String, dynamic>) throw const FormatException();
    return SchoolRecord(Map<String, Object?>.from(value));
  }

  static String _businessError(String body) {
    try {
      final Object? json = jsonDecode(body);
      if (json is Map<String, dynamic> &&
          json['code'] == 'NUTRIENT_DATA_MISSING') {
        final missing = json['missingIngredients'];
        final names = missing is List
            ? missing
                  .whereType<Map<String, dynamic>>()
                  .take(5)
                  .map((item) => item['name'])
                  .whereType<String>()
                  .map(
                    (name) =>
                        name.length > 80 ? '${name.substring(0, 80)}…' : name,
                  )
                  .join(', ')
            : '';
        return 'Chưa có dữ liệu dinh dưỡng phù hợp${names.isEmpty ? '.' : ': $names.'} Cập nhật nguyên liệu hoặc chọn lại thời điểm đối chiếu.';
      }
      if (json is Map<String, dynamic> && json['code'] == 'VALIDATION_ERROR') {
        return 'Thông tin chưa hợp lệ. Kiểm tra tên, định lượng, đơn vị và dữ liệu dinh dưỡng.';
      }
      if (json is Map<String, dynamic> &&
          (json['message'] is String || json['detail'] is String)) {
        final message = (json['message'] ?? json['detail']) as String;
        if (message.length <= 500) return message;
      }
    } catch (_) {
      /* No raw server response is shown. */
    }
    return 'Thông tin chưa hợp lệ. Kiểm tra các trường và thử lại.';
  }

  @override
  void dispose() {
    _closed = true;
    _client.close();
  }
}

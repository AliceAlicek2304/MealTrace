import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';
import 'support/school_fixtures.dart';

/// Checks the mobile commands against the authoritative BE request records.
/// This does not require a running API or send notifications to real people.
void main() {
  test('Android routes match BE, with only the documented workflow gaps', () {
    String normalized(String path) =>
        path.replaceAll(RegExp(r'\{[^}]+\}'), '{}');
    final covered = schoolRoutes.values
        .map((route) => '${route.method} ${normalized('/api${route.path}')}')
        .toSet();
    covered.addAll([
      'POST /api/auth/login',
      'POST /api/auth/register/otp',
      'POST /api/auth/register',
      'GET /api/auth/me', 'POST /api/auth/logout',
      // Search variants retain the same business UI and support the full dataset.
      'GET /api/parent/absences',
      'GET /api/meal-days',
      'GET /api/kitchen/settlements',
    ]);
    // Serving awaits the combined migration and the menu/source/version
    // handoff. Admin maintenance of age ranges is outside Web FE parity.
    const deferred = {
      'POST /api/kitchen/servings',
      'GET /api/kitchen/servings/{}',
      'POST /api/kitchen/servings/{}/versions',
      'GET /api/kitchen/servings/{}/history',
      'GET /api/kitchen/nutrition-references',
      'POST /api/admin/nutrition-references',
    };
    final backend = <String>{};
    for (final file in Directory(
      '../be/src/MealTrace.Api/Features',
    ).listSync().whereType<File>()) {
      if (!file.path.endsWith('.cs')) continue;
      final source = file.readAsStringSync();
      final name = file.uri.pathSegments.last;
      final groups = <String, String>{
        'app': '',
        'kitchen': '/api/kitchen',
        'admin': '/api/admin',
        'parent': '/api/parent/students',
        if (name == 'AuditEndpoints.cs') 'group': '/api/admin/audit-logs',
      };
      final registrations = RegExp(
        r'var\s+(\w+)\s*=\s*(\w+)\.MapGroup\("([^"]*)"\)|(\w+)\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"',
      );
      for (final match in registrations.allMatches(source)) {
        if (match[1] != null) {
          final prefix = groups[match[2]];
          expect(prefix, isNotNull, reason: 'Unknown endpoint group in $name');
          groups[match[1]!] = '$prefix${match[3]}';
          continue;
        }
        final prefix = groups[match[4]];
        expect(prefix, isNotNull, reason: 'Unknown endpoint group in $name');
        backend.add(
          '${match[5]!.toUpperCase()} ${normalized('$prefix${match[6]}')}',
        );
      }
    }
    expect(backend.length, greaterThan(75));
    expect(
      covered.difference(backend),
      isEmpty,
      reason: 'Android routes absent in BE',
    );
    expect(
      backend.difference(covered),
      deferred,
      reason: 'APIs without Android coverage',
    );
  });
  const requests = {
    SchoolOperation.createUser: 'Accounts/AccountInput.cs',
    SchoolOperation.updateUser: 'Accounts/AccountInput.cs',
    SchoolOperation.resetPassword: 'Accounts/ResetPasswordInput.cs',
    SchoolOperation.changePassword: 'Auth/ChangePasswordRequest.cs',
    SchoolOperation.createClass: 'Workflow/CreateClass.cs',
    SchoolOperation.editClass: 'Students/EditClass.cs',
    SchoolOperation.createStudent: 'Workflow/CreateStudent.cs',
    SchoolOperation.editStudent: 'Students/EditStudent.cs',
    SchoolOperation.changeEnrollment: 'Students/ChangeEnrollment.cs',
    SchoolOperation.linkParent: 'Workflow/LinkParent.cs',
    SchoolOperation.saveYear: 'Workflow/YearDates.cs',
    SchoolOperation.reportAbsence: 'Workflow/ReportAbsence.cs',
    SchoolOperation.replaceAbsence: 'Workflow/ReportAbsence.cs',
    SchoolOperation.createMealDay: 'Workflow/CreateMealDay.cs',
    SchoolOperation.recordException: 'Meals/ExceptionInput.cs',
    SchoolOperation.requestAmendment: 'Portions/RequestInput.cs',
    SchoolOperation.reviewAmendment: 'Portions/ReviewInput.cs',
    SchoolOperation.schedule: 'Calendar/ScheduleInput.cs',
    SchoolOperation.calendarDay: 'Calendar/DayInput.cs',
    SchoolOperation.previewSessions: 'Calendar/GenerateInput.cs',
    SchoolOperation.generateSessions: 'Calendar/GenerateInput.cs',
  };
  for (final entry in requests.entries) {
    test('${entry.key} DTO keys remain compatible with BE contract', () {
      final source = File(
        '../be/src/MealTrace.Application/Dtos/${entry.value}',
      ).readAsStringSync();
      final properties =
          RegExp(r'(?:string|Guid|bool|int|DateOnly)(?:\[\])?\??\s+(\w+)')
              .allMatches(source)
              .map(
                (match) =>
                    '${match[1]![0].toLowerCase()}${match[1]!.substring(1)}',
              )
              .toSet();
      final input = formInput(
        schoolForms[entry.key]!,
        validSchoolValues(),
        validSchoolValues(),
      );
      expect(input.keys.where((key) => !properties.contains(key)), isEmpty);
      // Legacy optional inputs intentionally replaced by the web's newer flow.
      final omitted = switch (entry.key) {
        SchoolOperation.linkParent => {'email'},
        SchoolOperation.recordException => {'willEat'},
        SchoolOperation.requestAmendment => {'studentId'},
        SchoolOperation.previewSessions => {'previewToken'},
        _ => <String>{},
      };
      expect(input.keys.toSet(), properties.difference(omitted));
    });
  }
}

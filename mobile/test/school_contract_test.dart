import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:mealtrace_mobile/features/school/domain/school_models.dart';
import 'package:mealtrace_mobile/features/school/data/school_repository_impl.dart';
import 'package:mealtrace_mobile/features/school/presentation/school_forms.dart';
import 'support/school_fixtures.dart';

/// Checks the mobile commands against the authoritative BE request records.
/// This does not require a running API or send notifications to real people.
void main() {
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
  test('Every mobile route uses an existing endpoint suffix in BE source', () {
    final files = Directory(
      '../be/src/MealTrace.Api/Features',
    ).listSync().whereType<File>();
    final sources = files
        .map((file) => file.readAsStringSync())
        .join('\n')
        .replaceAll(':guid', '')
        .replaceAll(':int', '');
    for (final route in schoolRoutes.values) {
      // BE groups add /api, /admin, /auth or /meal-days prefixes.
      final suffix = route.path.replaceAll('{dayId}', '{id}');
      final candidates = [
        '/api${route.path}',
        suffix,
        suffix.replaceFirst('/admin', ''),
        suffix.replaceFirst('/auth', ''),
        suffix.replaceFirst('/meal-days', ''),
      ];
      if (route.path.contains('/link-requests/') ||
          route.path.contains('/student-link-requests/')) {
        candidates.add(
          route.path
              .replaceFirst('/parent/link-requests', '')
              .replaceFirst('/student-link-requests', ''),
        );
      }
      if (route.path.contains('/students/import/')) {
        candidates.add(route.path.replaceFirst('/admin/students/import', ''));
      }
      if (route.path.contains('/amendments')) {
        candidates.addAll([
          '/meal-days/{dayId}/amendments',
          '/{requestId}',
          '/{requestId}/review',
        ]);
      }
      if (route.path.contains('/meal-calendar/')) {
        candidates.add(route.path.replaceFirst('/admin/meal-calendar', ''));
      }
      expect(
        candidates.any((path) => sources.contains('"$path"')),
        true,
        reason: route.path,
      );
    }
  });
}

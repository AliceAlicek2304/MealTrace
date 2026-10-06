namespace MealTrace.Application.Dtos.Meals;

public sealed record MealDecision(Guid StudentId, string StudentCode, string FullName, Guid EnrollmentId,
Guid ClassId, string ClassName, string SchoolYear, bool WillEat, string Source,
bool ParentReportedAbsent, Guid? AbsenceId, Guid? LatestEventId, string? LatestAction, string? LatestReason);

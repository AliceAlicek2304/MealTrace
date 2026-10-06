namespace MealTrace.Application.Dtos.Workflow;

public sealed record ReportAbsence(Guid StudentId, DateOnly FromDate, DateOnly ToDate, string Reason);

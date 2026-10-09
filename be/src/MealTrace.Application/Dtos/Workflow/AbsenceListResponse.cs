namespace MealTrace.Application.Dtos.Workflow;

public sealed record AbsenceListResponse(List<AbsenceSummary> Items, int Total, int Page, int PageSize, List<AbsenceStudentOption> Students);

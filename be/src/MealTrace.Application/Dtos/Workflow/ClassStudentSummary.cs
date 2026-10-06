using MealTrace.Application.Dtos.Students;

namespace MealTrace.Application.Dtos.Workflow;

public sealed record ClassStudentSummary
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
    public required ParentSummary[] Parents { get; init; }
}

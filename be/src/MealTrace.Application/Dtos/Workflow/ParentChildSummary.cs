
namespace MealTrace.Application.Dtos.Workflow;

public sealed record ParentChildSummary
{
    public Guid StudentId { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
    public required string SchoolYear { get; init; }
    public DateOnly? YearStartDate { get; init; }
    public DateOnly? YearEndDate { get; init; }
}

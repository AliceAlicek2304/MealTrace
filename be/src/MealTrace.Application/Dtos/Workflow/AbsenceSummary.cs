
namespace MealTrace.Application.Dtos.Workflow;

public sealed record AbsenceSummary
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string? SchoolYear { get; init; }
}

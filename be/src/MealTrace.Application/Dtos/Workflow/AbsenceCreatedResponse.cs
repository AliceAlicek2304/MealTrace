
namespace MealTrace.Application.Dtos.Workflow;

public sealed record AbsenceCreatedResponse
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
}

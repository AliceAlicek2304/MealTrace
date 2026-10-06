
namespace MealTrace.Application.Dtos.Students;

public sealed record EnrollmentHistoryItem
{
    public Guid Id { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
    public required string SchoolYear { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public required string Reason { get; init; }
    public string? EndReason { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public DateTimeOffset? EndRecordedAt { get; init; }
    public Guid? EndedByUserId { get; init; }
}

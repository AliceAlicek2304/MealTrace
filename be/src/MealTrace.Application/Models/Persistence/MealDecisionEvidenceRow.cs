namespace MealTrace.Application.Models.Persistence;

public sealed record MealDecisionEvidenceRow
{
    public int Kind { get; init; } = default!;
    public Guid StudentId { get; init; } = default!;
    public Guid Id { get; init; } = default!;
    public DateTimeOffset RecordedAt { get; init; } = default!;
    public bool? WillEat { get; init; } = default!;
    public string? Reason { get; init; } = default!;
}

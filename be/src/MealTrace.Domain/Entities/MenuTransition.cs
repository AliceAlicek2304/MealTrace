namespace MealTrace.Domain.Entities;

/// <summary>Append-only lifecycle event: draft, submitted, approved, rejected or published.</summary>
public sealed class MenuTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuVersionId { get; set; }
    /// <summary>1, 2, 3... per menu version; unique, so two concurrent transitions cannot both win.</summary>
    public int Sequence { get; set; }
    public required string State { get; set; }
    public string? Reason { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

namespace MealTrace.Domain.Entities;

/// <summary>Immutable menu content. Its state is the latest <see cref="MenuTransition"/>, never a mutable column.</summary>
public sealed class MenuVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuId { get; set; }
    public int Version { get; set; }
    public string? Reason { get; set; }
    public Guid? PriorVersionId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
}

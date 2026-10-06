namespace MealTrace.Domain.Entities;

public sealed class MealEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public required string Kind { get; set; }
    public required string Description { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AmendsId { get; set; }
}

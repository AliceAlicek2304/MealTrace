
namespace MealTrace.Application.Dtos.Meals;

public sealed record MealEvidenceSummary
{
    public Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string Description { get; init; }
    public string? PhotoUrl { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public DateTimeOffset SyncedAt { get; init; }
    public Guid? AmendsId { get; init; }
}

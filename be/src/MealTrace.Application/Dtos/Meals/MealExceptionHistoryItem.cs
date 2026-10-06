
namespace MealTrace.Application.Dtos.Meals;

public sealed record MealExceptionHistoryItem
{
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("action")]
    public required string Action { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid? RecordedByUserId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("actorName")]
    public string? ActorName { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isLegacy")]
    public bool IsLegacy { get; init; }
    public Guid? SupersedesId { get; init; }
    public int Sequence { get; init; }
}

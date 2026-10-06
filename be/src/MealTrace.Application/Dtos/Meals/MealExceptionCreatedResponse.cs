
namespace MealTrace.Application.Dtos.Meals;

public sealed record MealExceptionCreatedResponse
{
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("action")]
    public required string Action { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid? SupersedesId { get; init; }
    public int Sequence { get; init; }
}

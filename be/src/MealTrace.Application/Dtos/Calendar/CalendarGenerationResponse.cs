
namespace MealTrace.Application.Dtos.Calendar;

public sealed record CalendarGenerationResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public int Created { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("restored")]
    public int Restored { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("existing")]
    public int Existing { get; init; }
}

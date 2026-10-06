
namespace MealTrace.Application.Dtos.Calendar;
public sealed record CalendarDaySummary
{
    [System.Text.Json.Serialization.JsonPropertyName("date")]
    public DateOnly Date { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isOpen")]
    public bool IsOpen { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("mealTypes")]
    public required string[] MealTypes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isException")]
    public bool IsException { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("locked")]
    public bool Locked { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("sessions")]
    public required IEnumerable<CalendarSessionSummary> Sessions { get; init; }
}

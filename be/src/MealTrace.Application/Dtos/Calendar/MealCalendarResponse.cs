
namespace MealTrace.Application.Dtos.Calendar;

public sealed record MealCalendarResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("schoolYear")]
    public required string SchoolYear { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("from")]
    public DateOnly From { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("to")]
    public DateOnly To { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("revision")]
    public int Revision { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("weekdays")]
    public required IEnumerable<int> Weekdays { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("mealTypes")]
    public required string[] MealTypes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("days")]
    public required List<CalendarDaySummary> Days { get; init; }
}

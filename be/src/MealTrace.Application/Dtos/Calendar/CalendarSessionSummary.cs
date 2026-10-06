
namespace MealTrace.Application.Dtos.Calendar;

public sealed record CalendarSessionSummary
{
    public Guid Id { get; init; }
    public required string MealType { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isSettled")]
    public bool IsSettled { get; init; }
}

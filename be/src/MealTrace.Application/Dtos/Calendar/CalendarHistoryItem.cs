
namespace MealTrace.Application.Dtos.Calendar;

public sealed record CalendarHistoryItem
{
    public Guid Id { get; init; }
    public DateOnly? Date { get; init; }
    public required string Kind { get; init; }
    public required string Reason { get; init; }
    public Guid ActorId { get; init; }
    public required string ActorName { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

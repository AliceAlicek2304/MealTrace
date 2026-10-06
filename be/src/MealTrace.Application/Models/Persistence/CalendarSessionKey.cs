namespace MealTrace.Application.Models.Persistence;

public sealed record CalendarSessionKey
{
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
}

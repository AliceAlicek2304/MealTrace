
namespace MealTrace.Application.Dtos.Calendar;

public sealed record GeneratedSessionSummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
}

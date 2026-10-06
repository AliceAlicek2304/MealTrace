namespace MealTrace.Application.Models.Persistence;

public sealed record EligibleClassRow
{
    public Guid ClassId { get; init; } = default!;
    public string Name { get; init; } = default!;
}

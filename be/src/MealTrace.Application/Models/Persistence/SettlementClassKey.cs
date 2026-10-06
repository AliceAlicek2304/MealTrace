namespace MealTrace.Application.Models.Persistence;

public sealed record SettlementClassKey
{
    public Guid ClassId { get; init; } = default!;
    public string ClassName { get; init; } = default!;
}

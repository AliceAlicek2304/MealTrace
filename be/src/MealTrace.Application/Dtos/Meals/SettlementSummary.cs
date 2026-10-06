
namespace MealTrace.Application.Dtos.Meals;

public sealed record SettlementSummary
{
    public Guid Id { get; init; }
    public Guid? ClassId { get; init; }
    public string? ClassName { get; init; }
    public int Version { get; init; }
    public int Count { get; init; }
    public DateTimeOffset SettledAt { get; init; }
    public required string SettledBy { get; init; }
    public string? Reason { get; init; }
    public Guid? SupersedesId { get; init; }
}

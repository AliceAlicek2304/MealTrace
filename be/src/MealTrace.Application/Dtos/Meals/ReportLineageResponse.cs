
namespace MealTrace.Application.Dtos.Meals;

public sealed record ReportLineageResponse
{
    public Guid Id { get; init; }
    public Guid MealDayId { get; init; }
    public Guid SettlementId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SignedAt { get; init; }
    public decimal EnergyKcalPerPortion { get; init; }
    public decimal CostPerPortion { get; init; }
    public required string SourceJson { get; init; }
}

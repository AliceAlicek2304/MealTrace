namespace MealTrace.Domain.Entities;

public sealed class ReportSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid SettlementId { get; set; }
    public PortionSettlement Settlement { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SignedAt { get; set; }
    public decimal EnergyKcalPerPortion { get; set; }
    public decimal CostPerPortion { get; set; }
    public string SourceJson { get; set; } = "{}";
}

namespace MealTrace.Domain.Entities;

public sealed class PortionSettlement
{
    public int Version { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid? ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public string? ClassName { get; set; }
    public int Count { get; set; }
    public DateTimeOffset CutoffAt { get; set; }
    public DateTimeOffset SettledAt { get; set; } = DateTimeOffset.UtcNow;
    public required string SettledBy { get; set; }
    public string? Reason { get; set; }
    public Guid? SupersedesId { get; set; }
    public List<SettlementStudent> Students { get; set; } = [];
    public List<SettlementDecision> Decisions { get; set; } = [];
}

// Freeze both included and excluded children and the sources used at cutoff.

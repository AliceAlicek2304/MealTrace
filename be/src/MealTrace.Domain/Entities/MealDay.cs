namespace MealTrace.Domain.Entities;

public sealed class MealDay
{
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string? SchoolYear { get; set; }
    public required string MealType { get; set; }
    public DateTimeOffset CutoffAt { get; set; }
    public DateTimeOffset? SettledAt { get; set; }
    public int DecisionRevision { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<MenuDish> Dishes { get; set; } = [];
    public List<MealRegistration> Registrations { get; set; } = [];
    public List<PortionSettlement> Settlements { get; set; } = [];
    public List<MealEvidence> Evidence { get; set; } = [];
}

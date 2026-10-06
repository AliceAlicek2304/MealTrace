namespace MealTrace.Domain.Entities;

public sealed class SettlementStudent
{
    public Guid PortionSettlementId { get; set; }
    public PortionSettlement PortionSettlement { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public required string StudentName { get; set; }
}

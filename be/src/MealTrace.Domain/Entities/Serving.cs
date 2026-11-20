namespace MealTrace.Domain.Entities;

public sealed class Serving
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public Guid MenuVersionId { get; set; }
    public Guid SettlementId { get; set; }
    public required string SnapshotToken { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset RecordedAt { get; set; }
    public required string ResponsibleStaffId { get; set; }
    public List<ServedDish> Dishes { get; set; } = [];
}

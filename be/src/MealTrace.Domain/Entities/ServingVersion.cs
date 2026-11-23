namespace MealTrace.Domain.Entities;

public sealed class ServingVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServingId { get; set; }
    public Serving Serving { get; set; } = null!;
    public int Version { get; set; } = 1;
    public Guid MenuVersionId { get; set; }
    public Guid SettlementId { get; set; }
    public required string SnapshotToken { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public required string ResponsibleStaffId { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid? ClientRequestId { get; set; }
    public string? RequestPayloadHash { get; set; }
    public List<ServedDish> Dishes { get; set; } = [];
}

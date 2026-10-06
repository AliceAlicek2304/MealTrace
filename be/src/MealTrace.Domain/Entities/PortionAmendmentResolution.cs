namespace MealTrace.Domain.Entities;

public sealed class PortionAmendmentResolution
{
    public Guid AmendmentId { get; set; }
    public PortionAmendment Amendment { get; set; } = null!;
    public bool Approved { get; set; }
    public Guid? AppliedSettlementId { get; set; }
    public PortionSettlement? AppliedSettlement { get; set; }
    public required string Reason { get; set; }
    public Guid ReviewedBy { get; set; }
    public required string ReviewedByName { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}

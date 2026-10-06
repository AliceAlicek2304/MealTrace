namespace MealTrace.Domain.Entities;

public sealed class PortionAmendment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BaseSettlementId { get; set; }
    public PortionSettlement BaseSettlement { get; set; } = null!;
    // Legacy single-child fields retained for existing records and API clients.
    // New requests store the selected children in Students; quantity-only requests have no children.
    public Guid StudentId { get; set; }
    public required string StudentName { get; set; }
    public required string StudentCode { get; set; }
    public Guid? EnrollmentId { get; set; }
    public bool WasEating { get; set; }
    public bool WillEat { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsQuantityOnly { get; set; }
    public List<PortionAmendmentStudent> Students { get; set; } = [];
    public required string Reason { get; set; }
    public Guid RequestedBy { get; set; }
    public required string RequestedByName { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public PortionAmendmentResolution? Resolution { get; set; }
}

// A single append-only resolution per request; approval links to a new snapshot.

namespace MealTrace.Domain.Entities;

/// <summary>Immutable snapshot of a supplier's details; a correction is a new revision pointing at the prior one.</summary>
public sealed class SupplierRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public int Version { get; set; }
    public required string Name { get; set; }
    public string? Contact { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public Guid? PriorVersionId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
}

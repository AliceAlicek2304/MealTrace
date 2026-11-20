namespace MealTrace.Domain.Entities;

public sealed class IngredientSourceEntryVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceEntryId { get; set; }
    public int Version { get; set; }
    /// <summary>The supplier revision in force when the receipt was recorded, so supplier renames never rewrite history.</summary>
    public Guid SupplierRevisionId { get; set; }
    public DateOnly ReferenceDate { get; set; }
    public decimal Quantity { get; set; }
    public required string Unit { get; set; }
    public string? DeliveryRef { get; set; }
    public string? LotRef { get; set; }
    public string? Reason { get; set; }
    public Guid? PriorVersionId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
}

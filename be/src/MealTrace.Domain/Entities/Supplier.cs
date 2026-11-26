namespace MealTrace.Domain.Entities;

/// <summary>Stable identity of a supplier. Name, contact and reference live in append-only <see cref="SupplierRevision"/> rows.</summary>
public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedByUserId { get; set; }
}

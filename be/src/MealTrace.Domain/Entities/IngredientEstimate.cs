namespace MealTrace.Domain.Entities;

/// <summary>
/// An immutable planning estimate (not a purchase order, not stock): inputs and result as canonical JSON plus SHA-256.
/// A changed amendment or correction produces a new estimate; the old one is never edited.
/// </summary>
public sealed class IngredientEstimate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string InputJson { get; set; }
    public required string PayloadJson { get; set; }
    public required string PayloadHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedByUserId { get; set; }
}

namespace MealTrace.Domain.Entities;

public sealed class SourceAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngredientUseId { get; set; }
    public IngredientUse IngredientUse { get; set; } = null!;
    public Guid SourceEntryVersionId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "g";
}

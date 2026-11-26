namespace MealTrace.Domain.Entities;

public sealed class IngredientUse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServedDishId { get; set; }
    public ServedDish ServedDish { get; set; } = null!;
    public Guid IngredientId { get; set; }
    public Guid? NutrientVersionId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "g";
    public string WeightBasis { get; set; } = "gross";
    public decimal? MissingQuantity { get; set; }
    public string? MissingReason { get; set; }
    public List<SourceAllocation> Allocations { get; set; } = [];
}

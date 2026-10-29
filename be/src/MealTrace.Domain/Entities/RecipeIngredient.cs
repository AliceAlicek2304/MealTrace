namespace MealTrace.Domain.Entities;

public sealed class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeVersionId { get; set; }
    public RecipeVersion RecipeVersion { get; set; } = null!;
    public Guid IngredientVersionId { get; set; }
    public IngredientVersion IngredientVersion { get; set; } = null!;
    public decimal GramsPerPortion { get; set; }

    // v1.3 (BE1): the quantity exactly as entered. Null on rows written before v1.3.
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    /// <summary>"gross" (edible ratio is applied) or "edible" (ratio is not applied); null means gross.</summary>
    public string? WeightBasis { get; set; }
}

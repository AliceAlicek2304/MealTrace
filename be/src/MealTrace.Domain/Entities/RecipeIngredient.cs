namespace MealTrace.Domain.Entities;

public sealed class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeVersionId { get; set; }
    public RecipeVersion RecipeVersion { get; set; } = null!;
    public Guid IngredientVersionId { get; set; }
    public IngredientVersion IngredientVersion { get; set; } = null!;
    public decimal GramsPerPortion { get; set; }
}

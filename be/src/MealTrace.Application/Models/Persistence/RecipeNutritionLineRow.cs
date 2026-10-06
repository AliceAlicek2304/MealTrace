namespace MealTrace.Application.Models.Persistence;

public sealed record RecipeNutritionLineRow
{
    public Guid IngredientId { get; init; } = default!;
    public string IngredientName { get; init; } = default!;
    public Guid PinnedVersionId { get; init; } = default!;
    public decimal GramsPerPortion { get; init; } = default!;
}

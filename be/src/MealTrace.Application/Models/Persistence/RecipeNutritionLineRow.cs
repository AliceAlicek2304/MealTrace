using MealTrace.Domain.Entities;

namespace MealTrace.Application.Models.Persistence;

public sealed record RecipeNutritionLineRow
{
    public Guid IngredientId { get; init; } = default!;
    public string IngredientName { get; init; } = default!;
    public Guid PinnedVersionId { get; init; } = default!;
    public decimal GramsPerPortion { get; init; } = default!;

    // v1.3: the line as entered (null on lines recorded before v1.3) and the pinned ingredient version itself.
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public string? WeightBasis { get; init; }
    public IngredientVersion Pinned { get; init; } = default!;
}

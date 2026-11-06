namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>
/// Quantity is the total for the whole batch, in g or ml; ServingBasis determines the number of portions.
/// WeightBasis: "gross" (as purchased, the edible ratio is applied) or "edible" (already edible, ratio NOT applied).
/// Omitted means "gross", which is what the calculator assumed before v1.3.
/// </summary>
public sealed record RecipeLineRequest(Guid? IngredientId, decimal? Quantity, string? Unit, string? WeightBasis = null);

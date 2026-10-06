namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>Quantity is the total for the whole batch, in g or ml; ServingBasis determines the number of portions.</summary>
public sealed record RecipeLineRequest(Guid? IngredientId, decimal? Quantity, string? Unit);

namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>The line as entered. Quantity and Unit are null on lines recorded before v1.3.</summary>
public sealed record NutritionLineQuantity(decimal? Quantity, string? Unit, string WeightBasis, int ServingBasis);

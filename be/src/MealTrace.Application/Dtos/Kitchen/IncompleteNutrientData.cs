namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>A nutrient version that lacks some values (versions recorded before v1.3 have no fat/carbohydrate).</summary>
public sealed record IncompleteNutrientData(
    Guid IngredientId, string IngredientName, Guid NutrientVersionId, IReadOnlyList<string> MissingFields);

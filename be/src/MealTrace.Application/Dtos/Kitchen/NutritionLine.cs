namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionLine(
    Guid IngredientId,
    string IngredientName,
    Guid PinnedVersionId,     // ingredient version recorded on the recipe line
    Guid NutrientVersionId,   // ingredient version actually used at (validAt, knownAt)
    decimal GramsPerPortion,
    NutritionTotals Contribution);

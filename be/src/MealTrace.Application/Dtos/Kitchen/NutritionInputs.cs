namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionInputs(
    DateTimeOffset ValidAt,
    DateTimeOffset KnownAt,
    int RecipeVersion,
    DateTimeOffset RecipeVersionRecordedAt,
    IReadOnlyList<NutritionLineInput> Lines);

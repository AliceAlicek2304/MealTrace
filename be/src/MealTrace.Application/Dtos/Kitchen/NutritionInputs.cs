namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>ValidAt and KnownAt are null in mode=pinned: time is not used, the stored inputs are.</summary>
public sealed record NutritionInputs(
    DateTimeOffset? ValidAt,
    DateTimeOffset? KnownAt,
    int RecipeVersion,
    DateTimeOffset RecipeVersionRecordedAt,
    IReadOnlyList<NutritionLineInput> Lines,
    int ServingBasis = 1);

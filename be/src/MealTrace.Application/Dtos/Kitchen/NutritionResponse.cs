namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionResponse(
    NutritionTotals PerServing,
    IReadOnlyList<NutritionLine> Lines,
    NutritionRoundingRule RoundingRule,
    NutritionInputs Inputs);

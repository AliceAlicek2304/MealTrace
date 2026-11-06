namespace MealTrace.Application.Dtos.Kitchen;

public sealed record CreateRecipeRequest(
    string? Name, int? ServingBasis, List<RecipeLineRequest>? Lines, RoundingRuleRequest? RoundingRule = null);

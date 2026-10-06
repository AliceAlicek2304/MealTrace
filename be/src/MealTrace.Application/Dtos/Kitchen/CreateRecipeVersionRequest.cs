namespace MealTrace.Application.Dtos.Kitchen;

public sealed record CreateRecipeVersionRequest(int? ExpectedVersion, int? ServingBasis, List<RecipeLineRequest>? Lines);

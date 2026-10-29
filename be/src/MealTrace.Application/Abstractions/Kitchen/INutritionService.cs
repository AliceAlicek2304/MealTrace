using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface INutritionService
{
    /// <summary>Recompute mode at validAt/knownAt (default now). Kept for existing callers.</summary>
    Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct);

    Task<NutritionResponse> CalculateAsync(Guid recipeId, int version, NutritionQuery query, CancellationToken ct);
}

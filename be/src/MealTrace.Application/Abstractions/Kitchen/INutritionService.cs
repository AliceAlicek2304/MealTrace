using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface INutritionService
{
    /// <summary>Recompute mode at validAt/knownAt (default now). Kept for existing callers.</summary>
    Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct);

    Task<NutritionResponse> CalculateAsync(Guid recipeId, int version, NutritionQuery query, CancellationToken ct);

    /// <summary>
    /// Compares a per-meal total with the documented per-meal ranges of the age group in force at (validAt, knownAt).
    /// 422 AGE_GROUP_REFERENCE_MISSING when none is recorded, 422 REFERENCE_BASIS_MISMATCH when only daily or
    /// unspecified ranges exist. Never claims a match it cannot support.
    /// </summary>
    Task<AgeGroupComparison> CompareAsync(
        string ageGroup, NutritionTotals perMeal, string? mealType, DateTimeOffset validAt, DateTimeOffset knownAt,
        CancellationToken ct);
}

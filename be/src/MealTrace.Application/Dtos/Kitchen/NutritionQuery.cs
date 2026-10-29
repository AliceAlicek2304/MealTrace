namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>
/// Mode "recompute" (default) resolves ingredient versions at ValidAt/KnownAt (default: now).
/// Mode "pinned" uses the ingredient versions stored on the recipe lines; ValidAt/KnownAt are not allowed with it.
/// </summary>
public sealed record NutritionQuery(
    string? Mode = null, DateTimeOffset? ValidAt = null, DateTimeOffset? KnownAt = null, string? AgeGroup = null);

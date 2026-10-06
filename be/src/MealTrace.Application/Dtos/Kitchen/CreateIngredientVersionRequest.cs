namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>Nutrient values are per 100 g (or per 100 ml, matching the ingredient unit).</summary>
public sealed record CreateIngredientVersionRequest(
    DateTimeOffset? ValidFrom,
    decimal? EdiblePercent,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? PricePerKg);

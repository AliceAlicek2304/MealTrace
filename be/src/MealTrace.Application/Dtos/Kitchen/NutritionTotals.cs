namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>FatG / CarbG are null when a contributing nutrient version has no value for them: unknown, never 0.</summary>
public sealed record NutritionTotals(decimal Kcal, decimal ProteinG, decimal? FatG = null, decimal? CarbG = null);

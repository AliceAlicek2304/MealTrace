namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionLineInput(
    Guid IngredientId,
    decimal GramsPerPortion,
    decimal EdibleFraction,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G,
    decimal? FatGPer100G = null,
    decimal? CarbGPer100G = null,
    string? WeightBasis = null);

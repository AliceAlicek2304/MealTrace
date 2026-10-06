namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionRoundingRule(int KcalDecimals, int ProteinDecimals, string Mode);

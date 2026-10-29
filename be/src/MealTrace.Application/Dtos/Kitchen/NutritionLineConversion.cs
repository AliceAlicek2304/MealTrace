namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>GramsPerMl is set only for ml lines; EdibleFractionApplied is null when the line is already an edible weight.</summary>
public sealed record NutritionLineConversion(decimal? GramsPerMl, decimal? EdibleFractionApplied, decimal EdibleGramsPerPortion);

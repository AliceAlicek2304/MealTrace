namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionLine(
    Guid IngredientId,
    string IngredientName,
    Guid PinnedVersionId,     // ingredient version recorded on the recipe line
    Guid NutrientVersionId,   // ingredient version actually used (the pinned one in mode=pinned)
    decimal GramsPerPortion,  // entered weight per portion, in grams (ml converted with the density)
    NutritionTotals Contribution,
    bool ChangedFromPinned = false,
    NutritionLineQuantity? Input = null,
    NutritionLineConversion? Conversion = null,
    NutritionTotals? Per100GEdible = null,
    string? Source = null,
    string? SourceRef = null);

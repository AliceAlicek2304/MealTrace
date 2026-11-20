namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionResponse(
    NutritionTotals PerServing,
    IReadOnlyList<NutritionLine> Lines,
    NutritionRoundingRule RoundingRule,
    NutritionInputs Inputs,
    string Mode = "recompute",
    NutritionTotals? PinnedPerServing = null,    // recompute only: the same version using its pinned inputs
    NutritionTotals? DeltaFromPinned = null,     // recompute only: PerServing - PinnedPerServing
    IReadOnlyList<IncompleteNutrientData>? IncompleteData = null,
    AgeGroupComparison? AgeGroupComparison = null,
    IReadOnlyList<MissingIngredient>? UnverifiedConversions = null);   // lines recorded before v1.3 measured in ml: unit conversion unknown

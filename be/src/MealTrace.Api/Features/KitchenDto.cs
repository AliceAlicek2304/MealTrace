namespace MealTrace.Api.Features;

// ============ Ingredients ============

public sealed record CreateIngredientRequest(
    string Name,
    string Unit,
    int EditablePercent);

public sealed record IngredientResponse(
    Guid Id,
    string Name,
    string Unit,
    int EditablePercent,
    NutrientSummary? CurrentNutrient);

public sealed record NutrientSummary(
    decimal EnergyKcal,
    decimal ProteinG,
    decimal FatG,
    decimal CarbG,
    DateTimeOffset ValidFrom);

// ============ Ingredient Versions (Bitemporal) ============

public sealed record CreateIngredientVersionRequest(
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G,
    decimal FatGPer100G,
    decimal CarbGPer100G,
    string Source,
    string? SourceRef = null,
    Guid? SupersedesId = null);

public sealed record IngredientVersionResponse(
    Guid Id,
    Guid IngredientId,
    int Version,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G,
    decimal FatGPer100G,
    decimal CarbGPer100G,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Source,
    string? SourceRef,
    Guid? SupersedesId,
    DateTimeOffset RecordedAt,
    string RecordedByName);

// ============ Recipes ============

public sealed record RecipeLineDto(
    Guid IngredientId,
    decimal Quantity,
    string Unit);

public sealed record CreateRecipeRequest(
    string Name,
    int ServingBasis,
    List<RecipeLineDto> Lines,
    string RoundingRule = "ROUND_HALF_UP");

public sealed record RecipeResponse(
    Guid Id,
    string Name,
    int ServingBasis,
    int CurrentVersion);

// ============ Recipe Versions ============

public sealed record CreateRecipeVersionRequest(
    int ExpectedVersion,
    List<RecipeLineDto> Lines,
    string Reason);

public sealed record RecipeVersionResponse(
    Guid Id,
    Guid RecipeId,
    int Version,
    int ServingBasis,
    string RoundingRule,
    DateTimeOffset RecordedAt,
    List<RecipeLineWithNutrientDto> Lines);

public sealed record RecipeLineWithNutrientDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    Guid? NutrientVersionId,
    decimal EnergyKcal,
    decimal ProteinG,
    decimal FatG,
    decimal CarbG);

// ============ Nutrition Calculation ============

public sealed record NutritionQueryDto(
    DateTimeOffset? ValidAt = null,
    DateTimeOffset? KnownAt = null,
    string? AgeGroup = null);

public sealed record RecipeNutritionResponse(
    Guid RecipeId,
    int Version,
    int ServingBasis,
    string RoundingRule,
    NutritionPerServingDto PerServing,
    List<RecipeNutritionLineDto> Lines,
    NutritionInputsDto Inputs);

public sealed record NutritionPerServingDto(
    decimal EnergyKcal,
    decimal ProteinG,
    decimal FatG,
    decimal CarbG);

public sealed record RecipeNutritionLineDto(
    Guid IngredientId,
    string IngredientName,
    Guid NutrientVersionId,
    int NutrientVersion,
    decimal Quantity,
    string Unit,
    decimal EnergyKcalContribution,
    decimal ProteinGContribution,
    decimal FatGContribution,
    decimal CarbGContribution);

public sealed record NutritionInputsDto(
    DateTimeOffset ValidAt,
    DateTimeOffset KnownAt,
    string? AgeGroup);

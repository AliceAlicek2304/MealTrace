namespace MealTrace.Application.Dtos.Kitchen;

public sealed record RecipeCatalogItem(Guid RecipeId, string Name, int Version, Guid VersionId);

public sealed record RecipeCatalogPage(IReadOnlyList<RecipeCatalogItem> Items, int Total, int Page, int PageSize);

/// <summary>LegacyConversionUnverified: a line recorded before v1.3 whose ingredient is measured in ml; 1 ml is never assumed to be 1 g.</summary>
public sealed record RecipeVersionLine(
    Guid IngredientId, string IngredientName, Guid IngredientVersionId, decimal? Quantity, string? Unit,
    string WeightBasis, decimal GramsPerPortion, bool LegacyConversionUnverified);

public sealed record RecipeVersionItem(
    Guid VersionId, int Version, int? ServingBasis, NutritionRoundingRule Rounding, string? Reason,
    DateTimeOffset RecordedAt, Guid? RecordedBy, IReadOnlyList<RecipeVersionLine> Lines);

public sealed record RecipeVersionList(Guid RecipeId, string Name, IReadOnlyList<RecipeVersionItem> Items);

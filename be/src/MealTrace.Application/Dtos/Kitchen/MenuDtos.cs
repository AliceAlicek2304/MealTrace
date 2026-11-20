namespace MealTrace.Application.Dtos.Kitchen;

public sealed record MenuDishRequest(Guid? RecipeVersionId, int? PlannedPortions);

public sealed record CreateMenuRequest(Guid? MealDayId, List<MenuDishRequest>? Dishes, string? Reason);

public sealed record CreateMenuVersionRequest(int? ExpectedVersion, List<MenuDishRequest>? Dishes, string? Reason);

/// <summary>Submit: the version the caller last saw and an optional note.</summary>
public sealed record MenuTransitionRequest(int? ExpectedVersion, string? Reason = null);

public sealed record MenuReviewRequest(int? ExpectedVersion, string? Decision, string? Reason);

public sealed record MenuPublishRequest(int? ExpectedVersion, string? AgeGroup = null);

public sealed record MenuCreatedResponse(
    Guid Id, Guid VersionId, int Version, string State, DateTimeOffset RecordedAt, Guid? RecordedBy,
    Guid? PriorVersionId = null);

public sealed record MenuTransitionResponse(
    Guid Id, Guid VersionId, int Version, string State, DateTimeOffset At, Guid? Actor,
    Guid? NutritionSnapshotId = null, DateTimeOffset? PublishedAt = null);

public sealed record MenuDishView(Guid RecipeVersionId, Guid RecipeId, string RecipeName, int RecipeVersion, int? PlannedPortions);

public sealed record MenuTransitionView(int Sequence, string State, string? Reason, Guid? Actor, DateTimeOffset At);

public sealed record MenuVersionView(
    Guid VersionId, int Version, Guid? PriorVersionId, string State, string? Reason, DateTimeOffset RecordedAt,
    Guid? RecordedBy, IReadOnlyList<MenuDishView> Dishes, IReadOnlyList<MenuTransitionView> Transitions,
    Guid? NutritionSnapshotId);

public sealed record MenuView(
    Guid Id, Guid MealDayId, DateOnly Date, string MealType, MenuVersionView Latest,
    Guid? PublishedVersionId, int? PublishedVersion);

public sealed record MenuPage(IReadOnlyList<MenuView> Items, int Total, int Page, int PageSize);

public sealed record MenuHistory(Guid Id, Guid MealDayId, IReadOnlyList<MenuVersionView> Items);

// ---- Frozen nutrition ---------------------------------------------------------------------------------

public sealed record MenuDishNutrition(
    Guid RecipeVersionId, Guid RecipeId, string RecipeName, int RecipeVersion, int? PlannedPortions,
    NutritionTotals PerServing, NutritionResponse Calculation);

/// <summary>
/// What is stored (as JSON) when a menu version is published. Menu total = sum of the dishes' unrounded per-serving
/// values, rounded once with the stated rule; a child is assumed to receive one portion of every dish.
/// </summary>
public sealed record MenuNutritionPayload(
    string AlgorithmVersion,
    NutritionRoundingRule Rounding,
    IReadOnlyList<MenuDishNutrition> Dishes,
    NutritionTotals Total,
    string? AgeGroup,
    string AgeComparisonStatus,
    AgeGroupComparison? AgeComparison,
    IReadOnlyList<Guid> ReferenceVersionIds);

public sealed record MenuNutritionResponse(
    string Mode,                          // "pinned" = the frozen snapshot, "recompute" = a new view, never stored
    Guid MenuVersionId,
    int Version,
    Guid? SnapshotId,
    string? PayloadHash,
    MenuNutritionPayload Payload,
    MenuNutritionPayload? AsPublished = null,
    NutritionTotals? DeltaFromPublished = null,
    IReadOnlyList<Guid>? ChangedReferenceVersionIds = null,
    IReadOnlyList<Guid>? ChangedNutrientVersionIds = null);

// ---- Parent view: published versions only, frozen numbers only ----------------------------------------------

public sealed record ParentMenuItem(
    Guid MealDayId, DateOnly Date, string MealType, Guid MenuVersionId, string State,
    IReadOnlyList<MenuDishView> Dishes, NutritionTotals? Nutrition, Guid? CalculationSnapshotId,
    IReadOnlyList<Guid> ReferenceVersionIds, string AgeComparisonStatus, string? AgeGroup);

public sealed record ParentMenuResponse(IReadOnlyList<ParentMenuItem> Items, int Total);

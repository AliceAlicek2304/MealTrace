namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>SnapshotTokens must hold, for each meal day, the token the caller last saw; a stale token is a 409.</summary>
public sealed record IngredientEstimateRequest(
    List<Guid>? MenuVersionIds, List<Guid>? MealDayIds, List<Guid>? ClassIds, Dictionary<Guid, string>? SnapshotTokens);

public sealed record EstimateConversion(
    Guid RecipeVersionId, string Unit, string WeightBasis, decimal? GramsPerMl, decimal EdibleFraction);

public sealed record EstimateLine(
    Guid IngredientId, string IngredientName, decimal Quantity, string Unit, decimal GrossGrams, decimal EdibleGrams,
    IReadOnlyList<Guid> RecipeVersionIds, IReadOnlyList<Guid> NutrientVersionIds,
    IReadOnlyList<EstimateConversion> Conversion);

public sealed record EstimatePortion(Guid MealDayId, Guid ClassId, int Version, int Portions, Guid? SettlementId);

/// <summary>Everything the estimate was computed from, so the same numbers can be reproduced.</summary>
public sealed record EstimatePayload(
    IReadOnlyList<Guid> MenuVersionIds, IReadOnlyList<Guid> SettlementIds, IReadOnlyList<EstimatePortion> Portions,
    IReadOnlyList<EstimateLine> Lines);

public sealed record IngredientEstimateResponse(
    Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<Guid> MenuVersionIds, IReadOnlyList<Guid> SettlementIds,
    IReadOnlyList<EstimatePortion> Portions, IReadOnlyList<EstimateLine> Lines, bool IsPlanningEstimate,
    string PayloadHash);

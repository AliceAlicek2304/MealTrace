namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>Basis is "perMeal" or "daily". A daily range is never compared with a per-meal value.</summary>
public sealed record CreateNutritionReferenceRequest(
    string? AgeGroup, string? Nutrient, decimal? MinValue, decimal? MaxValue, string? Basis, string? MealType,
    string? Source, string? SourceRef, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo,
    Guid? SupersedesId, string? Reason);

public sealed record NutritionReferenceCreatedResponse(
    Guid Id, int Version, DateTimeOffset RecordedAt, Guid? RecordedBy, Guid? SupersedesId);

public sealed record NutritionReferenceView(
    Guid Id, int Version, string AgeGroup, string Nutrient, decimal? MinValue, decimal? MaxValue, string? Basis,
    string? MealType, string Source, string? SourceRef, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo,
    DateTimeOffset RecordedAt, Guid? SupersedesId);

public sealed record NutritionReferenceList(
    string AgeGroup, DateTimeOffset ValidAt, DateTimeOffset KnownAt, IReadOnlyList<NutritionReferenceView> Items);

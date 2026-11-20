namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>Status: "below", "within", "above", or "unknown" when the per-serving value is not known.</summary>
public sealed record AgeGroupComparisonItem(
    string Nutrient, decimal? Value, decimal? Min, decimal? Max, string Status,
    Guid ReferenceId, string Source, string? SourceRef, int Version = 1, string? Basis = null, string? MealType = null);

public sealed record AgeGroupComparison(string AgeGroup, IReadOnlyList<AgeGroupComparisonItem> Items);

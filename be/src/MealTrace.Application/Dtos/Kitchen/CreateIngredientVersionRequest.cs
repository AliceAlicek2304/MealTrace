namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>
/// Nutrient values are per 100 g of EDIBLE portion (if a source gives values per 100 g as purchased, send ediblePercent = 100).
/// Source, FatG and CarbG are required by v1.3 and are optional here only so existing callers still compile.
/// </summary>
public sealed record CreateIngredientVersionRequest(
    DateTimeOffset? ValidFrom,
    decimal? EdiblePercent,
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? PricePerKg,
    decimal? FatG = null,
    decimal? CarbG = null,
    DateTimeOffset? ValidTo = null,
    decimal? GramsPerMl = null,
    string? Source = null,
    string? SourceRef = null,
    Guid? SupersedesId = null);

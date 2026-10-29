namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>FatGPer100G, CarbGPer100G and Source are null on versions recorded before v1.3: unknown, never 0.</summary>
public sealed record NutrientSnapshot(
    Guid VersionId,
    int Version,
    DateTimeOffset ValidFrom,
    DateTimeOffset RecordedAt,
    decimal EdiblePercent,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G,
    DateTimeOffset? ValidTo = null,
    decimal? FatGPer100G = null,
    decimal? CarbGPer100G = null,
    decimal? GramsPerMl = null,
    string? Source = null,
    string? SourceRef = null);

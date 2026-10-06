namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutrientSnapshot(
    Guid VersionId,
    int Version,
    DateTimeOffset ValidFrom,
    DateTimeOffset RecordedAt,
    decimal EdiblePercent,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G);

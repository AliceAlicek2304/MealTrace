namespace MealTrace.Domain.Entities;

public sealed class IngredientVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public int Version { get; set; }
    public decimal EnergyKcalPer100G { get; set; }
    public decimal ProteinGPer100G { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal EdibleFraction { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    // v1.3 (BE1). Null on rows written before v1.3: "not recorded", never 0.
    public decimal? FatGPer100G { get; set; }
    public decimal? CarbGPer100G { get; set; }
    /// <summary>Confirmed density. Required before the ingredient can be used with ml quantities.</summary>
    public decimal? GramsPerMl { get; set; }
    public string? Source { get; set; }
    public string? SourceRef { get; set; }
    /// <summary>Exclusive end of validity; null means open-ended.</summary>
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid? RecordedByUserId { get; set; }
    /// <summary>Explicit correction: the earlier version this row replaces.</summary>
    public Guid? SupersedesId { get; set; }
}

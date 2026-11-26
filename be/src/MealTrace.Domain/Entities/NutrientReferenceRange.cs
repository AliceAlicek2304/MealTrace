namespace MealTrace.Domain.Entities;

/// <summary>
/// Documented age-group reference range for one nutrient, with its source. Data only: no values are built in.
/// Append-only; a correction is a new row that supersedes the old one.
/// </summary>
public sealed class NutrientReferenceRange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AgeGroup { get; set; }
    /// <summary>"kcal", "proteinG", "fatG" or "carbG".</summary>
    public required string Nutrient { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public required string Source { get; set; }
    public string? SourceRef { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }

    // Phase 3 (P3-H02). Rows written earlier have no basis and are treated as "unspecified": never claimed as a match.
    /// <summary>"perMeal" or "daily". A daily range is never compared with a per-meal value.</summary>
    public string? Basis { get; set; }
    public string? MealType { get; set; }
    public int Version { get; set; } = 1;
    public Guid? SupersedesId { get; set; }
    public string? Reason { get; set; }
}

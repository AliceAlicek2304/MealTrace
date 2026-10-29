namespace MealTrace.Domain.Entities;

/// <summary>
/// Documented age-group reference range for one nutrient, with its source. Data only: no values are built in.
/// Append-only; a correction is a newer row with the same AgeGroup and Nutrient.
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
}

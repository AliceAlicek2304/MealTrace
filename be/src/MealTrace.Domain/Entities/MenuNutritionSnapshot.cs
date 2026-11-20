namespace MealTrace.Domain.Entities;

/// <summary>
/// The nutrition result frozen when a menu version is published: exact recipe/nutrient/reference version ids,
/// quantities, conversions, rounding and algorithm version, as canonical JSON plus its SHA-256.
/// </summary>
public sealed class MenuNutritionSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuVersionId { get; set; }
    public required string AlgorithmVersion { get; set; }
    public string? AgeGroup { get; set; }
    public required string AgeComparisonStatus { get; set; }
    public required string PayloadJson { get; set; }
    public required string PayloadHash { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
}

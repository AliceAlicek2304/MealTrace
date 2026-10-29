namespace MealTrace.Domain.Entities;

public sealed class RecipeVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int Version { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecipeIngredient> Ingredients { get; set; } = [];

    // v1.3 (BE1). Null on rows written before v1.3.
    /// <summary>Number of portions the line quantities were entered for; null means lines are already per portion.</summary>
    public int? ServingBasis { get; set; }
    public int? RoundingKcalDecimals { get; set; }
    public int? RoundingMacroDecimals { get; set; }
    public string? Reason { get; set; }
    public Guid? RecordedByUserId { get; set; }
}

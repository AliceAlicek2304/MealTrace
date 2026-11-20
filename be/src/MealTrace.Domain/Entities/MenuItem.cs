namespace MealTrace.Domain.Entities;

/// <summary>A planned dish. Points at an exact recipe version, never "the latest".</summary>
public sealed class MenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuVersionId { get; set; }
    public Guid RecipeVersionId { get; set; }
    public int? PlannedPortions { get; set; }
    public int SortOrder { get; set; }
}

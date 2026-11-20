namespace MealTrace.Domain.Entities;

/// <summary>
/// One physical receipt of an ingredient (a dated delivery). Distinct from the nutrient source on
/// <see cref="IngredientVersion"/>, which says where the nutrient values came from.
/// </summary>
public sealed class IngredientSourceEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngredientId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedByUserId { get; set; }
}

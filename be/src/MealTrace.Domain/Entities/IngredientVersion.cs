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
}

namespace MealTrace.Domain.Entities;

public sealed class RecipeVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int Version { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

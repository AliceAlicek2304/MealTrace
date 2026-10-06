namespace MealTrace.Domain.Entities;

public sealed class MenuDish
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid RecipeVersionId { get; set; }
    public RecipeVersion RecipeVersion { get; set; } = null!;
}

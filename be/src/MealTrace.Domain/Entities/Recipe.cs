namespace MealTrace.Domain.Entities;

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public List<RecipeVersion> Versions { get; set; } = [];
}

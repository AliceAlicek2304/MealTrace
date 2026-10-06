namespace MealTrace.Domain.Entities;

public sealed class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public List<IngredientVersion> Versions { get; set; } = [];
}

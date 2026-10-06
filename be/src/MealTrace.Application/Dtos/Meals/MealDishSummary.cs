
namespace MealTrace.Application.Dtos.Meals;

public sealed record MealDishSummary
{
    public Guid Id { get; init; }
    public Guid RecipeVersionId { get; init; }
    public required string Name { get; init; }
}

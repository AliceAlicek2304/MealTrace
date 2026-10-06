using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface INutritionRepository
{
    Task<RecipeVersion?> FindRecipeVersionAsync(Guid recipeId, int version, CancellationToken ct);
    Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct);
    Task<List<RecipeNutritionLineRow>> ListRecipeNutritionLinesAsync(RecipeVersion recipeVersion, CancellationToken ct);
}

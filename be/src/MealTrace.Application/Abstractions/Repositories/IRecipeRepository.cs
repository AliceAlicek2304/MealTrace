using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IRecipeRepository
{
    Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct);
    Task<int?> GetLatestVersionNumberAsync(Guid recipeId, CancellationToken ct);
    Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdAsync(Guid[] ids, CancellationToken ct);

    void AddRecipeIngredients(IEnumerable<RecipeIngredient> value);
    void AddRecipe(Recipe value);
    void AddRecipeVersion(RecipeVersion value);
}

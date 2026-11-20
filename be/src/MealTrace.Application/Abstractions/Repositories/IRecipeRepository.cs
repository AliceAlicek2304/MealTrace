using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IRecipeRepository
{
    Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct);
    Task<Recipe?> FindRecipeAsync(Guid recipeId, CancellationToken ct);
    Task<RecipeVersion?> FindLatestVersionAsync(Guid recipeId, CancellationToken ct);
    Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdAsync(Guid[] ids, CancellationToken ct);
    Task<int> CountRecipesAsync(string? search, CancellationToken ct);
    /// <summary>Each recipe with its highest version, ordered by name then id.</summary>
    Task<List<RecipeCatalogRow>> ListLatestRecipeVersionsAsync(string? search, int skip, int size, CancellationToken ct);
    Task<List<RecipeVersion>> ListRecipeVersionsAsync(Guid recipeId, CancellationToken ct);

    void AddRecipeIngredients(IEnumerable<RecipeIngredient> value);
    void AddRecipe(Recipe value);
    void AddRecipeVersion(RecipeVersion value);
}

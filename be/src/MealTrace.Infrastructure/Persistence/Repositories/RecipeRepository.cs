using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class RecipeRepository(MealTraceDbContext db) : IRecipeRepository
{
    public async Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Recipes.AnyAsync(r => r.Id == recipeId, ct);
        });
    }
    public async Task<int?> GetLatestVersionNumberAsync(Guid recipeId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.RecipeVersions.Where(v => v.RecipeId == recipeId).MaxAsync(v => (int?)v.Version, ct);
        });
    }
    public async Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdAsync(Guid[] ids, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Ingredients.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        });
    }

    public void AddRecipeIngredients(IEnumerable<RecipeIngredient> value) => db.RecipeIngredients.AddRange(value);
    public void AddRecipe(Recipe value) => db.Recipes.Add(value);
    public void AddRecipeVersion(RecipeVersion value) => db.RecipeVersions.Add(value);
}

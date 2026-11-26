using MealTrace.Application.Models.Persistence;
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
    public async Task<RecipeVersion?> FindLatestVersionAsync(Guid recipeId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.RecipeVersions.AsNoTracking().Where(v => v.RecipeId == recipeId)
                .OrderByDescending(v => v.Version).FirstOrDefaultAsync(ct);
        });
    }
    public async Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdAsync(Guid[] ids, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Ingredients.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        });
    }

    public async Task<Recipe?> FindRecipeAsync(Guid recipeId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Recipes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == recipeId, ct);
        });
    }
    public async Task<int> CountRecipesAsync(string? search, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await Filter(search).CountAsync(ct);
        });
    }
    public async Task<List<RecipeCatalogRow>> ListLatestRecipeVersionsAsync(string? search, int skip, int size, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var page = Filter(search).OrderBy(r => r.Name).ThenBy(r => r.Id).Skip(skip).Take(size);
            var rows = await (
                from r in page
                join v in db.RecipeVersions.AsNoTracking() on r.Id equals v.RecipeId
                where v.Version == db.RecipeVersions.Where(x => x.RecipeId == r.Id).Max(x => x.Version)
                select new { Recipe = r, Latest = v }).ToListAsync(ct);
            return rows.Select(x => new RecipeCatalogRow(x.Recipe, x.Latest)).OrderBy(x => x.Recipe.Name).ThenBy(x => x.Recipe.Id).ToList();
        });
    }
    public async Task<List<RecipeVersion>> ListRecipeVersionsAsync(Guid recipeId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.RecipeVersions.AsNoTracking().Where(v => v.RecipeId == recipeId).ToListAsync(ct);
        });
    }
    private IQueryable<Recipe> Filter(string? search)
    {
        var query = db.Recipes.AsNoTracking();
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().ToLower();
        return query.Where(r => r.Name.ToLower().Contains(term));
    }

    public void AddRecipeIngredients(IEnumerable<RecipeIngredient> value) => db.RecipeIngredients.AddRange(value);
    public void AddRecipe(Recipe value) => db.Recipes.Add(value);
    public void AddRecipeVersion(RecipeVersion value) => db.RecipeVersions.Add(value);
}

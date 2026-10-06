using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class NutritionRepository(MealTraceDbContext db) : INutritionRepository
{
    public async Task<RecipeVersion?> FindRecipeVersionAsync(Guid recipeId, int version, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.RecipeVersions.AsNoTracking().SingleOrDefaultAsync(v => v.RecipeId == recipeId && v.Version == version, ct);
        });
    }
    public async Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Recipes.AnyAsync(r => r.Id == recipeId, ct);
        });
    }
    public async Task<List<RecipeNutritionLineRow>> ListRecipeNutritionLinesAsync(RecipeVersion recipeVersion, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await (
                from ri in db.RecipeIngredients.AsNoTracking()
                join pinned in db.IngredientVersions.AsNoTracking() on ri.IngredientVersionId equals pinned.Id
                join ingredient in db.Ingredients.AsNoTracking() on pinned.IngredientId equals ingredient.Id
                where ri.RecipeVersionId == recipeVersion.Id
                orderby ingredient.Name, ingredient.Id
                select new RecipeNutritionLineRow
                {
                    IngredientId = ingredient.Id,
                    IngredientName = ingredient.Name,
                    PinnedVersionId = pinned.Id,
                    GramsPerPortion = ri.GramsPerPortion
                }

            ).ToListAsync(ct);
        });
    }
}

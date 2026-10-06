using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class IngredientRepository(MealTraceDbContext db) : IIngredientRepository
{
    public async Task<int> CountIngredientsAsync(string? search, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Ingredients.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{EscapeLike(search.Trim())}%";
                query = db.SearchIngredients(query, pattern);
            }
            return await query.CountAsync(ct);
        });
    }
    public async Task<List<Ingredient>> SearchIngredientsAsync(string? search, int skip, int size, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.Ingredients.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{EscapeLike(search.Trim())}%";
                query = db.SearchIngredients(query, pattern);
            }
            return await query.OrderBy(i => i.Name).ThenBy(i => i.Id).Skip(skip).Take(size).ToListAsync(ct);
        });
    }
    public async Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Ingredients.AnyAsync(i => i.Id == ingredientId, ct);
        });
    }
    public async Task<int?> GetLatestVersionNumberAsync(Guid ingredientId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.IngredientVersions.Where(v => v.IngredientId == ingredientId).MaxAsync(v => (int?)v.Version, ct);
        });
    }
    public async Task<List<IngredientVersion>> ListEffectiveVersionCandidatesAsync(Guid[] ids, DateTimeOffset validUtc, DateTimeOffset knownUtc, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.IngredientVersions.AsNoTracking().Where(v => ids.Contains(v.IngredientId) && v.EffectiveFrom <= validUtc && v.RecordedAt <= knownUtc).ToListAsync(ct);
        });
    }
    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public void AddIngredient(Ingredient value) => db.Ingredients.Add(value);
    public void AddIngredientVersion(IngredientVersion value) => db.IngredientVersions.Add(value);
}

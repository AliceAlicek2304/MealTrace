using System.Security.Cryptography;
using System.Text;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class ServingRepository(MealTraceDbContext db) : IServingRepository
{
    public async Task<ServingVersion?> FindVersionByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ServingVersions.AsNoTracking()
                .Include(v => v.Dishes)
                    .ThenInclude(d => d.IngredientUses)
                        .ThenInclude(u => u.Allocations)
                .FirstOrDefaultAsync(v => v.ClientRequestId == clientRequestId, ct);
        });
    }

    public async Task<Serving?> FindServingWithVersionAsync(Guid servingId, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Servings.AsNoTracking()
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Dishes)
                        .ThenInclude(d => d.IngredientUses)
                            .ThenInclude(u => u.Allocations)
                .FirstOrDefaultAsync(s => s.Id == servingId, ct);
        });
    }

    public async Task<Serving?> FindServingByClassAndMealDayAsync(Guid mealDayId, Guid classId, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Servings.AsNoTracking()
                .Include(s => s.Versions.OrderByDescending(v => v.Version))
                .FirstOrDefaultAsync(s => s.MealDayId == mealDayId && s.ClassId == classId, ct);
        });
    }

    public async Task<bool> MealDayIsActiveAndMatchesClassAsync(Guid mealDayId, Guid classId, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var day = await db.MealDays.AsNoTracking()
                .Select(m => new { m.Id, m.IsCancelled, m.SchoolYear })
                .FirstOrDefaultAsync(m => m.Id == mealDayId, ct);
            if (day is null || day.IsCancelled)
            {
                return false;
            }

            var room = await db.Classes.AsNoTracking()
                .Select(c => new { c.Id, c.SchoolYear })
                .FirstOrDefaultAsync(c => c.Id == classId, ct);
            if (room is null)
            {
                return false;
            }

            return day.SchoolYear is null || day.SchoolYear == room.SchoolYear;
        });
    }

    public async Task<ServingSettlementValidationStatus> ValidateSettlementAsync(Guid settlementId, Guid mealDayId, Guid classId, string snapshotToken, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var settlements = await db.PortionSettlements.AsNoTracking()
                .Where(s => s.MealDayId == mealDayId)
                .Select(s => new { s.Id, s.ClassId, s.Version })
                .ToListAsync(ct);

            var settlement = settlements.FirstOrDefault(s => s.Id == settlementId && (s.ClassId == null || s.ClassId == classId));
            if (settlement is null)
            {
                return ServingSettlementValidationStatus.Invalid;
            }

            var versions = settlements
                .Where(s => s.ClassId.HasValue)
                .GroupBy(s => s.ClassId!.Value)
                .OrderBy(g => g.Key)
                .Select(g => g.OrderByDescending(s => s.Version).First().Id.ToString("N"));
            var expectedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{mealDayId:N}:{string.Join(":", versions)}")));

            var latestForClass = settlements
                .Where(s => s.ClassId == classId)
                .OrderByDescending(s => s.Version)
                .FirstOrDefault();
            if (latestForClass is not null && settlement.Id != latestForClass.Id)
            {
                return ServingSettlementValidationStatus.Stale;
            }

            if (!string.Equals(snapshotToken, expectedToken, StringComparison.OrdinalIgnoreCase))
            {
                var wasAcknowledged = await db.SettlementAcknowledgements.AsNoTracking()
                    .AnyAsync(a => a.MealDayId == mealDayId && a.SnapshotToken == snapshotToken, ct);
                return wasAcknowledged
                    ? ServingSettlementValidationStatus.Stale
                    : ServingSettlementValidationStatus.Invalid;
            }

            var isCurrentAcknowledged = await db.SettlementAcknowledgements.AsNoTracking()
                .AnyAsync(a => a.MealDayId == mealDayId && a.SnapshotToken == expectedToken, ct);
            return isCurrentAcknowledged
                ? ServingSettlementValidationStatus.Valid
                : ServingSettlementValidationStatus.Invalid;
        });
    }

    public async Task<bool> RecipesExistAsync(Guid[] recipeVersionIds, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var distinctIds = recipeVersionIds.Distinct().ToArray();
            if (distinctIds.Length == 0)
            {
                return true;
            }

            if (distinctIds.Any(id => id == Guid.Empty))
            {
                return false;
            }

            var count = await db.RecipeVersions.AsNoTracking()
                .CountAsync(rv => distinctIds.Contains(rv.Id), ct);
            return count == distinctIds.Length;
        });
    }

    public async Task<bool> IngredientsExistAsync(Guid[] ingredientIds, (Guid IngredientId, Guid NutrientVersionId)[] nutrientPairs, CancellationToken ct = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var distinctIngredients = ingredientIds.Distinct().ToArray();
            if (distinctIngredients.Any(id => id == Guid.Empty))
            {
                return false;
            }

            if (distinctIngredients.Length > 0)
            {
                var count = await db.Ingredients.AsNoTracking()
                    .CountAsync(i => distinctIngredients.Contains(i.Id), ct);
                if (count != distinctIngredients.Length)
                {
                    return false;
                }
            }

            var distinctPairs = nutrientPairs.Distinct().ToArray();
            if (distinctPairs.Length == 0)
            {
                return true;
            }

            if (distinctPairs.Any(p => p.IngredientId == Guid.Empty || p.NutrientVersionId == Guid.Empty))
            {
                return false;
            }

            var versionIds = distinctPairs.Select(p => p.NutrientVersionId).Distinct().ToArray();
            var owners = await db.IngredientVersions.AsNoTracking()
                .Where(v => versionIds.Contains(v.Id))
                .Select(v => new { v.Id, v.IngredientId })
                .ToDictionaryAsync(v => v.Id, v => v.IngredientId, ct);

            return distinctPairs.All(p => owners.TryGetValue(p.NutrientVersionId, out var ownerId) && ownerId == p.IngredientId);
        });
    }

    public void AddServing(Serving serving) => db.Servings.Add(serving);

    public void AddServingVersion(ServingVersion version) => db.ServingVersions.Add(version);
}

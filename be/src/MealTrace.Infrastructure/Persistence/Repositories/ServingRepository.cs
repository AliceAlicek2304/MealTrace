using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MealTrace.Infrastructure.Persistence.Repositories
{
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

        public async Task<bool> MealDayExistsAsync(Guid mealDayId, CancellationToken ct = default)
        {
            return await PersistenceErrors.ExecuteAsync(async () =>
            {
                return await db.MealDays.AnyAsync(m => m.Id == mealDayId, ct);
            });
        }

        public async Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct = default)
        {
            return await PersistenceErrors.ExecuteAsync(async () =>
            {
                return await db.Classes.AnyAsync(c => c.Id == classId, ct);
            });
        }

        public async Task<bool> SettlementSnapshotMatchesAsync(Guid settlementId, string snapshotToken, CancellationToken ct = default)
        {
            return await PersistenceErrors.ExecuteAsync(async () =>
            {
                var settlement = await db.PortionSettlements.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == settlementId, ct);

                if (settlement is null )
                {
                    return false;
                }

                return await db.SettlementAcknowledgements.AsNoTracking()
                    .AnyAsync(a => a.MealDayId == settlement.MealDayId && a.SnapshotToken == snapshotToken, ct);
            });
        }

        public void AddServing(Serving serving) => db.Servings.Add(serving);

        public void AddServingVersion(ServingVersion version) => db.ServingVersions.Add(version);
    }
}

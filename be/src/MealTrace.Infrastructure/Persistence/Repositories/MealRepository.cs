using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class MealRepository(MealTraceDbContext db) : IMealRepository
{
    public async Task<List<MealDay>> ListMealDaysAsync(DateOnly? from, DateOnly? to)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var query = db.MealDays.AsNoTracking().AsQueryable();
            if (from is not null)
                query = query.Where(x => x.Date >= from);
            if (to is not null)
                query = query.Where(x => x.Date <= to);
            return await query.OrderByDescending(x => x.Date).Take(100).Include("Dishes.RecipeVersion.Recipe").Include("Settlements").AsSplitQuery().ToListAsync();
        });
    }
    public async Task<MealDay?> FindMealDayDetailsAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().Where(x => x.Id == id).Include("Dishes.RecipeVersion.Recipe").Include("Settlements").Include("Evidence").AsSplitQuery().FirstOrDefaultAsync();
        });
    }
    public async Task<ReportLineageResponse?> FindReportLineageAsync(Guid id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.ReportSnapshots.AsNoTracking().Where(x => x.Id == id).Select(x => new ReportLineageResponse
            {
                Id = x.Id,
                MealDayId = x.MealDayId,
                SettlementId = x.SettlementId,
                CreatedAt = x.CreatedAt,
                SignedAt = x.SignedAt,
                EnergyKcalPerPortion = x.EnergyKcalPerPortion,
                CostPerPortion = x.CostPerPortion,
                SourceJson = x.SourceJson
            }).FirstOrDefaultAsync();
        });
    }
}

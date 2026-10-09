using MealTrace.Application.Models.Persistence;
using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class MealRepository(MealTraceDbContext db) : IMealRepository
{
    private IQueryable<MealDay> FilterMealDays(MealDayListFilter filter)
    {
        var query = db.MealDays.AsNoTracking();
        if (filter.Date is not null) query = query.Where(x => x.Date == filter.Date);
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var term = filter.Search.ToLower();
            query = query.Where(x => x.MealType.ToLower().Contains(term)
                || x.Dishes.Any(d => d.RecipeVersion.Recipe.Name.ToLower().Contains(term)));
        }
        return filter.Status switch
        {
            "CANCELLED" => query.Where(x => x.IsCancelled),
            "SETTLED" => query.Where(x => !x.IsCancelled && x.Settlements.Any()),
            "PENDING" => query.Where(x => !x.IsCancelled && !x.Settlements.Any()),
            _ => query
        };
    }

    public Task<int> CountMealDaysAsync(MealDayListFilter filter, CancellationToken ct) =>
        PersistenceErrors.ExecuteAsync(() => FilterMealDays(filter).CountAsync(ct));

    public Task<List<MealDaySummary>> SearchMealDaysAsync(MealDayListFilter filter, int page, int size, CancellationToken ct) =>
        PersistenceErrors.ExecuteAsync(() => FilterMealDays(filter).OrderByDescending(x => x.Date).ThenBy(x => x.MealType).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).Select(x => new MealDaySummary
            {
                Id = x.Id, Date = x.Date, MealType = x.MealType, CutoffAt = x.CutoffAt,
                PublishedAt = x.PublishedAt, IsCancelled = x.IsCancelled, CancellationReason = x.CancellationReason,
                Dishes = x.Dishes.OrderBy(d => d.Id).Select(d => new MealDishSummary
                    { Id = d.Id, RecipeVersionId = d.RecipeVersionId, Name = d.RecipeVersion.Recipe.Name }).ToList(),
                SettledPortions = x.Settlements.Any(s => s.ClassId != null)
                    ? x.Settlements.Where(s => s.ClassId != null && !db.PortionSettlements.Any(newer => newer.MealDayId == x.Id
                        && newer.ClassId == s.ClassId && newer.Version > s.Version)).Sum(s => (int?)s.Count)
                    : x.Settlements.OrderByDescending(s => s.SettledAt).ThenBy(s => s.Id).Select(s => (int?)s.Count).FirstOrDefault()
            }).AsSplitQuery().ToListAsync(ct));

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

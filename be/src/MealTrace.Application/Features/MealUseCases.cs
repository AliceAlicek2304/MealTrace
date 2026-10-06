using MealTrace.Application.Dtos.Responses;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features;
public static class MealUseCases
{
    public static async Task<UseCaseResult> ListMealDaysAsync(DateOnly? from, DateOnly? to, IMealTraceData db)
    {
        var query = db.MealDays.AsNoTracking(db.Queries).AsQueryable();
        if (from is not null)
            query = query.Where(x => x.Date >= from);
        if (to is not null)
            query = query.Where(x => x.Date <= to);
        var days = await query.OrderByDescending(x => x.Date).Take(100).Include(db.Queries, "Dishes.RecipeVersion.Recipe").Include(db.Queries, "Settlements").AsSplitQuery(db.Queries).ToListAsync(db.Queries);
        return UseCaseResult.Ok(days.Select(x => new MealDaySummary
        {
            Id = x.Id,
            Date = x.Date,
            MealType = x.MealType,
            CutoffAt = x.CutoffAt,
            PublishedAt = x.PublishedAt,
            IsCancelled = x.IsCancelled,
            CancellationReason = x.CancellationReason,
            Dishes = x.Dishes.Select(d => new MealDishSummary
            {
                Id = d.Id,
                RecipeVersionId = d.RecipeVersionId,
                Name = d.RecipeVersion.Recipe.Name
            }).ToList(),
            SettledPortions = x.Settlements.Any(s => s.ClassId != null) ? x.Settlements.Where(s => s.ClassId != null).GroupBy(s => s.ClassId).Sum(group => (int?)group.OrderByDescending(s => s.Version).First().Count) : x.Settlements.OrderByDescending(s => s.SettledAt).Select(s => (int?)s.Count).FirstOrDefault()
        }).ToList());
    }

    public static async Task<UseCaseResult> GetMealDayAsync(Guid id, IMealTraceData db)
    {
        var day = await db.MealDays.AsNoTracking(db.Queries).Where(x => x.Id == id).Include(db.Queries, "Dishes.RecipeVersion.Recipe").Include(db.Queries, "Settlements").Include(db.Queries, "Evidence").AsSplitQuery(db.Queries).FirstOrDefaultAsync(db.Queries);
        if (day is null)
            return UseCaseResult.NotFound();
        return UseCaseResult.Ok(new MealDayDetail
        {
            Id = day.Id,
            Date = day.Date,
            MealType = day.MealType,
            CutoffAt = day.CutoffAt,
            PublishedAt = day.PublishedAt,
            IsCancelled = day.IsCancelled,
            CancellationReason = day.CancellationReason,
            SettledPortions = day.Settlements.Any(s => s.ClassId != null) ? day.Settlements.Where(s => s.ClassId != null).GroupBy(s => s.ClassId).Sum(group => (int?)group.OrderByDescending(s => s.Version).First().Count) : day.Settlements.OrderByDescending(s => s.SettledAt).Select(s => (int?)s.Count).FirstOrDefault(),
            Dishes = day.Dishes.Select(d => new MealDishSummary
            {
                Id = d.Id,
                RecipeVersionId = d.RecipeVersionId,
                Name = d.RecipeVersion.Recipe.Name
            }),
            Settlements = day.Settlements.OrderBy(s => s.SettledAt).Select(s => new SettlementSummary
            {
                Id = s.Id,
                ClassId = s.ClassId,
                ClassName = s.ClassName,
                Version = s.Version,
                Count = s.Count,
                SettledAt = s.SettledAt,
                SettledBy = s.SettledBy,
                Reason = s.Reason,
                SupersedesId = s.SupersedesId
            }),
            Evidence = day.Evidence.OrderBy(e => e.CapturedAt).Select(e => new MealEvidenceSummary
            {
                Id = e.Id,
                Kind = e.Kind,
                Description = e.Description,
                PhotoUrl = e.PhotoUrl,
                CapturedAt = e.CapturedAt,
                SyncedAt = e.SyncedAt,
                AmendsId = e.AmendsId
            })
        });
    }

    public static async Task<UseCaseResult> GetReportLineageAsync(Guid id, IMealTraceData db)
    {
        var report = await db.ReportSnapshots.AsNoTracking(db.Queries).Where(x => x.Id == id).Select(x => new ReportLineageResponse
        {
            Id = x.Id,
            MealDayId = x.MealDayId,
            SettlementId = x.SettlementId,
            CreatedAt = x.CreatedAt,
            SignedAt = x.SignedAt,
            EnergyKcalPerPortion = x.EnergyKcalPerPortion,
            CostPerPortion = x.CostPerPortion,
            SourceJson = x.SourceJson
        }).FirstOrDefaultAsync(db.Queries);
        return report is null ? UseCaseResult.NotFound() : UseCaseResult.Ok(report);
    }
}

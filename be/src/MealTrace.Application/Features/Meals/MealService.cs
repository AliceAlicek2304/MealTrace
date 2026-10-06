using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Dtos.Meals;

namespace MealTrace.Application.Features.Meals;
public sealed class MealService(IMealRepository repository)
{
    public async Task<Result<List<MealDaySummary>>> ListMealDaysAsync(DateOnly? from, DateOnly? to)
    {
        var days = await repository.ListMealDaysAsync(from, to);
        return Result.Success(days.Select(x => new MealDaySummary
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

    public async Task<Result<MealDayDetail>> GetMealDayAsync(Guid id)
    {
        var day = await repository.FindMealDayDetailsAsync(id);
        if (day is null)
            return Result.NotFound();
        return Result.Success(new MealDayDetail
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

    public async Task<Result<ReportLineageResponse>> GetReportLineageAsync(Guid id)
    {
        var report = await repository.FindReportLineageAsync(id);
        return report is null ? Result.NotFound() : Result.Success(report);
    }
}

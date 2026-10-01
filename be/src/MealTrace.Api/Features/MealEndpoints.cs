using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;
using MealTrace.Api.Security;

namespace MealTrace.Api.Features;

public static class MealEndpoints
{
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Meals").RequireAuthorization();

        api.MapGet("/meal-days", async (DateOnly? from, DateOnly? to, MealTraceDbContext db) =>
        {
            var query = db.MealDays.AsNoTracking().AsQueryable();
            if (from is not null) query = query.Where(x => x.Date >= from);
            if (to is not null) query = query.Where(x => x.Date <= to);
            var days = await query.OrderByDescending(x => x.Date).Take(100)
                .Include(x => x.Dishes).ThenInclude(x => x.RecipeVersion).ThenInclude(x => x.Recipe)
                .Include(x => x.Settlements).AsSplitQuery().ToListAsync();
            return days.Select(x => new
            {
                x.Id, x.Date, x.MealType, x.CutoffAt, x.PublishedAt, x.IsCancelled, x.CancellationReason,
                Dishes = x.Dishes.Select(d => new { d.Id, d.RecipeVersionId, Name = d.RecipeVersion.Recipe.Name }).ToList(),
                SettledPortions = x.Settlements.Any(s => s.ClassId != null)
                    ? x.Settlements.Where(s => s.ClassId != null).GroupBy(s => s.ClassId)
                        .Sum(group => (int?)group.OrderByDescending(s => s.Version).First().Count)
                    : x.Settlements.OrderByDescending(s => s.SettledAt).Select(s => (int?)s.Count).FirstOrDefault()
            }).ToList();
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("ListMealDays");

        api.MapGet("/meal-days/{id:guid}", async (Guid id, MealTraceDbContext db) =>
        {
            var day = await db.MealDays.AsNoTracking().Where(x => x.Id == id)
                .Include(x => x.Dishes).ThenInclude(x => x.RecipeVersion).ThenInclude(x => x.Recipe)
                .Include(x => x.Settlements).Include(x => x.Evidence).AsSplitQuery().FirstOrDefaultAsync();
            if (day is null) return Results.NotFound();
            return Results.Ok(new
            {
                day.Id, day.Date, day.MealType, day.CutoffAt, day.PublishedAt, day.IsCancelled, day.CancellationReason,
                SettledPortions = day.Settlements.Any(s => s.ClassId != null)
                    ? day.Settlements.Where(s => s.ClassId != null).GroupBy(s => s.ClassId)
                        .Sum(group => (int?)group.OrderByDescending(s => s.Version).First().Count)
                    : day.Settlements.OrderByDescending(s => s.SettledAt).Select(s => (int?)s.Count).FirstOrDefault(),
                Dishes = day.Dishes.Select(d => new { d.Id, d.RecipeVersionId, Name = d.RecipeVersion.Recipe.Name }),
                Settlements = day.Settlements.OrderBy(s => s.SettledAt).Select(s => new
                { s.Id, s.ClassId, s.ClassName, s.Version, s.Count, s.SettledAt, s.SettledBy, s.Reason, s.SupersedesId }),
                Evidence = day.Evidence.OrderBy(e => e.CapturedAt).Select(e => new
                { e.Id, e.Kind, e.Description, e.PhotoUrl, e.CapturedAt, e.SyncedAt, e.AmendsId })
            });
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("GetMealDay");

        api.MapGet("/reports/{id:guid}/lineage", async (Guid id, MealTraceDbContext db) =>
        {
            var report = await db.ReportSnapshots.AsNoTracking().Where(x => x.Id == id).Select(x => new
            {
                x.Id, x.MealDayId, x.SettlementId, x.CreatedAt, x.SignedAt,
                x.EnergyKcalPerPortion, x.CostPerPortion, x.SourceJson
            }).FirstOrDefaultAsync();
            return report is null ? Results.NotFound() : Results.Ok(report);
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.ReportReaders)).WithName("GetReportLineage");

        return app;
    }
}

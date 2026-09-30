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
            return await query.OrderBy(x => x.Date).Take(100).Select(x => new
            {
                x.Id, x.Date, x.MealType, x.CutoffAt, x.PublishedAt,
                Dishes = x.Dishes.Select(d => new { d.Id, d.RecipeVersionId, Name = d.RecipeVersion.Recipe.Name }),
                SettledPortions = x.Settlements.OrderByDescending(s => s.SettledAt)
                    .Select(s => (int?)s.Count).FirstOrDefault()
            }).ToListAsync();
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("ListMealDays");

        api.MapGet("/meal-days/{id:guid}", async (Guid id, MealTraceDbContext db) =>
        {
            var day = await db.MealDays.AsNoTracking().Where(x => x.Id == id).Select(x => new
            {
                x.Id, x.Date, x.MealType, x.CutoffAt, x.PublishedAt,
                Dishes = x.Dishes.Select(d => new { d.Id, d.RecipeVersionId, Name = d.RecipeVersion.Recipe.Name }),
                Settlements = x.Settlements.OrderBy(s => s.SettledAt).Select(s => new
                { s.Id, s.Count, s.SettledAt, s.SettledBy, s.Reason, s.SupersedesId }),
                Evidence = x.Evidence.OrderBy(e => e.CapturedAt).Select(e => new
                { e.Id, e.Kind, e.Description, e.PhotoUrl, e.CapturedAt, e.SyncedAt, e.AmendsId })
            }).FirstOrDefaultAsync();
            return day is null ? Results.NotFound() : Results.Ok(day);
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

using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Features.Meals;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Meals;
namespace MealTrace.Api.Features;

public static class MealEndpoints
{
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Meals").RequireAuthorization();

        api.MapGet("/meal-days/search", async (DateOnly? date, string? status, string? search, int? page, int? pageSize, CancellationToken ct, MealService service) => (await service.SearchMealDaysAsync(date, status, search, page, pageSize, ct)).ToHttpResult()).Produces<PageResponse<MealDaySummary>>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("SearchMealDays");

        api.MapGet("/meal-days", async (DateOnly? from, DateOnly? to, MealService service) => (await service.ListMealDaysAsync(from, to)).ToHttpResult()).Produces<MealDaySummary[]>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("ListMealDays");

        api.MapGet("/meal-days/{id:guid}", async (Guid id, MealService service) => (await service.GetMealDayAsync(id)).ToHttpResult()).Produces<MealDayDetail>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("GetMealDay");

        api.MapGet("/reports/{id:guid}/lineage", async (Guid id, MealService service) => (await service.GetReportLineageAsync(id)).ToHttpResult()).Produces<ReportLineageResponse>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.ReportReaders)).WithName("GetReportLineage");

        return app;
    }
}

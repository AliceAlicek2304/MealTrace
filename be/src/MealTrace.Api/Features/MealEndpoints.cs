using MealTrace.Application.Features.Meals;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Meals;
namespace MealTrace.Api.Features;

public static class MealEndpoints
{
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Meals").RequireAuthorization();

        api.MapGet("/meal-days", async (DateOnly? from, DateOnly? to, MealUseCases service) => (await service.ListMealDaysAsync(from, to)).ToHttpResult()).Produces<MealDaySummary[]>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("ListMealDays");

        api.MapGet("/meal-days/{id:guid}", async (Guid id, MealUseCases service) => (await service.GetMealDayAsync(id)).ToHttpResult()).Produces<MealDayDetail>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("GetMealDay");

        api.MapGet("/reports/{id:guid}/lineage", async (Guid id, MealUseCases service) => (await service.GetReportLineageAsync(id)).ToHttpResult()).Produces<ReportLineageResponse>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.ReportReaders)).WithName("GetReportLineage");

        return app;
    }
}

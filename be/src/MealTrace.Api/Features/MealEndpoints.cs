using MealTrace.Application.Dtos.Responses;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using MealTrace.Domain.Security;

namespace MealTrace.Api.Features;

public static class MealEndpoints
{
    public static IEndpointRouteBuilder MapMealEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Meals").RequireAuthorization();

        api.MapGet("/meal-days", async (DateOnly? from, DateOnly? to, IMealTraceData db) => (await MealUseCases.ListMealDaysAsync(from, to, db)).ToHttpResult()).Produces<MealDaySummary[]>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("ListMealDays");

        api.MapGet("/meal-days/{id:guid}", async (Guid id, IMealTraceData db) => (await MealUseCases.GetMealDayAsync(id, db)).ToHttpResult()).Produces<MealDayDetail>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.MealStaff)).WithName("GetMealDay");

        api.MapGet("/reports/{id:guid}/lineage", async (Guid id, IMealTraceData db) => (await MealUseCases.GetReportLineageAsync(id, db)).ToHttpResult()).Produces<ReportLineageResponse>(StatusCodes.Status200OK).RequireAuthorization(policy => policy.RequireRole(RoleNames.ReportReaders)).WithName("GetReportLineage");

        return app;
    }
}

using MealTrace.Application.Dtos.Responses;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Meals;

namespace MealTrace.Api.Features;

public static class MealExceptionEndpoints
{
    public static IEndpointRouteBuilder MapMealExceptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meal-days").WithTags("Meal exceptions")
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConcurrencyConflict(ex)) { return MealExceptionUseCases.Stale().ToHttpResult(); }
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConflict(ex)) { return MealExceptionUseCases.Stale().ToHttpResult(); }
        });

        group.MapGet("/{id:guid}/decisions", async (Guid id, Guid? classId, string? search, int? page, int? pageSize,
            ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock, CancellationToken ct) => (await MealExceptionUseCases.GetDecisionsAsync(id, classId, search, page, pageSize, principal, db, clock, ct)).ToHttpResult()).Produces<MealDecisionsResponse>(StatusCodes.Status200OK).WithName("MealDecisions");

        group.MapGet("/{id:guid}/students/{studentId:guid}/exceptions", async (Guid id, Guid studentId, int? page, int? pageSize,
            ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await MealExceptionUseCases.GetStudentHistoryAsync(id, studentId, page, pageSize, principal, db, clock)).ToHttpResult()).Produces<MealExceptionHistoryResponse>(StatusCodes.Status200OK).WithName("MealExceptionHistory");

        group.MapPost("/{id:guid}/exceptions", async (Guid id, ExceptionInput input, ClaimsPrincipal principal,
            IMealTraceData db, TimeProvider clock) => (await MealExceptionUseCases.RecordExceptionAsync(id, input, principal, db, clock)).ToHttpResult()).Produces<MealExceptionCreatedResponse>(StatusCodes.Status200OK).WithName("RecordMealException");
        return app;
    }
}

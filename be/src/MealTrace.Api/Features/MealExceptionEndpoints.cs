using MealTrace.Application.Features.Meals;
using MealTrace.Application.Exceptions;
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
            catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.StaleWrite }) { return MealExceptionUseCases.Stale().ToHttpResult(); }
            catch (Exception ex) when (ex is PersistenceConflictException) { return MealExceptionUseCases.Stale().ToHttpResult(); }
        });

        group.MapGet("/{id:guid}/decisions", async (Guid id, Guid? classId, string? search, int? page, int? pageSize, CancellationToken ct, MealExceptionUseCases service) => (await service.GetDecisionsAsync(id, classId, search, page, pageSize, ct)).ToHttpResult()).Produces<MealDecisionsResponse>(StatusCodes.Status200OK).WithName("MealDecisions");

        group.MapGet("/{id:guid}/students/{studentId:guid}/exceptions", async (Guid id, Guid studentId, int? page, int? pageSize, MealExceptionUseCases service) => (await service.GetStudentHistoryAsync(id, studentId, page, pageSize)).ToHttpResult()).Produces<MealExceptionHistoryResponse>(StatusCodes.Status200OK).WithName("MealExceptionHistory");

        group.MapPost("/{id:guid}/exceptions", async (Guid id, ExceptionInput input, MealExceptionUseCases service) => (await service.RecordExceptionAsync(id, input)).ToHttpResult()).Produces<MealExceptionCreatedResponse>(StatusCodes.Status200OK).WithName("RecordMealException");
        return app;
    }
}

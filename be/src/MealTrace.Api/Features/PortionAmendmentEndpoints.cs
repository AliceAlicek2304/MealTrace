using MealTrace.Application.Features.Portions;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Dtos.Common;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Portions;

namespace MealTrace.Api.Features;

public static class PortionAmendmentEndpoints
{
    public static IEndpointRouteBuilder MapPortionAmendmentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/meal-days/{dayId:guid}/amendments").WithTags("Portion amendments")
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));
        api.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (Exception ex) when (ex is PersistenceConflictException)
            { return Results.Conflict(new MessageResponse("Dữ liệu vừa thay đổi. Tải lại bản suất và đối chiếu lại yêu cầu.")); }
        });

        api.MapGet("", async (Guid dayId, Guid classId, string? q, int? page, int? candidatePage, PortionAmendmentUseCases service) => (await service.GetClassAmendmentsAsync(dayId, classId, q, page, candidatePage)).ToHttpResult()).Produces<ClassAmendmentsResponse>(StatusCodes.Status200OK);

        api.MapPost("", async (Guid dayId, RequestInput input, PortionAmendmentUseCases service) => (await service.RequestAmendmentAsync(dayId, input)).ToHttpResult()).Produces<AmendmentCreatedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapGet("/{requestId:guid}", async (Guid dayId, Guid requestId, PortionAmendmentUseCases service) => (await service.GetAmendmentAsync(dayId, requestId)).ToHttpResult()).Produces<AmendmentComparisonResponse>(StatusCodes.Status200OK);

        api.MapPost("/{requestId:guid}/review", async (Guid dayId, Guid requestId, ReviewInput input, PortionAmendmentUseCases service) => (await service.ReviewAmendmentAsync(dayId, requestId, input)).ToHttpResult()).Produces<AmendmentReviewedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        return app;
    }
}

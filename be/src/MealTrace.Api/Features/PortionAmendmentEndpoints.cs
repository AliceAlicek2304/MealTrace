using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
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
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConflict(ex))
            { return Results.Conflict(new MessageResponse("Dữ liệu vừa thay đổi. Tải lại bản suất và đối chiếu lại yêu cầu.")); }
        });

        api.MapGet("", async (Guid dayId, Guid classId, string? q, int? page, int? candidatePage,
            ClaimsPrincipal principal, IMealTraceData db) => (await PortionAmendmentUseCases.GetClassAmendmentsAsync(dayId, classId, q, page, candidatePage, principal, db)).ToHttpResult()).Produces<ClassAmendmentsResponse>(StatusCodes.Status200OK);

        api.MapPost("", async (Guid dayId, RequestInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await PortionAmendmentUseCases.RequestAmendmentAsync(dayId, input, principal, db, clock)).ToHttpResult()).Produces<AmendmentCreatedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapGet("/{requestId:guid}", async (Guid dayId, Guid requestId, ClaimsPrincipal principal, IMealTraceData db) => (await PortionAmendmentUseCases.GetAmendmentAsync(dayId, requestId, principal, db)).ToHttpResult()).Produces<AmendmentComparisonResponse>(StatusCodes.Status200OK);

        api.MapPost("/{requestId:guid}/review", async (Guid dayId, Guid requestId, ReviewInput input,
            ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await PortionAmendmentUseCases.ReviewAmendmentAsync(dayId, requestId, input, principal, db, clock)).ToHttpResult()).Produces<AmendmentReviewedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        return app;
    }
}

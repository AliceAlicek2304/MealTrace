using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Students;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Features.Students;
using MealTrace.Domain.Security;
namespace MealTrace.Api.Features;

public static class ParentLinkEndpoints
{
    public static IEndpointRouteBuilder MapParentLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var parent = app.MapGroup("/api/parent/link-requests").WithTags("Parent child links").RequireAuthorization(p => p.RequireRole(RoleNames.Parent));
        var staff = app.MapGroup("/api/student-link-requests").WithTags("Parent child links").RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));
        foreach (var group in new[] { parent, staff }) group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (PersistenceConflictException) { return Results.Conflict(new MessageResponse("Yêu cầu hoặc liên kết đã thay đổi. Vui lòng tải lại.")); }
        });
        parent.MapGet("/classes", async (ParentLinkService service) => (await service.ClassesAsync(false)).ToHttpResult()).Produces<List<ParentLinkClass>>().WithName("ParentLinkClassChoices");
        parent.MapGet("", async (string? status, int? page, int? pageSize, ParentLinkService service) => (await service.ListAsync(false, status, page, pageSize)).ToHttpResult()).Produces<ParentLinkRequestPage>().WithName("ParentLinkRequests");
        parent.MapPost("", async (CreateParentLinkRequest input, ParentLinkService service) => (await service.CreateAsync(input)).ToHttpResult()).RequireRateLimiting("parentLinks").Produces(StatusCodes.Status204NoContent).WithName("CreateParentLinkRequest");
        parent.MapPost("/{id:guid}/cancel", async (Guid id, CancelParentLinkRequest input, ParentLinkService service) => (await service.CancelAsync(id, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("CancelParentLinkRequest");
        staff.MapGet("", async (string? status, int? page, int? pageSize, Guid? classId, string? schoolYear, ParentLinkService service) => (await service.ListAsync(true, status, page, pageSize, classId, schoolYear)).ToHttpResult()).Produces<ParentLinkRequestPage>().WithName("ReviewableParentLinkRequests");
        staff.MapPost("/{id:guid}/review", async (Guid id, ReviewParentLinkRequest input, ParentLinkService service) => (await service.ReviewAsync(id, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("ReviewParentLinkRequest");
        staff.MapGet("/classes", async (ParentLinkService service) => (await service.ClassesAsync()).ToHttpResult()).Produces<List<ParentLinkClass>>().WithName("ParentLinkClasses");
        staff.MapPost("/bulk-review", async (BulkReviewParentLinks input, ParentLinkService service) => (await service.BulkReviewAsync(input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("BulkReviewParentLinks");
        staff.MapPost("/{id:guid}/revoke", async (Guid id, RevokeParentLink input, ParentLinkService service) => (await service.RevokeAsync(id, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("RevokeParentLink");
        return app;
    }
}

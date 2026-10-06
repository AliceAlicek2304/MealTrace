using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Api.Features;

public static class StudentAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapStudentAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Classes and enrollment")
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConcurrencyConflict(ex)) { return StudentAdministrationUseCases.Conflict().ToHttpResult(); }
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConflict(ex))
            { return Results.Conflict(new MessageResponse("Mã trẻ, lớp hoặc ngày ghi danh đã tồn tại. Hãy tải lại dữ liệu.")); }
        });

        group.MapGet("/classes", async (string? search, int? page, int? pageSize, IMealTraceData db, TimeProvider clock) => (await StudentAdministrationUseCases.SearchClassesAsync(search, page, pageSize, db, clock)).ToHttpResult()).Produces<ClassListResponse>(StatusCodes.Status200OK).WithName("SearchClasses");

        group.MapPut("/classes/{id:guid}", async (Guid id, EditClass input, IMealTraceData db) => (await StudentAdministrationUseCases.EditClassAsync(id, input, db)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("EditClass");

        group.MapGet("/students", async (Guid? classId, string? search, string? status, int? page, int? pageSize, IMealTraceData db, TimeProvider clock) => (await StudentAdministrationUseCases.SearchStudentsAsync(classId, search, status, page, pageSize, db, clock)).ToHttpResult()).Produces<StudentListResponse>(StatusCodes.Status200OK).WithName("SearchStudents");

        group.MapPut("/students/{id:guid}", async (Guid id, EditStudent input, IMealTraceData db) => (await StudentAdministrationUseCases.EditStudentAsync(id, input, db)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("EditStudent");

        group.MapGet("/students/{id:guid}/enrollments", async (Guid id, IMealTraceData db) => (await StudentAdministrationUseCases.GetEnrollmentHistoryAsync(id, db)).ToHttpResult()).Produces<EnrollmentHistoryItem[]>(StatusCodes.Status200OK).WithName("StudentEnrollmentHistory");

        // ClassId null means withdrawal; otherwise transfer or re-enrollment. Parent links stay attached to the student.
        group.MapPost("/students/{id:guid}/enrollments", async (Guid id, ChangeEnrollment input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await StudentAdministrationUseCases.ChangeEnrollmentAsync(id, input, principal, db, clock)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("ChangeStudentEnrollment");
        return app;
    }
}

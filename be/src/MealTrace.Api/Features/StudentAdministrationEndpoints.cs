using MealTrace.Application.Features.Students;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Dtos.Common;
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
            catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.StaleWrite }) { return StudentAdministrationService.Conflict().ToHttpResult(); }
            catch (Exception ex) when (ex is PersistenceConflictException)
            { return Results.Conflict(new MessageResponse("Mã trẻ, lớp hoặc ngày ghi danh đã tồn tại. Hãy tải lại dữ liệu.")); }
        });

        group.MapGet("/classes", async (string? search, int? page, int? pageSize, StudentAdministrationService service) => (await service.SearchClassesAsync(search, page, pageSize)).ToHttpResult()).Produces<ClassListResponse>(StatusCodes.Status200OK).WithName("SearchClasses");

        group.MapPut("/classes/{id:guid}", async (Guid id, EditClass input, StudentAdministrationService service) => (await service.EditClassAsync(id, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("EditClass");

        group.MapGet("/students", async (Guid? classId, string? search, string? status, string? parentStatus, int? page, int? pageSize, StudentAdministrationService service) => (await service.SearchStudentsAsync(classId, search, status, page, pageSize, parentStatus)).ToHttpResult()).Produces<StudentListResponse>(StatusCodes.Status200OK).WithName("SearchStudents");

        app.MapGet("/api/students/search", async (Guid? classId, string? search, string? status, string? parentStatus, int? page, int? pageSize, StudentAdministrationService service) => (await service.SearchStudentsAsync(classId, search, status, page, pageSize, parentStatus)).ToHttpResult())
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher))
            .AddEndpointFilter(async (context, next) => { context.HttpContext.Response.Headers.CacheControl = "no-store"; return await next(context); })
            .Produces<StudentListResponse>(StatusCodes.Status200OK).WithTags("Classes and enrollment").WithName("SearchScopedStudents");

        group.MapPut("/students/{id:guid}", async (Guid id, EditStudent input, StudentAdministrationService service) => (await service.EditStudentAsync(id, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("EditStudent");

        group.MapGet("/students/{id:guid}/enrollments", async (Guid id, StudentAdministrationService service) => (await service.GetEnrollmentHistoryAsync(id)).ToHttpResult()).Produces<EnrollmentHistoryItem[]>(StatusCodes.Status200OK).WithName("StudentEnrollmentHistory");

        // ClassId null means withdrawal; otherwise transfer or re-enrollment. Parent links stay attached to the student.
        group.MapPost("/students/{id:guid}/enrollments", async (Guid id, ChangeEnrollment input, StudentAdministrationService service) => (await service.ChangeEnrollmentAsync(id, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).WithName("ChangeStudentEnrollment");
        return app;
    }
}

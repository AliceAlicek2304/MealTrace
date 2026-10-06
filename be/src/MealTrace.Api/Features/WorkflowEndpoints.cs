using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Workflow;

namespace MealTrace.Api.Features;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().WithTags("Meal workflow");
        api.MapGet("/academic-years", async (IMealTraceData db) => (await WorkflowUseCases.ListAcademicYearsAsync(db)).ToHttpResult()).Produces<AcademicYearResponse[]>(StatusCodes.Status200OK);
        api.MapGet("/admin/academic-years", async (IMealTraceData db) => (await WorkflowUseCases.ListYearConfigurationAsync(db)).ToHttpResult()).Produces<AcademicYearConfiguration[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        api.MapGet("/admin/academic-years/{code}/next", async (string code, IMealTraceData db) => (await WorkflowUseCases.PreviewNextYearAsync(code, db)).ToHttpResult()).Produces<NextAcademicYearResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin)).WithName("PreviewNextAcademicYear");
        api.MapPut("/admin/academic-years/{code}", async (string code, YearDates input, IMealTraceData db) => (await WorkflowUseCases.ConfigureYearAsync(code, input, db)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).RequireAuthorization(p => p.RequireRole(RoleNames.Admin)).WithName("ConfigureAcademicYearDates");
        api.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConflict(ex))
            { return Results.Conflict(new MessageResponse("Dữ liệu trùng hoặc vừa thay đổi. Hãy tải lại và thử lại.")); }
        });

        api.MapGet("/classes", async (ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.ListClassesAsync(principal, db, clock)).ToHttpResult()).Produces<ClassSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/classes", async (CreateClass input, IMealTraceData db) => (await WorkflowUseCases.CreateClassAsync(input, db)).ToHttpResult()).Produces<ClassCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/classes/{classId:guid}/students", async (Guid classId, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.ListClassStudentsAsync(classId, principal, db, clock)).ToHttpResult()).Produces<ClassStudentSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapPost("/students", async (CreateStudent input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.CreateStudentAsync(input, principal, db, clock)).ToHttpResult()).Produces<StudentCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapPost("/admin/students/{studentId:guid}/parents", async (Guid studentId, LinkParent input,
            IMealTraceData db, IIdentityService users) => (await WorkflowUseCases.LinkParentAsync(studentId, input, db, users)).ToHttpResult()).Produces<ParentLinkedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/parent/students", async (ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.ListParentStudentsAsync(principal, db, clock)).ToHttpResult()).Produces<ParentChildSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences", async (ReportAbsence input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.ReportAbsenceAsync(input, principal, db, clock)).ToHttpResult()).Produces<AbsenceCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        // Cancel the original and append a replacement so decisions at an earlier cutoff keep their original dates.
        api.MapPost("/parent/absences/{id:guid}/replace", async (Guid id, ReportAbsence input, ClaimsPrincipal principal,
            IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.ReplaceAbsenceAsync(id, input, principal, db, clock)).ToHttpResult()).Produces<AbsenceReplacedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Parent)).WithName("UpdateMealAbsencePeriod");

        api.MapGet("/parent/absences", async (ClaimsPrincipal principal, IMealTraceData db) => (await WorkflowUseCases.ListAbsencesAsync(principal, db)).ToHttpResult()).Produces<AbsenceSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences/{id:guid}/cancel", async (Guid id, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.CancelAbsenceAsync(id, principal, db, clock)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/meal-days", async (CreateMealDay input, IMealTraceData db, TimeProvider clock) => (await WorkflowUseCases.CreateMealDayAsync(input, db, clock)).ToHttpResult()).Produces<MealDayCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/meal-days/workflow", async (DateOnly? date, int? page, IMealTraceData db) => (await WorkflowUseCases.ListWorkflowDaysAsync(date, page, db)).ToHttpResult()).Produces<WorkflowMealDayListResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapGet("/meal-days/{id:guid}/portions", async (Guid id, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock, CancellationToken ct) => (await WorkflowUseCases.GetPortionsAsync(id, principal, db, clock, ct)).ToHttpResult()).Produces<MealPortionsResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/meal-days/{id:guid}/settle", async (Guid id, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock, CancellationToken ct) => (await WorkflowUseCases.SettlePortionsAsync(id, principal, db, clock, ct)).ToHttpResult()).Produces<PortionsSettledResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        return app;
    }
}

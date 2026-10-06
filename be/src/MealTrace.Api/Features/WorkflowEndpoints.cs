using MealTrace.Application.Features.Workflow;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Dtos.Common;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Workflow;
using MealTrace.Application.Dtos.Students;
namespace MealTrace.Api.Features;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().WithTags("Meal workflow");
        api.MapGet("/academic-years", async (WorkflowUseCases service) => (await service.ListAcademicYearsAsync()).ToHttpResult()).Produces<AcademicYearResponse[]>(StatusCodes.Status200OK);
        api.MapGet("/admin/academic-years", async (WorkflowUseCases service) => (await service.ListYearConfigurationAsync()).ToHttpResult()).Produces<AcademicYearConfiguration[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        api.MapGet("/admin/academic-years/{code}/next", async (string code, WorkflowUseCases service) => (await service.PreviewNextYearAsync(code)).ToHttpResult()).Produces<NextAcademicYearResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin)).WithName("PreviewNextAcademicYear");
        api.MapPut("/admin/academic-years/{code}", async (string code, YearDates input, WorkflowUseCases service) => (await service.ConfigureYearAsync(code, input)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).RequireAuthorization(p => p.RequireRole(RoleNames.Admin)).WithName("ConfigureAcademicYearDates");
        api.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (Exception ex) when (ex is PersistenceConflictException)
            { return Results.Conflict(new MessageResponse("Dữ liệu trùng hoặc vừa thay đổi. Hãy tải lại và thử lại.")); }
        });

        api.MapGet("/classes", async (WorkflowUseCases service) => (await service.ListClassesAsync()).ToHttpResult()).Produces<ClassSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/classes", async (CreateClass input, WorkflowUseCases service) => (await service.CreateClassAsync(input)).ToCreatedHttpResult(value => $"/api/classes/{value.Id}")).Produces<ClassCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/classes/{classId:guid}/students", async (Guid classId, WorkflowUseCases service) => (await service.ListClassStudentsAsync(classId)).ToHttpResult()).Produces<ClassStudentSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapPost("/students", async (CreateStudent input, WorkflowUseCases service) => (await service.CreateStudentAsync(input)).ToCreatedHttpResult(value => $"/api/students/{value.Id}")).Produces<StudentCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapPost("/admin/students/{studentId:guid}/parents", async (Guid studentId, LinkParent input, WorkflowUseCases service) => (await service.LinkParentAsync(studentId, input)).ToHttpResult()).Produces<ParentLinkedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/parent/students", async (WorkflowUseCases service) => (await service.ListParentStudentsAsync()).ToHttpResult()).Produces<ParentChildSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences", async (ReportAbsence input, WorkflowUseCases service) => (await service.ReportAbsenceAsync(input)).ToCreatedHttpResult(value => $"/api/parent/absences/{value.Id}")).Produces<AbsenceCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        // Cancel the original and append a replacement so decisions at an earlier cutoff keep their original dates.
        api.MapPost("/parent/absences/{id:guid}/replace", async (Guid id, ReportAbsence input, WorkflowUseCases service) => (await service.ReplaceAbsenceAsync(id, input)).ToHttpResult()).Produces<AbsenceReplacedResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Parent)).WithName("UpdateMealAbsencePeriod");

        api.MapGet("/parent/absences", async (WorkflowUseCases service) => (await service.ListAbsencesAsync()).ToHttpResult()).Produces<AbsenceSummary[]>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences/{id:guid}/cancel", async (Guid id, WorkflowUseCases service) => (await service.CancelAbsenceAsync(id)).ToHttpResult()).Produces(StatusCodes.Status204NoContent).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/meal-days", async (CreateMealDay input, WorkflowUseCases service) => (await service.CreateMealDayAsync(input)).ToCreatedHttpResult(value => $"/api/meal-days/{value.Id}")).Produces<MealDayCreatedResponse>(StatusCodes.Status201Created).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/meal-days/workflow", async (DateOnly? date, int? page, WorkflowUseCases service) => (await service.ListWorkflowDaysAsync(date, page)).ToHttpResult()).Produces<WorkflowMealDayListResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapGet("/meal-days/{id:guid}/portions", async (Guid id, CancellationToken ct, WorkflowUseCases service) => (await service.GetPortionsAsync(id, ct)).ToHttpResult()).Produces<MealPortionsResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/meal-days/{id:guid}/settle", async (Guid id, CancellationToken ct, WorkflowUseCases service) => (await service.SettlePortionsAsync(id, ct)).ToHttpResult()).Produces<PortionsSettledResponse>(StatusCodes.Status200OK).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        return app;
    }
}

using MealTrace.Application.Features.Calendar;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Exceptions;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Calendar;

namespace MealTrace.Api.Features;

public static class MealCalendarEndpoints
{
    public static IEndpointRouteBuilder MapMealCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/meal-calendar").WithTags("Meal calendar").RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (CalendarConflict ex) { return Results.Conflict(new MessageResponse(ex.Message)); }
            catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.StaleWrite }) { return Results.Conflict(new MessageResponse("Lịch vừa thay đổi. Tải lại và mở lại form.")); }
            catch (Exception ex) when (ex is PersistenceConflictException) { return Results.Conflict(new MessageResponse("Dữ liệu vừa thay đổi. Tải lại và xem trước lại.")); }
        });

        group.MapGet("/{code}", async (string code, DateOnly? from, DateOnly? to, MealCalendarService service) => (await service.GetCalendarAsync(code, from, to)).ToHttpResult()).Produces<MealCalendarResponse>(StatusCodes.Status200OK).WithName("ReadMealCalendar");

        group.MapPut("/{code}/schedule", async (string code, ScheduleInput input, MealCalendarService service) => (await service.SaveScheduleAsync(code, input)).ToHttpResult()).Produces<CalendarRevisionResponse>(StatusCodes.Status200OK).WithName("SaveMealSchedule");

        group.MapPut("/{code}/days/{date}", async (string code, DateOnly date, DayInput input, MealCalendarService service) => (await service.EditDayAsync(code, date, input)).ToHttpResult()).Produces<CalendarRevisionResponse>(StatusCodes.Status200OK).WithName("EditMealCalendarDay");

        group.MapPost("/{code}/preview", async (string code, GenerateInput input, MealCalendarService service) => (await service.PreviewGenerationAsync(code, input)).ToHttpResult()).Produces<CalendarGenerationPreview>(StatusCodes.Status200OK).WithName("PreviewMealGeneration");

        group.MapPost("/{code}/generate", async (string code, GenerateInput input, MealCalendarService service) => (await service.GenerateSessionsAsync(code, input)).ToHttpResult()).Produces<CalendarGenerationResponse>(StatusCodes.Status200OK).WithName("GenerateMealSessions");

        group.MapGet("/{code}/history", async (string code, MealCalendarService service) => (await service.GetHistoryAsync(code)).ToHttpResult()).Produces<CalendarHistoryItem[]>(StatusCodes.Status200OK).WithName("MealCalendarHistory");
        return app;
    }
}

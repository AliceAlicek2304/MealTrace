using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Exceptions;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
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
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConcurrencyConflict(ex)) { return Results.Conflict(new MessageResponse("Lịch vừa thay đổi. Tải lại và mở lại form.")); }
            catch (Exception ex) when (context.HttpContext.RequestServices.GetRequiredService<IMealTraceData>().IsConflict(ex)) { return Results.Conflict(new MessageResponse("Dữ liệu vừa thay đổi. Tải lại và xem trước lại.")); }
        });

        group.MapGet("/{code}", async (string code, DateOnly? from, DateOnly? to, IMealTraceData db, TimeProvider clock) => (await MealCalendarUseCases.GetCalendarAsync(code, from, to, db, clock)).ToHttpResult()).Produces<MealCalendarResponse>(StatusCodes.Status200OK).WithName("ReadMealCalendar");

        group.MapPut("/{code}/schedule", async (string code, ScheduleInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await MealCalendarUseCases.SaveScheduleAsync(code, input, principal, db, clock)).ToHttpResult()).Produces<CalendarRevisionResponse>(StatusCodes.Status200OK).WithName("SaveMealSchedule");

        group.MapPut("/{code}/days/{date}", async (string code, DateOnly date, DayInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await MealCalendarUseCases.EditDayAsync(code, date, input, principal, db, clock)).ToHttpResult()).Produces<CalendarRevisionResponse>(StatusCodes.Status200OK).WithName("EditMealCalendarDay");

        group.MapPost("/{code}/preview", async (string code, GenerateInput input, IMealTraceData db, TimeProvider clock) => (await MealCalendarUseCases.PreviewGenerationAsync(code, input, db, clock)).ToHttpResult()).Produces<CalendarGenerationPreview>(StatusCodes.Status200OK).WithName("PreviewMealGeneration");

        group.MapPost("/{code}/generate", async (string code, GenerateInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock) => (await MealCalendarUseCases.GenerateSessionsAsync(code, input, principal, db, clock)).ToHttpResult()).Produces<CalendarGenerationResponse>(StatusCodes.Status200OK).WithName("GenerateMealSessions");

        group.MapGet("/{code}/history", async (string code, IMealTraceData db) => (await MealCalendarUseCases.GetHistoryAsync(code, db)).ToHttpResult()).Produces<CalendarHistoryItem[]>(StatusCodes.Status200OK).WithName("MealCalendarHistory");
        return app;
    }
}

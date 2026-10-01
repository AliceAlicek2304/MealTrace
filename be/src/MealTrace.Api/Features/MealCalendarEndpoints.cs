using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public static class MealCalendarEndpoints
{
    public sealed record ScheduleInput(int[] Weekdays, string[] MealTypes, int ExpectedRevision, string Reason);
    public sealed record DayInput(string Mode, string[] MealTypes, int ExpectedRevision, string Reason);
    public sealed record GenerateInput(DateOnly From, DateOnly To, int ExpectedRevision, string? PreviewToken = null);
    private sealed record PlanItem(DateOnly Date, string MealType, string Action, Guid? MealId = null);
    private sealed class CalendarConflict(string message) : Exception(message);

    public static DateTimeOffset Cutoff(DateOnly date) => new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 30)), TimeSpan.FromHours(7)).ToUniversalTime();
    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
    private static string[] Types(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    private static string[]? NormalizeTypes(string[]? types)
    {
        if (types is null || types.Length is < 1 or > 6 || types.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 60)) return null;
        var result = types.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return result.Length == types.Length ? result : null;
    }
    private static string[] ForDate(MealSchedule schedule, MealCalendarException? exception, DateOnly date) => exception is not null
        ? exception.IsOpen ? Types(exception.MealTypesJson) : []
        : (schedule.WeekdayMask & (1 << (int)date.DayOfWeek)) != 0 ? Types(schedule.MealTypesJson) : [];

    // The academic-year row serializes schedule edits, overrides and generation, including the first setup.
    public static async Task<AcademicYear?> LockYear(MealTraceDbContext db, string code) => db.Database.IsNpgsql()
        ? (await db.AcademicYears.FromSqlInterpolated($"SELECT * FROM \"AcademicYears\" WHERE \"Code\" = {code} FOR UPDATE").ToListAsync()).SingleOrDefault()
        : await db.AcademicYears.FindAsync(code);
    private static async Task<List<MealDay>> LockDays(MealTraceDbContext db, string code, DateOnly from, DateOnly to) => db.Database.IsNpgsql()
        ? await db.MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"SchoolYear\" = {code} AND \"Date\" >= {from} AND \"Date\" <= {to} ORDER BY \"Id\" FOR UPDATE").ToListAsync()
        : await db.MealDays.Where(x => x.SchoolYear == code && x.Date >= from && x.Date <= to).ToListAsync();

    public static async Task<bool> Allows(MealTraceDbContext db, string code, DateOnly date, string type)
    {
        var year = await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
        var schedule = await db.MealSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolYear == code);
        if (year is null || schedule is null || date < year.StartDate || date > year.EndDate) return false;
        var exception = await db.MealCalendarExceptions.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolYear == code && x.Date == date);
        return ForDate(schedule, exception, date).Contains(type, StringComparer.Ordinal);
    }

    public static IEndpointRouteBuilder MapMealCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/meal-calendar").WithTags("Meal calendar").RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (CalendarConflict ex) { return Results.Conflict(new { message = ex.Message }); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "Lịch vừa thay đổi. Tải lại và mở lại form." }); }
            catch (Exception ex) when (DatabaseConflicts.IsConflict(ex)) { return Results.Conflict(new { message = "Dữ liệu vừa thay đổi. Tải lại và xem trước lại." }); }
        });

        group.MapGet("/{code}", async (string code, DateOnly? from, DateOnly? to, MealTraceDbContext db, TimeProvider clock) =>
        {
            var year = await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code); if (year is null) return Results.NotFound();
            var start = from ?? (Today(clock) < year.StartDate ? year.StartDate : Today(clock) > year.EndDate ? year.EndDate : Today(clock));
            var end = to ?? (start.AddDays(Math.Min(30, year.EndDate.DayNumber - start.DayNumber)));
            if (!ValidRange(year, start, end)) return Results.BadRequest(new { message = "Chọn tối đa 400 ngày trong năm học." });
            var schedule = await db.MealSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolYear == code);
            var exceptions = await db.MealCalendarExceptions.AsNoTracking().Where(x => x.SchoolYear == code && x.Date >= start && x.Date <= end).ToDictionaryAsync(x => x.Date);
            var sessions = await db.MealDays.AsNoTracking().Where(x => x.SchoolYear == code && x.Date >= start && x.Date <= end).OrderBy(x => x.MealType).ToListAsync();
            var sessionsByDate = sessions.ToLookup(x => x.Date);
            var days = new List<object>();
            for (var dayNumber = start.DayNumber; dayNumber <= end.DayNumber; dayNumber++)
            {
                var date = DateOnly.FromDayNumber(dayNumber);
                exceptions.TryGetValue(date, out var exception);
                var types = schedule is null ? [] : ForDate(schedule, exception, date);
                days.Add(new { date, isOpen = types.Length > 0, mealTypes = types, isException = exception != null, reason = exception?.Reason,
                    locked = Cutoff(date) <= clock.GetUtcNow() || sessionsByDate[date].Any(x => x.SettledAt != null || x.PublishedAt != null),
                    sessions = sessionsByDate[date].Select(x => new { x.Id, x.MealType, x.IsCancelled, x.CancellationReason, isSettled = x.SettledAt != null }) });
            }
            return Results.Ok(new { schoolYear = code, year.StartDate, year.EndDate, from = start, to = end, revision = schedule?.Revision ?? 0,
                weekdays = Enumerable.Range(0, 7).Where(x => ((schedule?.WeekdayMask ?? 0) & (1 << x)) != 0), mealTypes = schedule is null ? [] : Types(schedule.MealTypesJson), days });
        }).WithName("ReadMealCalendar");

        group.MapPut("/{code}/schedule", async (string code, ScheduleInput input, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            var types = NormalizeTypes(input.MealTypes);
            if (types is null || input.Weekdays is null || input.Weekdays.Any(x => x is < 0 or > 6) || input.Weekdays.Distinct().Count() != input.Weekdays.Length || !ValidReason(input.Reason))
                return Results.BadRequest(new { message = "Chọn các thứ, 1–6 phiên ăn khác nhau (tối đa 60 ký tự/phiên) và lý do tối đa 500 ký tự." });
            await using var tx = await db.Database.BeginTransactionAsync();
            var year = await LockYear(db, code); if (year is null) return Results.NotFound();
            var schedule = await db.MealSchedules.FindAsync(code);
            if ((schedule?.Revision ?? 0) != input.ExpectedRevision) throw new CalendarConflict("Lịch vừa thay đổi. Tải lại và mở lại form.");
            var before = schedule is null ? "null" : JsonSerializer.Serialize(schedule);
            schedule ??= new MealSchedule { SchoolYear = code, MealTypesJson = "[]" };
            if (db.Entry(schedule).State == EntityState.Detached) db.MealSchedules.Add(schedule);
            schedule.WeekdayMask = input.Weekdays.Sum(x => 1 << x); schedule.MealTypesJson = JsonSerializer.Serialize(types); schedule.Revision++;
            var today = Today(clock);
            var future = await LockDays(db, code, today > year.StartDate ? today : year.StartDate, year.EndDate);
            var exceptions = await db.MealCalendarExceptions.Where(x => x.SchoolYear == code).ToDictionaryAsync(x => x.Date);
            await Reconcile(db, future, d => { exceptions.TryGetValue(d.Date, out var exception); return ForDate(schedule, exception, d.Date).Contains(d.MealType); }, input.Reason.Trim(), clock);
            await Audit(db, principal, clock, code, null, "SCHEDULE", before, new { schedule, sessions = future.Select(x => new { x.Id, x.IsCancelled }) }, input.Reason.Trim());
            GuardChangedDays(db, clock);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { schedule.Revision });
        }).WithName("SaveMealSchedule");

        group.MapPut("/{code}/days/{date}", async (string code, DateOnly date, DayInput input, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            var mode = input.Mode?.Trim().ToUpperInvariant(); var types = mode == "OPEN" ? NormalizeTypes(input.MealTypes) : [];
            if (mode is not ("OPEN" or "CLOSED" or "DEFAULT") || types is null || !ValidReason(input.Reason)) return Results.BadRequest(new { message = "Chọn nghỉ/có ăn/theo lịch tuần, phiên ăn và lý do hợp lệ." });
            await using var tx = await db.Database.BeginTransactionAsync();
            var year = await LockYear(db, code); if (year is null) return Results.NotFound();
            if (date < year.StartDate || date > year.EndDate) return Results.BadRequest(new { message = "Ngày phải nằm trong năm học." });
            if (Cutoff(date) <= clock.GetUtcNow()) throw new CalendarConflict("Đã qua giờ chốt; không sửa lịch ngày này. Cần quy trình điều chỉnh sau chốt.");
            var schedule = await db.MealSchedules.FindAsync(code);
            if (schedule is null) return Results.BadRequest(new { message = "Thiết lập lịch tuần trước." });
            if (schedule.Revision != input.ExpectedRevision) throw new CalendarConflict("Lịch vừa thay đổi. Tải lại và mở lại form.");
            var exception = await db.MealCalendarExceptions.FindAsync(code, date); var before = JsonSerializer.Serialize(exception);
            if (mode == "DEFAULT") { if (exception is not null) db.MealCalendarExceptions.Remove(exception); exception = null; }
            else
            {
                exception ??= new MealCalendarException { SchoolYear = code, Date = date, MealTypesJson = "[]", Reason = "" };
                if (db.Entry(exception).State == EntityState.Detached) db.MealCalendarExceptions.Add(exception);
                exception.IsOpen = mode == "OPEN"; exception.MealTypesJson = JsonSerializer.Serialize(types); exception.Reason = input.Reason.Trim();
            }
            var sessions = await LockDays(db, code, date, date);
            if (sessions.Any(x => x.SettledAt != null || x.PublishedAt != null) || await db.PortionSettlements.AnyAsync(x => sessions.Select(d => d.Id).Contains(x.MealDayId)))
                throw new CalendarConflict("Ngày đã có phiên chốt/công bố; không đổi lịch ngày này.");
            await Reconcile(db, sessions, d => ForDate(schedule, exception, date).Contains(d.MealType), input.Reason.Trim(), clock);
            if (Cutoff(date) <= clock.GetUtcNow()) throw new CalendarConflict("Đã hết thời gian sửa lịch ngày.");
            schedule.Revision++;
            await Audit(db, principal, clock, code, date, mode, before, new { exception, sessions = sessions.Select(x => new { x.Id, x.IsCancelled }) }, input.Reason.Trim());
            if (Cutoff(date) <= clock.GetUtcNow()) throw new CalendarConflict("Đã hết thời gian sửa lịch ngày.");
            GuardChangedDays(db, clock);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { schedule.Revision });
        }).WithName("EditMealCalendarDay");

        group.MapPost("/{code}/preview", async (string code, GenerateInput input, MealTraceDbContext db, TimeProvider clock) =>
        {
            var year = await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code); if (year is null) return Results.NotFound();
            var schedule = await db.MealSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolYear == code);
            if (schedule is null || !ValidRange(year, input.From, input.To)) return Results.BadRequest(new { message = "Thiết lập lịch tuần và chọn tối đa 400 ngày trong năm học." });
            if (schedule.Revision != input.ExpectedRevision) throw new CalendarConflict("Lịch đã đổi. Tải lại trước khi xem trước.");
            var items = await BuildPlan(db, year, schedule, input, clock);
            return Results.Ok(new { items, previewToken = Token(code, schedule.Revision, input, items), createCount = items.Count(x => x.Action == "CREATE"), restoreCount = items.Count(x => x.Action == "RESTORE"), existingCount = items.Count(x => x.Action == "EXISTS") });
        }).WithName("PreviewMealGeneration");

        group.MapPost("/{code}/generate", async (string code, GenerateInput input, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(); var year = await LockYear(db, code); if (year is null) return Results.NotFound();
            var schedule = await db.MealSchedules.FindAsync(code);
            if (schedule is null || !ValidRange(year, input.From, input.To)) return Results.BadRequest(new { message = "Thiết lập lịch tuần và chọn tối đa 400 ngày trong năm học." });
            await LockDays(db, code, input.From, input.To);
            var items = await BuildPlan(db, year, schedule, input, clock);
            if (schedule.Revision != input.ExpectedRevision || input.PreviewToken != Token(code, schedule.Revision, input, items))
                throw new CalendarConflict("Lịch, giờ chốt hoặc phiên đã thay đổi. Hãy xem trước lại trước khi tạo.");
            var changed = new List<MealDay>();
            foreach (var item in items.Where(x => x.Action is "CREATE" or "RESTORE"))
            {
                if (Cutoff(item.Date) <= clock.GetUtcNow()) throw new CalendarConflict("Đã qua giờ chốt. Hãy xem trước lại.");
                var day = item.MealId is null ? new MealDay { Date = item.Date, SchoolYear = code, MealType = item.MealType, CutoffAt = Cutoff(item.Date) } : await db.MealDays.FindAsync(item.MealId.Value);
                if (day is null) throw new CalendarConflict("Phiên đã thay đổi. Xem trước lại.");
                if (item.MealId is null) db.MealDays.Add(day);
                day.IsCancelled = false; day.CancellationReason = null; day.DecisionRevision++; changed.Add(day);
            }
            if (changed.Any(x => x.CutoffAt <= clock.GetUtcNow())) throw new CalendarConflict("Đã qua giờ chốt. Xem trước lại.");
            if (changed.Count > 0) await Audit(db, principal, clock, code, null, "GENERATE", "null", changed.Select(x => new { x.Id, x.Date, x.MealType }), $"Tạo lịch {input.From:yyyy-MM-dd} đến {input.To:yyyy-MM-dd}");
            if (changed.Any(x => x.CutoffAt <= clock.GetUtcNow())) throw new CalendarConflict("Đã qua giờ chốt. Xem trước lại.");
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(new { created = items.Count(x => x.Action == "CREATE"), restored = items.Count(x => x.Action == "RESTORE"), existing = items.Count(x => x.Action == "EXISTS") });
        }).WithName("GenerateMealSessions");

        group.MapGet("/{code}/history", async (string code, MealTraceDbContext db) =>
            await db.MealCalendarAudits.AsNoTracking().Where(x => x.SchoolYear == code).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Take(50)
                .Select(x => new { x.Id, x.Date, x.Kind, x.Reason, x.ActorId, x.ActorName, x.RecordedAt }).ToListAsync()).WithName("MealCalendarHistory");
        return app;
    }

    private static bool ValidRange(AcademicYear year, DateOnly from, DateOnly to) => from >= year.StartDate && to <= year.EndDate && to >= from && to.DayNumber - from.DayNumber < 400;
    private static string Token(string code, int revision, GenerateInput range, List<PlanItem> items) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { code, revision, range.From, range.To, items }))));
    private static async Task<List<PlanItem>> BuildPlan(MealTraceDbContext db, AcademicYear year, MealSchedule schedule, GenerateInput range, TimeProvider clock)
    {
        var exceptions = await db.MealCalendarExceptions.AsNoTracking().Where(x => x.SchoolYear == year.Code && x.Date >= range.From && x.Date <= range.To).ToDictionaryAsync(x => x.Date);
        var sessions = await db.MealDays.AsNoTracking().Where(x => x.Date >= range.From && x.Date <= range.To).ToDictionaryAsync(x => new { x.Date, x.MealType });
        var sessionIds = sessions.Values.Select(x => x.Id).ToArray();
        var protectedIds = await db.PortionSettlements.Where(x => sessionIds.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync();
        protectedIds.AddRange(await db.MealEvidence.Where(x => sessionIds.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync());
        var result = new List<PlanItem>();
        for (var dayNumber = range.From.DayNumber; dayNumber <= range.To.DayNumber; dayNumber++)
        {
            var date = DateOnly.FromDayNumber(dayNumber);
            exceptions.TryGetValue(date, out var exception); var types = ForDate(schedule, exception, date);
            if (types.Length == 0) { result.Add(new(date, "", "CLOSED")); continue; }
            foreach (var type in types)
            {
                sessions.TryGetValue(new { Date = date, MealType = type }, out var day);
                var action = day is not null && day.SchoolYear != year.Code ? "OTHER_YEAR" : day is not null && !day.IsCancelled ? "EXISTS"
                    : Cutoff(date) <= clock.GetUtcNow() || (day is not null && (day.CutoffAt <= clock.GetUtcNow() || day.SettledAt != null || day.PublishedAt != null || protectedIds.Contains(day.Id))) ? "LOCKED" : day is null ? "CREATE" : "RESTORE";
                result.Add(new(date, type, action, day?.Id));
            }
        }
        return result;
    }
    private static async Task Reconcile(MealTraceDbContext db, List<MealDay> days, Func<MealDay, bool> allowed, string reason, TimeProvider clock)
    {
        var changed = days.Where(x => x.IsCancelled == allowed(x)).ToArray(); var ids = changed.Select(x => x.Id).ToArray();
        var protectedIds = await db.PortionSettlements.Where(x => ids.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync();
        protectedIds.AddRange(await db.MealEvidence.Where(x => ids.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync());
        if (changed.Any(x => x.SettledAt != null || x.PublishedAt != null || protectedIds.Contains(x.Id) || x.CutoffAt <= clock.GetUtcNow()))
            throw new CalendarConflict("Thay đổi ảnh hưởng phiên đã chốt/công bố hoặc qua giờ chốt. Giữ lịch cũ và điều chỉnh sau chốt riêng.");
        foreach (var day in changed) { day.IsCancelled = !allowed(day); day.CancellationReason = day.IsCancelled ? reason : null; day.DecisionRevision++; }
    }
    private static void GuardChangedDays(MealTraceDbContext db, TimeProvider clock)
    {
        if (db.ChangeTracker.Entries<MealDay>().Any(x => x.State == EntityState.Modified && x.Entity.CutoffAt <= clock.GetUtcNow()))
            throw new CalendarConflict("Đã qua giờ chốt trong lúc lưu. Tải lại lịch.");
    }
    private static async Task Audit(MealTraceDbContext db, ClaimsPrincipal principal, TimeProvider clock, string code, DateOnly? date, string kind, string before, object after, string reason)
    {
        var actor = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var name = await db.Users.Where(x => x.Id == actor).Select(x => x.FullName).SingleAsync();
        db.MealCalendarAudits.Add(new MealCalendarAudit { SchoolYear = code, Date = date, Kind = kind, BeforeJson = before, AfterJson = JsonSerializer.Serialize(after),
            Reason = reason, ActorId = actor, ActorName = name, RecordedAt = clock.GetUtcNow() });
    }
}

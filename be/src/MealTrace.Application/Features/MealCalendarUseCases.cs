using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Dtos.Calendar;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;

namespace MealTrace.Application.Features;
public static class MealCalendarUseCases
{
    public static DateTimeOffset Cutoff(DateOnly date) => SchoolTime.Cutoff(date);
    private static DateOnly Today(TimeProvider clock) => SchoolTime.Today(clock.GetUtcNow());
    private static string[] Types(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    private static string[]? NormalizeTypes(string[]? types)
    {
        if (types is null || types.Length is < 1 or > 6 || types.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 60))
            return null;
        var result = types.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return result.Length == types.Length ? result : null;
    }

    private static string[] ForDate(MealSchedule schedule, MealCalendarException? exception, DateOnly date) => exception is not null ? exception.IsOpen ? Types(exception.MealTypesJson) : [] : (schedule.WeekdayMask & (1 << (int)date.DayOfWeek)) != 0 ? Types(schedule.MealTypesJson) : [];
    // The academic-year row serializes schedule edits, overrides and generation, including the first setup.
    public static async Task<AcademicYear?> LockYear(IMealTraceData db, string code) => await db.LockAcademicYearAsync(code);
    private static async Task<List<MealDay>> LockDays(IMealTraceData db, string code, DateOnly from, DateOnly to) => await db.LockMealDaysAsync(code, from, to);
    public static async Task<bool> Allows(IMealTraceData db, string code, DateOnly date, string type)
    {
        var year = await db.AcademicYears.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Code == code);
        var schedule = await db.MealSchedules.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.SchoolYear == code);
        if (year is null || schedule is null || date < year.StartDate || date > year.EndDate)
            return false;
        var exception = await db.MealCalendarExceptions.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.SchoolYear == code && x.Date == date);
        return ForDate(schedule, exception, date).Contains(type, StringComparer.Ordinal);
    }

    public static async Task<UseCaseResult> GetCalendarAsync(string code, DateOnly? from, DateOnly? to, IMealTraceData db, TimeProvider clock)
    {
        var year = await db.AcademicYears.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Code == code);
        if (year is null)
            return UseCaseResult.NotFound();
        var now = clock.GetUtcNow();
        var today = SchoolTime.Today(now);
        var start = from ?? (today < year.StartDate ? year.StartDate : today > year.EndDate ? year.EndDate : today);
        var end = to ?? (start.AddDays(Math.Min(30, year.EndDate.DayNumber - start.DayNumber)));
        if (!ValidRange(year, start, end))
            return UseCaseResult.BadRequest(new MessageResponse("Chọn tối đa 400 ngày trong năm học."));
        var schedule = await db.MealSchedules.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.SchoolYear == code);
        var exceptions = await db.MealCalendarExceptions.AsNoTracking(db.Queries).Where(x => x.SchoolYear == code && x.Date >= start && x.Date <= end).ToDictionaryAsync(db.Queries, x => x.Date);
        var sessions = await db.MealDays.AsNoTracking(db.Queries).Where(x => x.SchoolYear == code && x.Date >= start && x.Date <= end).OrderBy(x => x.MealType).ToListAsync(db.Queries);
        var sessionsByDate = sessions.ToLookup(x => x.Date);
        var days = new List<CalendarDaySummary>();
        for (var dayNumber = start.DayNumber; dayNumber <= end.DayNumber; dayNumber++)
        {
            var date = DateOnly.FromDayNumber(dayNumber);
            exceptions.TryGetValue(date, out var exception);
            var types = schedule is null ? [] : ForDate(schedule, exception, date);
            days.Add(new CalendarDaySummary
            {
                Date = date,
                IsOpen = types.Length > 0,
                MealTypes = types,
                IsException = exception != null,
                Reason = exception?.Reason,
                Locked = Cutoff(date) <= now || sessionsByDate[date].Any(x => x.SettledAt != null || x.PublishedAt != null),
                Sessions = sessionsByDate[date].Select(x => new CalendarSessionSummary
                {
                    Id = x.Id,
                    MealType = x.MealType,
                    IsCancelled = x.IsCancelled,
                    CancellationReason = x.CancellationReason,
                    IsSettled = x.SettledAt != null
                })
            });
        }

        return UseCaseResult.Ok(new MealCalendarResponse
        {
            SchoolYear = code,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            From = start,
            To = end,
            Revision = schedule?.Revision ?? 0,
            Weekdays = Enumerable.Range(0, 7).Where(x => ((schedule?.WeekdayMask ?? 0) & (1 << x)) != 0),
            MealTypes = schedule is null ? [] : Types(schedule.MealTypesJson),
            Days = days
        });
    }

    public static async Task<UseCaseResult> SaveScheduleAsync(string code, ScheduleInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        var types = NormalizeTypes(input.MealTypes);
        if (types is null || input.Weekdays is null || input.Weekdays.Any(x => x is < 0 or > 6) || input.Weekdays.Distinct().Count() != input.Weekdays.Length || !ValidReason(input.Reason))
            return UseCaseResult.BadRequest(new MessageResponse("Chọn các thứ, 1–6 phiên ăn khác nhau (tối đa 60 ký tự/phiên) và lý do tối đa 500 ký tự."));
        await using var tx = await db.BeginTransactionAsync();
        var year = await LockYear(db, code);
        if (year is null)
            return UseCaseResult.NotFound();
        var schedule = await db.MealSchedules.FindAsync(code);
        if ((schedule?.Revision ?? 0) != input.ExpectedRevision)
            throw new CalendarConflict("Lịch vừa thay đổi. Tải lại và mở lại form.");
        var before = schedule is null ? "null" : JsonSerializer.Serialize(schedule);
        schedule ??= new MealSchedule
        {

            SchoolYear = code,

            MealTypesJson = "[]"

        };
        if (db.IsNew(schedule))
            db.MealSchedules.Add(schedule);
        schedule.WeekdayMask = input.Weekdays.Sum(x => 1 << x);
        schedule.MealTypesJson = JsonSerializer.Serialize(types);
        schedule.Revision++;
        var today = Today(clock);
        var future = await LockDays(db, code, today > year.StartDate ? today : year.StartDate, year.EndDate);
        var exceptions = await db.MealCalendarExceptions.Where(x => x.SchoolYear == code).ToDictionaryAsync(db.Queries, x => x.Date);
        await Reconcile(db, future, d =>
        {
            exceptions.TryGetValue(d.Date, out var exception);
            return ForDate(schedule, exception, d.Date).Contains(d.MealType);
        }, input.Reason.Trim(), clock);
        await Audit(db, principal, clock, code, null, "SCHEDULE", before, new ScheduleAuditState
        {
            Schedule = schedule,
            Sessions = future.Select(x => new SessionAuditState
            {
                Id = x.Id,
                IsCancelled = x.IsCancelled
            })
        }, input.Reason.Trim());
        GuardChangedDays(db, clock);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return UseCaseResult.Ok(new CalendarRevisionResponse { Revision = schedule.Revision });
    }

    public static async Task<UseCaseResult> EditDayAsync(string code, DateOnly date, DayInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        var mode = input.Mode?.Trim().ToUpperInvariant();
        var types = mode == "OPEN" ? NormalizeTypes(input.MealTypes) : [];
        if (mode is not ("OPEN" or "CLOSED" or "DEFAULT") || types is null || !ValidReason(input.Reason))
            return UseCaseResult.BadRequest(new MessageResponse("Chọn nghỉ/có ăn/theo lịch tuần, phiên ăn và lý do hợp lệ."));
        await using var tx = await db.BeginTransactionAsync();
        var year = await LockYear(db, code);
        if (year is null)
            return UseCaseResult.NotFound();
        if (date < year.StartDate || date > year.EndDate)
            return UseCaseResult.BadRequest(new MessageResponse("Ngày phải nằm trong năm học."));
        if (Cutoff(date) <= clock.GetUtcNow())
            throw new CalendarConflict("Đã qua giờ chốt; không sửa lịch ngày này. Cần quy trình điều chỉnh sau chốt.");
        var schedule = await db.MealSchedules.FindAsync(code);
        if (schedule is null)
            return UseCaseResult.BadRequest(new MessageResponse("Thiết lập lịch tuần trước."));
        if (schedule.Revision != input.ExpectedRevision)
            throw new CalendarConflict("Lịch vừa thay đổi. Tải lại và mở lại form.");
        var exception = await db.MealCalendarExceptions.FindAsync(code, date);
        var before = JsonSerializer.Serialize(exception);
        if (mode == "DEFAULT")
        {
            if (exception is not null)
                db.MealCalendarExceptions.Remove(exception);
            exception = null;
        }
        else
        {
            exception ??= new MealCalendarException
            {

                SchoolYear = code,

                Date = date,

                MealTypesJson = "[]",

                Reason = ""

            };
            if (db.IsNew(exception))
                db.MealCalendarExceptions.Add(exception);
            exception.IsOpen = mode == "OPEN";
            exception.MealTypesJson = JsonSerializer.Serialize(types);
            exception.Reason = input.Reason.Trim();
        }

        var sessions = await LockDays(db, code, date, date);
        if (sessions.Any(x => x.SettledAt != null || x.PublishedAt != null) || await db.PortionSettlements.AnyAsync(db.Queries, x => sessions.Select(d => d.Id).Contains(x.MealDayId)))
            throw new CalendarConflict("Ngày đã có phiên chốt/công bố; không đổi lịch ngày này.");
        await Reconcile(db, sessions, d => ForDate(schedule, exception, date).Contains(d.MealType), input.Reason.Trim(), clock);
        if (Cutoff(date) <= clock.GetUtcNow())
            throw new CalendarConflict("Đã hết thời gian sửa lịch ngày.");
        schedule.Revision++;
        await Audit(db, principal, clock, code, date, mode, before, new DayAuditState
        {
            Exception = exception,
            Sessions = sessions.Select(x => new SessionAuditState
            {
                Id = x.Id,
                IsCancelled = x.IsCancelled
            })
        }, input.Reason.Trim());
        if (Cutoff(date) <= clock.GetUtcNow())
            throw new CalendarConflict("Đã hết thời gian sửa lịch ngày.");
        GuardChangedDays(db, clock);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return UseCaseResult.Ok(new CalendarRevisionResponse { Revision = schedule.Revision });
    }

    public static async Task<UseCaseResult> PreviewGenerationAsync(string code, GenerateInput input, IMealTraceData db, TimeProvider clock)
    {
        var year = await db.AcademicYears.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Code == code);
        if (year is null)
            return UseCaseResult.NotFound();
        var schedule = await db.MealSchedules.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.SchoolYear == code);
        if (schedule is null || !ValidRange(year, input.From, input.To))
            return UseCaseResult.BadRequest(new MessageResponse("Thiết lập lịch tuần và chọn tối đa 400 ngày trong năm học."));
        if (schedule.Revision != input.ExpectedRevision)
            throw new CalendarConflict("Lịch đã đổi. Tải lại trước khi xem trước.");
        var items = await BuildPlan(db, year, schedule, input, clock);
        return UseCaseResult.Ok(new CalendarGenerationPreview
        {
            Items = items,
            PreviewToken = Token(code, schedule.Revision, input, items),
            CreateCount = items.Count(x => x.Action == "CREATE"),
            RestoreCount = items.Count(x => x.Action == "RESTORE"),
            ExistingCount = items.Count(x => x.Action == "EXISTS")
        });
    }

    public static async Task<UseCaseResult> GenerateSessionsAsync(string code, GenerateInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        await using var tx = await db.BeginTransactionAsync();
        var year = await LockYear(db, code);
        if (year is null)
            return UseCaseResult.NotFound();
        var schedule = await db.MealSchedules.FindAsync(code);
        if (schedule is null || !ValidRange(year, input.From, input.To))
            return UseCaseResult.BadRequest(new MessageResponse("Thiết lập lịch tuần và chọn tối đa 400 ngày trong năm học."));
        await LockDays(db, code, input.From, input.To);
        var items = await BuildPlan(db, year, schedule, input, clock);
        if (schedule.Revision != input.ExpectedRevision || input.PreviewToken != Token(code, schedule.Revision, input, items))
            throw new CalendarConflict("Lịch, giờ chốt hoặc phiên đã thay đổi. Hãy xem trước lại trước khi tạo.");
        var changed = new List<MealDay>();
        foreach (var item in items.Where(x => x.Action is "CREATE" or "RESTORE"))
        {
            if (Cutoff(item.Date) <= clock.GetUtcNow())
                throw new CalendarConflict("Đã qua giờ chốt. Hãy xem trước lại.");
            var day = item.MealId is null ? new MealDay
            {

                Date = item.Date,

                SchoolYear = code,

                MealType = item.MealType,

                CutoffAt = Cutoff(item.Date)

            }

            : await db.MealDays.FindAsync(item.MealId.Value);
            if (day is null)
                throw new CalendarConflict("Phiên đã thay đổi. Xem trước lại.");
            if (item.MealId is null)
                db.MealDays.Add(day);
            day.IsCancelled = false;
            day.CancellationReason = null;
            day.DecisionRevision++;
            changed.Add(day);
        }

        if (changed.Any(x => x.CutoffAt <= clock.GetUtcNow()))
            throw new CalendarConflict("Đã qua giờ chốt. Xem trước lại.");
        if (changed.Count > 0)
            await Audit(db, principal, clock, code, null, "GENERATE", "null", changed.Select(x => new GeneratedSessionSummary
            {
                Id = x.Id,
                Date = x.Date,
                MealType = x.MealType
            }), $"Tạo lịch {input.From:yyyy-MM-dd} đến {input.To:yyyy-MM-dd}");
        if (changed.Any(x => x.CutoffAt <= clock.GetUtcNow()))
            throw new CalendarConflict("Đã qua giờ chốt. Xem trước lại.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return UseCaseResult.Ok(new CalendarGenerationResponse
        {
            Created = items.Count(x => x.Action == "CREATE"),
            Restored = items.Count(x => x.Action == "RESTORE"),
            Existing = items.Count(x => x.Action == "EXISTS")
        });
    }

    public static async Task<UseCaseResult> GetHistoryAsync(string code, IMealTraceData db)
    {
        return UseCaseResult.Ok(await db.MealCalendarAudits.AsNoTracking(db.Queries).Where(x => x.SchoolYear == code).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Take(50).Select(x => new CalendarHistoryItem
        {
            Id = x.Id,
            Date = x.Date,
            Kind = x.Kind,
            Reason = x.Reason,
            ActorId = x.ActorId,
            ActorName = x.ActorName,
            RecordedAt = x.RecordedAt
        }).ToListAsync(db.Queries));
    }

    private static bool ValidRange(AcademicYear year, DateOnly from, DateOnly to) => from >= year.StartDate && to <= year.EndDate && to >= from && to.DayNumber - from.DayNumber < 400;
    private static string Token(string code, int revision, GenerateInput range, List<PlanItem> items) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CalendarPreviewFingerprint
    {
        Code = code,
        Revision = revision,
        From = range.From,
        To = range.To,
        Items = items
    }))));
    private static async Task<List<PlanItem>> BuildPlan(IMealTraceData db, AcademicYear year, MealSchedule schedule, GenerateInput range, TimeProvider clock)
    {
        var exceptions = await db.MealCalendarExceptions.AsNoTracking(db.Queries).Where(x => x.SchoolYear == year.Code && x.Date >= range.From && x.Date <= range.To).ToDictionaryAsync(db.Queries, x => x.Date);
        var sessions = await db.MealDays.AsNoTracking(db.Queries).Where(x => x.Date >= range.From && x.Date <= range.To).ToDictionaryAsync(db.Queries, x => new CalendarSessionKey
        {
            Date = x.Date,
            MealType = x.MealType
        });
        var sessionIds = sessions.Values.Select(x => x.Id).ToArray();
        var protectedIds = await db.PortionSettlements.Where(x => sessionIds.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync(db.Queries);
        protectedIds.AddRange(await db.MealEvidence.Where(x => sessionIds.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync(db.Queries));
        var now = clock.GetUtcNow();
        var result = new List<PlanItem>();
        for (var dayNumber = range.From.DayNumber; dayNumber <= range.To.DayNumber; dayNumber++)
        {
            var date = DateOnly.FromDayNumber(dayNumber);
            exceptions.TryGetValue(date, out var exception);
            var types = ForDate(schedule, exception, date);
            if (types.Length == 0)
            {
                result.Add(new(date, "", "CLOSED"));
                continue;
            }

            foreach (var type in types)
            {
                sessions.TryGetValue(new CalendarSessionKey
                {
                    Date = date,
                    MealType = type
                }, out var day);
                var action = day is not null && day.SchoolYear != year.Code ? "OTHER_YEAR" : day is not null && !day.IsCancelled ? "EXISTS" : Cutoff(date) <= now || (day is not null && (day.CutoffAt <= now || day.SettledAt != null || day.PublishedAt != null || protectedIds.Contains(day.Id))) ? "LOCKED" : day is null ? "CREATE" : "RESTORE";
                result.Add(new(date, type, action, day?.Id));
            }
        }

        return result;
    }

    private static async Task Reconcile(IMealTraceData db, List<MealDay> days, Func<MealDay, bool> allowed, string reason, TimeProvider clock)
    {
        var changed = days.Where(x => x.IsCancelled == allowed(x)).ToArray();
        var ids = changed.Select(x => x.Id).ToArray();
        var protectedIds = await db.PortionSettlements.Where(x => ids.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync(db.Queries);
        protectedIds.AddRange(await db.MealEvidence.Where(x => ids.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync(db.Queries));
        var now = clock.GetUtcNow();
        if (changed.Any(x => x.SettledAt != null || x.PublishedAt != null || protectedIds.Contains(x.Id) || x.CutoffAt <= now))
            throw new CalendarConflict("Thay đổi ảnh hưởng phiên đã chốt/công bố hoặc qua giờ chốt. Giữ lịch cũ và điều chỉnh sau chốt riêng.");
        foreach (var day in changed)
        {
            day.IsCancelled = !allowed(day);
            day.CancellationReason = day.IsCancelled ? reason : null;
            day.DecisionRevision++;
        }
    }

    private static void GuardChangedDays(IMealTraceData db, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (db.ChangedMealDays.Any(x => x.CutoffAt <= now))
            throw new CalendarConflict("Đã qua giờ chốt trong lúc lưu. Tải lại lịch.");
    }

    private static async Task Audit(IMealTraceData db, ClaimsPrincipal principal, TimeProvider clock, string code, DateOnly? date, string kind, string before, object after, string reason)
    {
        var actor = Guid.Parse(principal.FindFirst("sub")?.Value!);
        var name = await db.Users.Where(x => x.Id == actor).Select(x => x.FullName).SingleAsync(db.Queries);
        db.MealCalendarAudits.Add(new MealCalendarAudit
        {
            SchoolYear = code,
            Date = date,
            Kind = kind,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(after),
            Reason = reason,
            ActorId = actor,
            ActorName = name,
            RecordedAt = clock.GetUtcNow()
        });
    }
}

using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Models.Persistence;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Dtos.Calendar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Time;

namespace MealTrace.Application.Features.Calendar;
public sealed class MealCalendarUseCases(IMealCalendarRepository repository, TimeProvider clock, ICurrentActor currentActor, IUnitOfWork unitOfWork)
{
    public static DateTimeOffset Cutoff(DateOnly date) => SchoolTime.Cutoff(date);
    private DateOnly Today() => SchoolTime.Today(clock.GetUtcNow());
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
    public async Task<AcademicYear?> LockYear(string code) => await repository.LockAcademicYearAsync(code);
    private async Task<List<MealDay>> LockDays(string code, DateOnly from, DateOnly to) => await repository.LockMealDaysAsync(code, from, to);
    public async Task<bool> Allows(string code, DateOnly date, string type)
    {
        var year = await repository.FindAcademicYearAsync(code);
        var schedule = await repository.FindScheduleAsync(code);
        if (year is null || schedule is null || date < year.StartDate || date > year.EndDate)
            return false;
        var exception = await repository.FindDayExceptionAsync(code, date);
        return ForDate(schedule, exception, date).Contains(type, StringComparer.Ordinal);
    }

    public async Task<Result<MealCalendarResponse>> GetCalendarAsync(string code, DateOnly? from, DateOnly? to)
    {
        var year = await repository.FindAcademicYearAsync(code);
        if (year is null)
            return Result.NotFound();
        var now = clock.GetUtcNow();
        var today = SchoolTime.Today(clock.GetUtcNow());
        var start = from ?? (today < year.StartDate ? year.StartDate : today > year.EndDate ? year.EndDate : today);
        var end = to ?? (start.AddDays(Math.Min(30, year.EndDate.DayNumber - start.DayNumber)));
        if (!ValidRange(year, start, end))
            return Result.Invalid("Chọn tối đa 400 ngày trong năm học.");
        var schedule = await repository.FindScheduleAsync(code);
        var exceptions = await repository.GetDayExceptionsByDateAsync(code, start, end);
        var sessions = await repository.ListMealDaysAsync(code, start, end);
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

        return Result.Success(new MealCalendarResponse
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

    public async Task<Result<CalendarRevisionResponse>> SaveScheduleAsync(string code, ScheduleInput input)
    {
        var types = NormalizeTypes(input.MealTypes);
        if (types is null || input.Weekdays is null || input.Weekdays.Any(x => x is < 0 or > 6) || input.Weekdays.Distinct().Count() != input.Weekdays.Length || !ValidReason(input.Reason))
            return Result.Invalid("Chọn các thứ, 1–6 phiên ăn khác nhau (tối đa 60 ký tự/phiên) và lý do tối đa 500 ký tự.");
        await using var tx = await unitOfWork.BeginTransactionAsync();
        var year = await LockYear(code);
        if (year is null)
            return Result.NotFound();
        var schedule = await repository.FindTrackedScheduleAsync(code);
        if ((schedule?.Revision ?? 0) != input.ExpectedRevision)
            throw new CalendarConflict("Lịch vừa thay đổi. Tải lại và mở lại form.");
        var isNewSchedule = schedule is null;
        var before = schedule is null ? "null" : JsonSerializer.Serialize(schedule);
        schedule ??= new MealSchedule
        {
            SchoolYear = code,

            MealTypesJson = "[]"
        };
        if (isNewSchedule)
            repository.AddMealSchedule(schedule);
        schedule.WeekdayMask = input.Weekdays.Sum(x => 1 << x);
        schedule.MealTypesJson = JsonSerializer.Serialize(types);
        schedule.Revision++;
        var today = Today();
        var future = await LockDays(code, today > year.StartDate ? today : year.StartDate, year.EndDate);
        var exceptions = await repository.GetTrackedDayExceptionsAsync(code);
        await Reconcile(future, d =>
        {
            exceptions.TryGetValue(d.Date, out var exception);
            return ForDate(schedule, exception, d.Date).Contains(d.MealType);
        }, input.Reason.Trim());
        await Audit(code, null, "SCHEDULE", before, new ScheduleAuditState
        {
            Schedule = schedule,
            Sessions = future.Select(x => new SessionAuditState
            {
                Id = x.Id,
                IsCancelled = x.IsCancelled
            })
        }, input.Reason.Trim());
        GuardChangedDays();
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return Result.Success(new CalendarRevisionResponse { Revision = schedule.Revision });
    }

    public async Task<Result<CalendarRevisionResponse>> EditDayAsync(string code, DateOnly date, DayInput input)
    {
        var mode = input.Mode?.Trim().ToUpperInvariant();
        var types = mode == "OPEN" ? NormalizeTypes(input.MealTypes) : [];
        if (mode is not ("OPEN" or "CLOSED" or "DEFAULT") || types is null || !ValidReason(input.Reason))
            return Result.Invalid("Chọn nghỉ/có ăn/theo lịch tuần, phiên ăn và lý do hợp lệ.");
        await using var tx = await unitOfWork.BeginTransactionAsync();
        var year = await LockYear(code);
        if (year is null)
            return Result.NotFound();
        if (date < year.StartDate || date > year.EndDate)
            return Result.Invalid("Ngày phải nằm trong năm học.");
        if (Cutoff(date) <= clock.GetUtcNow())
            throw new CalendarConflict("Đã qua giờ chốt; không sửa lịch ngày này. Cần quy trình điều chỉnh sau chốt.");
        var schedule = await repository.FindTrackedScheduleAsync(code);
        if (schedule is null)
            return Result.Invalid("Thiết lập lịch tuần trước.");
        if (schedule.Revision != input.ExpectedRevision)
            throw new CalendarConflict("Lịch vừa thay đổi. Tải lại và mở lại form.");
        var exception = await repository.FindTrackedDayExceptionAsync(code, date);
        var isNewException = exception is null;
        var before = JsonSerializer.Serialize(exception);
        if (mode == "DEFAULT")
        {
            if (exception is not null)
                repository.RemoveMealCalendarException(exception);
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
            if (isNewException)
                repository.AddMealCalendarException(exception);
            exception.IsOpen = mode == "OPEN";
            exception.MealTypesJson = JsonSerializer.Serialize(types);
            exception.Reason = input.Reason.Trim();
        }

        var sessions = await LockDays(code, date, date);
        if (sessions.Any(x => x.SettledAt != null || x.PublishedAt != null) || await repository.AnyDayHasSettlementAsync(sessions))
            throw new CalendarConflict("Ngày đã có phiên chốt/công bố; không đổi lịch ngày này.");
        await Reconcile(sessions, d => ForDate(schedule, exception, date).Contains(d.MealType), input.Reason.Trim());
        if (Cutoff(date) <= clock.GetUtcNow())
            throw new CalendarConflict("Đã hết thời gian sửa lịch ngày.");
        schedule.Revision++;
        await Audit(code, date, mode, before, new DayAuditState
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
        GuardChangedDays();
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return Result.Success(new CalendarRevisionResponse { Revision = schedule.Revision });
    }

    public async Task<Result<CalendarGenerationPreview>> PreviewGenerationAsync(string code, GenerateInput input)
    {
        var year = await repository.FindAcademicYearAsync(code);
        if (year is null)
            return Result.NotFound();
        var schedule = await repository.FindScheduleAsync(code);
        if (schedule is null || !ValidRange(year, input.From, input.To))
            return Result.Invalid("Thiết lập lịch tuần và chọn tối đa 400 ngày trong năm học.");
        if (schedule.Revision != input.ExpectedRevision)
            throw new CalendarConflict("Lịch đã đổi. Tải lại trước khi xem trước.");
        var items = await BuildPlan(year, schedule, input);
        return Result.Success(new CalendarGenerationPreview
        {
            Items = items,
            PreviewToken = Token(code, schedule.Revision, input, items),
            CreateCount = items.Count(x => x.Action == "CREATE"),
            RestoreCount = items.Count(x => x.Action == "RESTORE"),
            ExistingCount = items.Count(x => x.Action == "EXISTS")
        });
    }

    public async Task<Result<CalendarGenerationResponse>> GenerateSessionsAsync(string code, GenerateInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync();
        var year = await LockYear(code);
        if (year is null)
            return Result.NotFound();
        var schedule = await repository.FindTrackedScheduleAsync(code);
        if (schedule is null || !ValidRange(year, input.From, input.To))
            return Result.Invalid("Thiết lập lịch tuần và chọn tối đa 400 ngày trong năm học.");
        await LockDays(code, input.From, input.To);
        var items = await BuildPlan(year, schedule, input);
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

            : await repository.FindTrackedMealDayAsync(item.MealId.Value);
            if (day is null)
                throw new CalendarConflict("Phiên đã thay đổi. Xem trước lại.");
            if (item.MealId is null)
                repository.AddMealDay(day);
            day.IsCancelled = false;
            day.CancellationReason = null;
            day.DecisionRevision++;
            changed.Add(day);
        }

        if (changed.Any(x => x.CutoffAt <= clock.GetUtcNow()))
            throw new CalendarConflict("Đã qua giờ chốt. Xem trước lại.");
        if (changed.Count > 0)
            await Audit(code, null, "GENERATE", "null", changed.Select(x => new GeneratedSessionSummary
            {
                Id = x.Id,
                Date = x.Date,
                MealType = x.MealType
            }), $"Tạo lịch {input.From:yyyy-MM-dd} đến {input.To:yyyy-MM-dd}");
        if (changed.Any(x => x.CutoffAt <= clock.GetUtcNow()))
            throw new CalendarConflict("Đã qua giờ chốt. Xem trước lại.");
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return Result.Success(new CalendarGenerationResponse
        {
            Created = items.Count(x => x.Action == "CREATE"),
            Restored = items.Count(x => x.Action == "RESTORE"),
            Existing = items.Count(x => x.Action == "EXISTS")
        });
    }

    public async Task<Result<List<CalendarHistoryItem>>> GetHistoryAsync(string code)
    {
        return Result.Success(await repository.ListCalendarHistoryAsync(code));
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
    private async Task<List<PlanItem>> BuildPlan(AcademicYear year, MealSchedule schedule, GenerateInput range)
    {
        var exceptions = await repository.GetGenerationDayExceptionsAsync(year, range);
        var sessions = await repository.GetSessionsByDateAndTypeAsync(range);
        var sessionIds = sessions.Values.Select(x => x.Id).ToArray();
        var protectedIds = await repository.ListSettledDayIdsAsync(sessionIds);
        protectedIds.AddRange(await repository.ListEvidenceDayIdsAsync(sessionIds));
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

    private async Task Reconcile(List<MealDay> days, Func<MealDay, bool> allowed, string reason)
    {
        var changed = days.Where(x => x.IsCancelled == allowed(x)).ToArray();
        var ids = changed.Select(x => x.Id).ToArray();
        var protectedIds = await repository.ListSettledDayIdsAsync(ids);
        protectedIds.AddRange(await repository.ListEvidenceDayIdsAsync(ids));
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

    private void GuardChangedDays()
    {
        var now = clock.GetUtcNow();
        if (repository.GetPendingMealDays().Any(x => x.CutoffAt <= now))
            throw new CalendarConflict("Đã qua giờ chốt trong lúc lưu. Tải lại lịch.");
    }

    private async Task Audit(string code, DateOnly? date, string kind, string before, object after, string reason)
    {
        var actor = currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
        var name = await repository.GetActorNameAsync(actor);
        repository.AddMealCalendarAudit(new MealCalendarAudit
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

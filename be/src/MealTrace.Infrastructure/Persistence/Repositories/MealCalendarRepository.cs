using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Calendar;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class MealCalendarRepository(MealTraceDbContext db) : IMealCalendarRepository
{
    public async Task<AcademicYear?> FindAcademicYearAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);
        });
    }
    public async Task<MealSchedule?> FindScheduleAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolYear == code);
        });
    }
    public async Task<MealCalendarException?> FindDayExceptionAsync(string code, DateOnly date)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealCalendarExceptions.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolYear == code && x.Date == date);
        });
    }
    public async Task<Dictionary<DateOnly, MealCalendarException>> GetDayExceptionsByDateAsync(string code, DateOnly start, DateOnly end)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealCalendarExceptions.AsNoTracking().Where(x => x.SchoolYear == code && x.Date >= start && x.Date <= end).ToDictionaryAsync(x => x.Date);
        });
    }
    public async Task<List<MealDay>> ListMealDaysAsync(string code, DateOnly start, DateOnly end)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().Where(x => x.SchoolYear == code && x.Date >= start && x.Date <= end).OrderBy(x => x.MealType).ToListAsync();
        });
    }
    public async Task<MealSchedule?> FindTrackedScheduleAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealSchedules.FindAsync(code);
        });
    }
    public async Task<Dictionary<DateOnly, MealCalendarException>> GetTrackedDayExceptionsAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealCalendarExceptions.Where(x => x.SchoolYear == code).ToDictionaryAsync(x => x.Date);
        });
    }
    public async Task<MealCalendarException?> FindTrackedDayExceptionAsync(string code, DateOnly date)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealCalendarExceptions.FindAsync(code, date);
        });
    }
    public async Task<bool> AnyDayHasSettlementAsync(List<MealDay> sessions)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionSettlements.AnyAsync(x => sessions.Select(d => d.Id).Contains(x.MealDayId));
        });
    }
    public async Task<MealDay?> FindTrackedMealDayAsync(Guid mealDayId)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.FindAsync(mealDayId);
        });
    }
    public async Task<List<CalendarHistoryItem>> ListCalendarHistoryAsync(string code)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealCalendarAudits.AsNoTracking().Where(x => x.SchoolYear == code).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Take(50).Select(x => new CalendarHistoryItem
            {
                Id = x.Id,
                Date = x.Date,
                Kind = x.Kind,
                Reason = x.Reason,
                ActorId = x.ActorId,
                ActorName = x.ActorName,
                RecordedAt = x.RecordedAt
            }).ToListAsync();
        });
    }
    public async Task<Dictionary<DateOnly, MealCalendarException>> GetGenerationDayExceptionsAsync(AcademicYear year, GenerateInput range)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealCalendarExceptions.AsNoTracking().Where(x => x.SchoolYear == year.Code && x.Date >= range.From && x.Date <= range.To).ToDictionaryAsync(x => x.Date);
        });
    }
    public async Task<Dictionary<CalendarSessionKey, MealDay>> GetSessionsByDateAndTypeAsync(GenerateInput range)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().Where(x => x.Date >= range.From && x.Date <= range.To).ToDictionaryAsync(x => new CalendarSessionKey
            {
                Date = x.Date,
                MealType = x.MealType
            });
        });
    }
    public async Task<List<Guid>> ListSettledDayIdsAsync(Guid[] sessionIds)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.PortionSettlements.Where(x => sessionIds.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync();
        });
    }
    public async Task<List<Guid>> ListEvidenceDayIdsAsync(Guid[] sessionIds)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealEvidence.Where(x => sessionIds.Contains(x.MealDayId)).Select(x => x.MealDayId).ToListAsync();
        });
    }
    public async Task<string> GetActorNameAsync(Guid actor)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).Where(x => x.Id == actor).Select(x => x.FullName).SingleAsync();
        });
    }

    public void RemoveMealCalendarException(MealCalendarException value) => db.MealCalendarExceptions.Remove(value);
    public void AddMealCalendarException(MealCalendarException value) => db.MealCalendarExceptions.Add(value);
    public void AddMealDay(MealDay value) => db.MealDays.Add(value);
    public void AddMealCalendarAudit(MealCalendarAudit value) => db.MealCalendarAudits.Add(value);
    public void AddMealSchedule(MealSchedule value) => db.MealSchedules.Add(value);
    public Task<AcademicYear?> LockAcademicYearAsync(string code, CancellationToken ct = default) => db.LockAcademicYearAsync(code, ct);
    public Task<List<MealDay>> LockMealDaysAsync(string code, DateOnly from, DateOnly to, CancellationToken ct = default) => db.LockMealDaysAsync(code, from, to, ct);
    public IReadOnlyList<MealDay> GetPendingMealDays() => db.ChangedMealDays.ToList();
}

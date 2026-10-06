using MealTrace.Application.Dtos.Calendar;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IMealCalendarRepository
{
    Task<AcademicYear?> FindAcademicYearAsync(string code);
    Task<MealSchedule?> FindScheduleAsync(string code);
    Task<MealCalendarException?> FindDayExceptionAsync(string code, DateOnly date);
    Task<Dictionary<DateOnly, MealCalendarException>> GetDayExceptionsByDateAsync(string code, DateOnly start, DateOnly end);
    Task<List<MealDay>> ListMealDaysAsync(string code, DateOnly start, DateOnly end);
    Task<MealSchedule?> FindTrackedScheduleAsync(string code);
    Task<Dictionary<DateOnly, MealCalendarException>> GetTrackedDayExceptionsAsync(string code);
    Task<MealCalendarException?> FindTrackedDayExceptionAsync(string code, DateOnly date);
    Task<bool> AnyDayHasSettlementAsync(List<MealDay> sessions);
    Task<MealDay?> FindTrackedMealDayAsync(Guid mealDayId);
    Task<List<CalendarHistoryItem>> ListCalendarHistoryAsync(string code);
    Task<Dictionary<DateOnly, MealCalendarException>> GetGenerationDayExceptionsAsync(AcademicYear year, GenerateInput range);
    Task<Dictionary<CalendarSessionKey, MealDay>> GetSessionsByDateAndTypeAsync(GenerateInput range);
    Task<List<Guid>> ListSettledDayIdsAsync(Guid[] sessionIds);
    Task<List<Guid>> ListEvidenceDayIdsAsync(Guid[] sessionIds);
    Task<string> GetActorNameAsync(Guid actor);

    void RemoveMealCalendarException(MealCalendarException value);
    void AddMealCalendarException(MealCalendarException value);
    void AddMealDay(MealDay value);
    void AddMealCalendarAudit(MealCalendarAudit value);
    void AddMealSchedule(MealSchedule value);
    Task<AcademicYear?> LockAcademicYearAsync(string code, CancellationToken ct = default);
    Task<List<MealDay>> LockMealDaysAsync(string code, DateOnly from, DateOnly to, CancellationToken ct = default);
    IReadOnlyList<MealDay> GetPendingMealDays();
}

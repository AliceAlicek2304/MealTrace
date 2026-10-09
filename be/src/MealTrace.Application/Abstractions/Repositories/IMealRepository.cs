using MealTrace.Application.Models.Persistence;
using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IMealRepository
{
    Task<int> CountMealDaysAsync(MealDayListFilter filter, CancellationToken ct);
    Task<List<MealDaySummary>> SearchMealDaysAsync(MealDayListFilter filter, int page, int size, CancellationToken ct);
    Task<List<MealDay>> ListMealDaysAsync(DateOnly? from, DateOnly? to);
    Task<MealDay?> FindMealDayDetailsAsync(Guid id);
    Task<ReportLineageResponse?> FindReportLineageAsync(Guid id);
}

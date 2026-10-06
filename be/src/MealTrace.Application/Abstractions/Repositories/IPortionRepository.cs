using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IPortionRepository
{
    Task<List<LatestSettlementRow>> ListLatestSettlementsAsync(MealDay day, Guid[]? allowedClasses, CancellationToken ct);
    Task<List<SettlementStudentRow>> ListSettlementStudentsAsync(Guid[] ids, CancellationToken ct);
}

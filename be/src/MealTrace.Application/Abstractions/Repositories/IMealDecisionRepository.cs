using MealTrace.Application.Dtos.Meals;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IMealDecisionRepository
{
    Task<List<MealEnrollmentMember>> ListEligibleMembersAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, CancellationToken ct);
    Task<List<Room>> ListEligibleClassesAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, CancellationToken ct);
    Task<int> CountEligibleMembersAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, Guid? classId, string? search, CancellationToken ct);
    Task<List<MealEnrollmentMember>> SearchEligibleMembersAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, Guid? classId, string? search, int page, int pageSize, CancellationToken ct);
    Task<List<MealDecisionEvidenceRow>> ListDecisionEvidenceAsync(MealDay day, DateTimeOffset asOf, List<MealEnrollmentMember> members, Guid[] ids, CancellationToken ct);
}

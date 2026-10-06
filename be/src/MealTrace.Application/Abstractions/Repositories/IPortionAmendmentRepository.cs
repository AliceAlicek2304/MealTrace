using MealTrace.Application.Dtos.Portions;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IPortionAmendmentRepository
{
    Task<bool> HasTeacherAssignmentAsync(Guid classId, Guid userId);
    Task<string> GetActorNameAsync(Guid userId);
    Task<PortionSettlement?> FindLatestSettlementAsync(Guid dayId, Guid classId);
    Task<MealDay?> FindMealDayAsync(Guid dayId);
    Task<PortionSettlement> GetOriginalSettlementAsync(Guid dayId, Guid classId);
    Task<int> CountAmendmentCandidatesAsync(MealDay day, Guid classId, PortionSettlement current, string? term);
    Task<List<AmendmentCandidateRow>> SearchAmendmentCandidatesAsync(MealDay day, Guid classId, PortionSettlement current, string? term, int candidateNumber);
    Task<int> CountAmendmentsAsync(Guid dayId, Guid classId);
    Task<List<PortionAmendmentSummary>> ListAmendmentsAsync(Guid dayId, Guid classId, int number);
    Task<bool> TeacherCanRequestAmendmentAsync(RequestInput input, Guid userId);
    Task<List<AmendmentCandidateRow>> FindStudentsAsync(Guid[] studentIds);
    Task<List<Enrollment>> FindCandidateEnrollmentsAsync(MealDay day, Guid classId, Guid[] studentIds);
    Task<bool> HasPendingStudentsAsync(Guid settlementId, Guid[] studentIds);
    Task<bool> HasAnyPortionElsewhereAsync(Guid dayId, Guid classId, Guid[] studentIds);
    Task<PortionAmendment?> FindAmendmentDetailsAsync(Guid requestId, Guid dayId);
    Task<PortionAmendment?> FindTrackedAmendmentAsync(Guid requestId, Guid dayId);

    void AddPortionAmendmentResolution(PortionAmendmentResolution value);
    void AddPortionSettlement(PortionSettlement value);
    void AddPortionAmendment(PortionAmendment value);
    Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct = default);
}

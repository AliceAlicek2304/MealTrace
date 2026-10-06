using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IMealExceptionRepository
{
    Task<MealDay?> FindMealDayAsync(Guid id, CancellationToken ct);
    Task<MealDay?> FindHistoryMealDayAsync(Guid id);
    Task<int> CountStudentExceptionsAsync(Guid id, Guid studentId);
    Task<List<MealRegistration>> ListStudentExceptionsAsync(Guid id, Guid studentId, int number, int size);
    Task<MealRegistration?> FindLatestExceptionAsync(Guid id, ExceptionInput input);
    Task<string> GetActorNameAsync(Guid userId);
    Task<Guid[]> ListAssignedClassIdsAsync(CancellationToken ct, Guid userId);
    Task<bool> HasTeacherAssignmentAsync(Guid classId, Guid userId);
    Task<Enrollment?> FindEnrollmentAtCutoffAsync(MealDay day, Guid studentId, DateTimeOffset now);

    void AddMealRegistration(MealRegistration value);
    Task LockStudentAsync(Guid id, CancellationToken ct = default);
    Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct = default);
}

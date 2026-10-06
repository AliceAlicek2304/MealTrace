using MealTrace.Application.Dtos.Students;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IStudentAdministrationRepository
{
    Task<int> CountClassesAsync(string? search);
    Task<List<ClassSummary>> SearchClassesAsync(string? search, int number, int size, DateOnly date);
    Task<SchoolClass?> FindTrackedClassAsync(Guid id);
    Task<bool> ClassNameUsedByOtherClassAsync(Guid id, SchoolClass room, string? name);
    Task<int> CountStudentsAsync(Guid? classId, DateOnly date, string? status, string? search);
    Task<List<StudentSummaryRow>> SearchStudentsAsync(Guid? classId, DateOnly date, string? status, string? search, int number, int size);
    Task<List<ParentStudentRow>> ListStudentParentsAsync(Guid[] ids);
    Task<Student?> FindTrackedStudentAsync(Guid id);
    Task<bool> StudentExistsAsync(Guid id);
    Task<List<EnrollmentHistoryItem>> ListEnrollmentHistoryAsync(Guid id);
    Task<Student?> FindStudentWithEnrollmentsAsync(Guid id);
    Task<bool> ClassExistsAsync(ChangeEnrollment input);

    void AddEnrollment(Enrollment value);
    Task LockStudentAsync(Guid id, CancellationToken ct = default);
}

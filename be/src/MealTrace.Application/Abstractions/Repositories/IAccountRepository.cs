using MealTrace.Application.Dtos.Accounts;
using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IAccountRepository
{
    Task<int> CountAccountsAsync(Guid? classId);
    Task<List<IdentityAccount>> ListAccountsAsync(Guid? classId, int number, int size);
    Task<List<AccountRoleRow>> ListAccountRolesAsync(Guid[] ids);
    Task<List<TeacherAssignment>> ListTeacherAssignmentsAsync(Guid[] ids);
    Task<List<ParentStudent>> ListParentLinksAsync(Guid[] ids);
    Task<List<InspectorGrant>> ListInspectorGrantsAsync(Guid[] ids);
    Task<IdentityAccount?> FindAccountAsync(Guid id);
    Task<Guid[]> ListAssignedClassIdsAsync(Guid id);
    Task<Guid[]> ListLinkedStudentIdsAsync(Guid id);
    Task<InspectorGrant?> FindInspectorGrantAsync(Guid id);
    Task<List<ClassScopeOption>> SearchClassOptionsAsync(string? search, Guid? classId, DateOnly date, int cp, int size);
    Task<List<StudentScopeOption>> SearchStudentOptionsAsync(string? search, Guid? classId, DateOnly date, int sp, int size);
    Task<List<ClassScopeOption>> ListSelectedClassOptionsAsync(Guid[] classIds);
    Task<List<StudentScopeOption>> ListSelectedStudentOptionsAsync(Guid[] studentIds);
    Task<int> CountClassOptionsAsync(string? search, Guid? classId, DateOnly date);
    Task<int> CountStudentOptionsAsync(string? search, Guid? classId, DateOnly date);
    Task<bool> PhoneExistsAsync(string? phone);
    Task<bool> PhoneUsedByOtherAccountAsync(string? phone, Guid id);
    Task<int> CountExistingClassesAsync(AccountInput input);
    Task<int> CountExistingStudentsAsync(AccountInput input);
    Task<int> DeleteTeacherAssignmentsAsync(Guid userId);
    Task<int> DeleteParentLinksAsync(Guid userId);
    Task<InspectorGrant?> FindTrackedInspectorGrantAsync(Guid userId);

    void AddTeacherAssignments(IEnumerable<TeacherAssignment> value);
    void AddParentStudents(IEnumerable<ParentStudent> value);
    void AddInspectorGrant(InspectorGrant value);
    void RemoveInspectorGrant(InspectorGrant value);
    void AddAccountPasswordResetAudit(AccountPasswordResetAudit value);
}

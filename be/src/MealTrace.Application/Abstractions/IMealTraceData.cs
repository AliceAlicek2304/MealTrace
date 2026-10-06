using MealTrace.Application.Dtos.Identity;
using System.Data;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions;

/// <summary>Unit of work and repository ports. All repositories and Identity writes share one scoped transaction.</summary>
public interface IMealTraceData
{
    IEntityRepository<AccountPasswordResetAudit> AccountPasswordResetAudits { get; }
    IEntityRepository<TeacherAssignment> TeacherAssignments { get; }
    IEntityRepository<ParentStudent> ParentStudents { get; }
    IEntityRepository<InspectorGrant> InspectorGrants { get; }
    IEntityRepository<SchoolClass> Classes { get; }
    IEntityRepository<AcademicYear> AcademicYears { get; }
    IEntityRepository<MealSchedule> MealSchedules { get; }
    IEntityRepository<MealCalendarException> MealCalendarExceptions { get; }
    IEntityRepository<MealCalendarAudit> MealCalendarAudits { get; }
    IEntityRepository<Student> Students { get; }
    IEntityRepository<Enrollment> Enrollments { get; }
    IEntityRepository<Ingredient> Ingredients { get; }
    IEntityRepository<IngredientVersion> IngredientVersions { get; }
    IEntityRepository<Recipe> Recipes { get; }
    IEntityRepository<RecipeVersion> RecipeVersions { get; }
    IEntityRepository<RecipeIngredient> RecipeIngredients { get; }
    IEntityRepository<MealDay> MealDays { get; }
    IEntityRepository<MenuDish> MenuDishes { get; }
    IEntityRepository<MealRegistration> MealRegistrations { get; }
    IEntityRepository<MealAbsence> MealAbsences { get; }
    IEntityRepository<PortionSettlement> PortionSettlements { get; }
    IEntityRepository<SettlementStudent> SettlementStudents { get; }
    IEntityRepository<SettlementDecision> SettlementDecisions { get; }
    IEntityRepository<PortionAmendment> PortionAmendments { get; }
    IEntityRepository<PortionAmendmentResolution> PortionAmendmentResolutions { get; }
    IEntityRepository<MealEvidence> MealEvidence { get; }
    IEntityRepository<ReportSnapshot> ReportSnapshots { get; }
    IQueryable<IdentityAccount> Users { get; }
    IQueryable<AccountRole> Roles { get; }
    IQueryable<AccountRoleLink> UserRoles { get; }
    bool IsNew<TEntity>(TEntity entity) where TEntity : class;
    IEnumerable<MealDay> ChangedMealDays { get; }
    IQueryable<Ingredient> SearchIngredients(IQueryable<Ingredient> query, string pattern);
    IQueryExecutor Queries { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default);
    Task LockStudentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AcademicYear?> LockAcademicYearAsync(string code, CancellationToken cancellationToken = default);
    Task<List<MealDay>> LockMealDaysAsync(string code, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    bool IsConflict(Exception exception);
    bool IsUniqueViolation(Exception exception);
    bool IsConcurrencyConflict(Exception exception);
}

public interface IDataTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

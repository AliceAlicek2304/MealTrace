using MealTrace.Application.Dtos.Identity;
using System.Data;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace MealTrace.Infrastructure.Persistence;

public sealed partial class MealTraceDbContext
{
    IEntityRepository<AccountPasswordResetAudit> IMealTraceData.AccountPasswordResetAudits => Repository(AccountPasswordResetAudits);
    IEntityRepository<TeacherAssignment> IMealTraceData.TeacherAssignments => Repository(TeacherAssignments);
    IEntityRepository<ParentStudent> IMealTraceData.ParentStudents => Repository(ParentStudents);
    IEntityRepository<InspectorGrant> IMealTraceData.InspectorGrants => Repository(InspectorGrants);
    IEntityRepository<SchoolClass> IMealTraceData.Classes => Repository(Classes);
    IEntityRepository<AcademicYear> IMealTraceData.AcademicYears => Repository(AcademicYears);
    IEntityRepository<MealSchedule> IMealTraceData.MealSchedules => Repository(MealSchedules);
    IEntityRepository<MealCalendarException> IMealTraceData.MealCalendarExceptions => Repository(MealCalendarExceptions);
    IEntityRepository<MealCalendarAudit> IMealTraceData.MealCalendarAudits => Repository(MealCalendarAudits);
    IEntityRepository<Student> IMealTraceData.Students => Repository(Students);
    IEntityRepository<Enrollment> IMealTraceData.Enrollments => Repository(Enrollments);
    IEntityRepository<Ingredient> IMealTraceData.Ingredients => Repository(Ingredients);
    IEntityRepository<IngredientVersion> IMealTraceData.IngredientVersions => Repository(IngredientVersions);
    IEntityRepository<Recipe> IMealTraceData.Recipes => Repository(Recipes);
    IEntityRepository<RecipeVersion> IMealTraceData.RecipeVersions => Repository(RecipeVersions);
    IEntityRepository<RecipeIngredient> IMealTraceData.RecipeIngredients => Repository(RecipeIngredients);
    IEntityRepository<MealDay> IMealTraceData.MealDays => Repository(MealDays);
    IEntityRepository<MenuDish> IMealTraceData.MenuDishes => Repository(MenuDishes);
    IEntityRepository<MealRegistration> IMealTraceData.MealRegistrations => Repository(MealRegistrations);
    IEntityRepository<MealAbsence> IMealTraceData.MealAbsences => Repository(MealAbsences);
    IEntityRepository<PortionSettlement> IMealTraceData.PortionSettlements => Repository(PortionSettlements);
    IEntityRepository<SettlementStudent> IMealTraceData.SettlementStudents => Repository(SettlementStudents);
    IEntityRepository<SettlementDecision> IMealTraceData.SettlementDecisions => Repository(SettlementDecisions);
    IEntityRepository<PortionAmendment> IMealTraceData.PortionAmendments => Repository(PortionAmendments);
    IEntityRepository<PortionAmendmentResolution> IMealTraceData.PortionAmendmentResolutions => Repository(PortionAmendmentResolutions);
    IEntityRepository<MealEvidence> IMealTraceData.MealEvidence => Repository(MealEvidence);
    IEntityRepository<ReportSnapshot> IMealTraceData.ReportSnapshots => Repository(ReportSnapshots);
    private readonly Dictionary<Type, object> _repositories = [];
    private IEntityRepository<T> Repository<T>(DbSet<T> entities) where T : class
    {
        if (!_repositories.TryGetValue(typeof(T), out var repository))
            _repositories[typeof(T)] = repository = new EfEntityRepository<T>(entities);
        return (IEntityRepository<T>)repository;
    }
    public IQueryExecutor Queries { get; } = new EfQueryExecutor();

    IQueryable<IdentityAccount> IMealTraceData.Users => Users.Select(IdentityService.AccountProjection);
    IQueryable<AccountRole> IMealTraceData.Roles => Roles.Select(role => new AccountRole { Id = role.Id, Name = role.Name });
    IQueryable<AccountRoleLink> IMealTraceData.UserRoles => UserRoles.Select(link => new AccountRoleLink { UserId = link.UserId, RoleId = link.RoleId });
    public bool IsNew<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State == EntityState.Detached;
    public IEnumerable<MealDay> ChangedMealDays => ChangeTracker.Entries<MealDay>()
        .Where(entry => entry.State == EntityState.Modified).Select(entry => entry.Entity);
    public IQueryable<Ingredient> SearchIngredients(IQueryable<Ingredient> query, string pattern) => Database.IsNpgsql()
        ? query.Where(ingredient => EF.Functions.ILike(ingredient.Name, pattern))
        : query.Where(ingredient => EF.Functions.Like(ingredient.Name.ToLower(), pattern.ToLower()));

    public async Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new DataTransaction(await Database.BeginTransactionAsync(cancellationToken));
    public async Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default) =>
        new DataTransaction(await Database.BeginTransactionAsync(isolation, cancellationToken));

    public async Task LockStudentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (Database.IsNpgsql())
            await Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken);
    }
    public async Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken cancellationToken = default) => Database.IsNpgsql()
        ? (await MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault()
        : await MealDays.FindAsync([id], cancellationToken);
    public async Task<AcademicYear?> LockAcademicYearAsync(string code, CancellationToken cancellationToken = default) => Database.IsNpgsql()
        ? (await AcademicYears.FromSqlInterpolated($"SELECT * FROM \"AcademicYears\" WHERE \"Code\" = {code} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault()
        : await AcademicYears.FindAsync([code], cancellationToken);
    public async Task<List<MealDay>> LockMealDaysAsync(string code, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => Database.IsNpgsql()
        ? await MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"SchoolYear\" = {code} AND \"Date\" >= {from} AND \"Date\" <= {to} ORDER BY \"Id\" FOR UPDATE").ToListAsync(cancellationToken)
        : await MealDays.Where(x => x.SchoolYear == code && x.Date >= from && x.Date <= to).ToListAsync(cancellationToken);

    public bool IsConcurrencyConflict(Exception exception) => exception is DbUpdateConcurrencyException;
    public bool IsUniqueViolation(Exception exception) => HasSqlState(exception, PostgresErrorCodes.UniqueViolation);
    public bool IsConflict(Exception exception) => HasSqlState(exception, PostgresErrorCodes.UniqueViolation,
        PostgresErrorCodes.SerializationFailure, PostgresErrorCodes.DeadlockDetected);
    private static bool HasSqlState(Exception exception, params string[] codes)
    {
        // Execution strategies may wrap provider errors. Preserve the original exception for diagnostics.
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException pg && codes.Contains(pg.SqlState)) return true;
        return false;
    }
    private sealed class DataTransaction(IDbContextTransaction transaction) : IDataTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        // Disposal rolls back uncommitted changes, including Identity writes on this same context.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

using System.Data;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MealTrace.Infrastructure.Persistence;

public sealed partial class MealTraceDbContext
{
    internal IEnumerable<MealDay> ChangedMealDays => ChangeTracker.Entries<MealDay>()
        .Where(entry => entry.State == EntityState.Modified).Select(entry => entry.Entity);
    internal IQueryable<Ingredient> SearchIngredients(IQueryable<Ingredient> query, string pattern) => Database.IsNpgsql()
        ? query.Where(ingredient => EF.Functions.ILike(ingredient.Name, pattern))
        : query.Where(ingredient => EF.Functions.Like(ingredient.Name.ToLower(), pattern.ToLower()));

    public async Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) { return await PersistenceErrors.ExecuteAsync(async () => { return new DataTransaction(await Database.BeginTransactionAsync(cancellationToken)); }); }
    public async Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default) { return await PersistenceErrors.ExecuteAsync(async () => { return new DataTransaction(await Database.BeginTransactionAsync(isolation, cancellationToken)); }); }
    public async Task LockStudentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await PersistenceErrors.ExecuteAsync(async () =>
        {
            if (Database.IsNpgsql())
                await Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken);
        });
    }
    public async Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Database.IsNpgsql()
            ? (await MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault()
            : await MealDays.FindAsync([id], cancellationToken);
        });
    }
    public async Task<AcademicYear?> LockAcademicYearAsync(string code, CancellationToken cancellationToken = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Database.IsNpgsql()
            ? (await AcademicYears.FromSqlInterpolated($"SELECT * FROM \"AcademicYears\" WHERE \"Code\" = {code} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault()
            : await AcademicYears.FindAsync([code], cancellationToken);
        });
    }
    public async Task<List<MealDay>> LockMealDaysAsync(string code, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Database.IsNpgsql()
            ? await MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"SchoolYear\" = {code} AND \"Date\" >= {from} AND \"Date\" <= {to} ORDER BY \"Id\" FOR UPDATE").ToListAsync(cancellationToken)
            : await MealDays.Where(x => x.SchoolYear == code && x.Date >= from && x.Date <= to).ToListAsync(cancellationToken);
        });
    }
    private sealed class DataTransaction(IDbContextTransaction transaction) : IDataTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => PersistenceErrors.ExecuteAsync(() => transaction.CommitAsync(cancellationToken));
        // Disposal rolls back uncommitted changes, including Identity writes on this same context.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

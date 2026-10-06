using System.Linq.Expressions;

namespace MealTrace.Application.Abstractions;

/// <summary>Composable entity query and tracked writes. Save/commit belong to the shared unit of work.</summary>
public interface IEntityRepository<TEntity> : IQueryable<TEntity> where TEntity : class
{
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
    ValueTask<TEntity?> FindAsync(params object?[] keyValues);
    ValueTask<TEntity?> FindAsync(object?[] keyValues, CancellationToken cancellationToken);
}

/// <summary>Executes composed LINQ expressions in the persistence adapter, never by loading all rows into memory.</summary>
public interface IQueryExecutor
{
    IQueryable<T> ReadOnly<T>(IQueryable<T> query) where T : class;
    IQueryable<T> Include<T>(IQueryable<T> query, string path) where T : class;
    IQueryable<T> SplitQuery<T>(IQueryable<T> query) where T : class;
    Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<T[]> ArrayAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<T> FirstAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<T> SingleAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct);
    Task<TResult> MaxAsync<T, TResult>(IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken ct);
    Task<Dictionary<TKey, T>> DictionaryAsync<T, TKey>(IQueryable<T> query, Func<T, TKey> key, CancellationToken ct) where TKey : notnull;
    Task<int> DeleteAsync<T>(IQueryable<T> query, CancellationToken ct) where T : class;
}

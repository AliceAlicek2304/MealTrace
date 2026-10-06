using System.Linq.Expressions;

namespace MealTrace.Application.Abstractions;

/// <summary>Convenient syntax for the query execution port. Implementation lives exclusively in Infrastructure.</summary>
public static class QueryExtensions
{
    public static IQueryable<T> AsNoTracking<T>(this IQueryable<T> query, IQueryExecutor executor) where T : class => executor.ReadOnly(query);
    public static IQueryable<T> Include<T>(this IQueryable<T> query, IQueryExecutor executor, string path) where T : class => executor.Include(query, path);
    public static IQueryable<T> AsSplitQuery<T>(this IQueryable<T> query, IQueryExecutor executor) where T : class => executor.SplitQuery(query);
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.ListAsync(query, ct);
    public static Task<T[]> ToArrayAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.ArrayAsync(query, ct);
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.FirstOrDefaultAsync(query, ct);
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, IQueryExecutor executor, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => executor.FirstOrDefaultAsync(query.Where(predicate), ct);
    public static Task<T> FirstAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.FirstAsync(query, ct);
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.SingleOrDefaultAsync(query, ct);
    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, IQueryExecutor executor, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => executor.SingleOrDefaultAsync(query.Where(predicate), ct);
    public static Task<T> SingleAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.SingleAsync(query, ct);
    public static Task<T> SingleAsync<T>(this IQueryable<T> query, IQueryExecutor executor, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => executor.SingleAsync(query.Where(predicate), ct);
    public static Task<bool> AnyAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.AnyAsync(query, ct);
    public static Task<bool> AnyAsync<T>(this IQueryable<T> query, IQueryExecutor executor, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => executor.AnyAsync(query.Where(predicate), ct);
    public static Task<int> CountAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) => executor.CountAsync(query, ct);
    public static Task<int> CountAsync<T>(this IQueryable<T> query, IQueryExecutor executor, Expression<Func<T, bool>> predicate, CancellationToken ct = default) => executor.CountAsync(query.Where(predicate), ct);
    public static Task<TResult> MaxAsync<T, TResult>(this IQueryable<T> query, IQueryExecutor executor, Expression<Func<T, TResult>> selector, CancellationToken ct = default) => executor.MaxAsync(query, selector, ct);
    public static Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this IQueryable<T> query, IQueryExecutor executor, Func<T, TKey> key, CancellationToken ct = default) where TKey : notnull => executor.DictionaryAsync(query, key, ct);
    public static Task<int> ExecuteDeleteAsync<T>(this IQueryable<T> query, IQueryExecutor executor, CancellationToken ct = default) where T : class => executor.DeleteAsync(query, ct);
}

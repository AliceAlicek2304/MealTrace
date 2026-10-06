using System.Linq.Expressions;
using MealTrace.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence;

internal sealed class EfQueryExecutor : IQueryExecutor
{
    public IQueryable<T> ReadOnly<T>(IQueryable<T> query) where T : class => query.AsNoTracking();
    public IQueryable<T> Include<T>(IQueryable<T> query, string path) where T : class => query.Include(path);
    public IQueryable<T> SplitQuery<T>(IQueryable<T> query) where T : class => query.AsSplitQuery();
    public Task<List<T>> ListAsync<T>(IQueryable<T> query, CancellationToken ct) => query.ToListAsync(ct);
    public Task<T[]> ArrayAsync<T>(IQueryable<T> query, CancellationToken ct) => query.ToArrayAsync(ct);
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct) => query.FirstOrDefaultAsync(ct);
    public Task<T> FirstAsync<T>(IQueryable<T> query, CancellationToken ct) => query.FirstAsync(ct);
    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct) => query.SingleOrDefaultAsync(ct);
    public Task<T> SingleAsync<T>(IQueryable<T> query, CancellationToken ct) => query.SingleAsync(ct);
    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken ct) => query.AnyAsync(ct);
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct) => query.CountAsync(ct);
    public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken ct) => query.MaxAsync(selector, ct);
    public Task<Dictionary<TKey, T>> DictionaryAsync<T, TKey>(IQueryable<T> query, Func<T, TKey> key, CancellationToken ct) where TKey : notnull => query.ToDictionaryAsync(key, ct);
    public Task<int> DeleteAsync<T>(IQueryable<T> query, CancellationToken ct) where T : class => query.ExecuteDeleteAsync(ct);
}

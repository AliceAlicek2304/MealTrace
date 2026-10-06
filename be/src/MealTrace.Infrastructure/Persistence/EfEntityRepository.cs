using System.Collections;
using System.Linq.Expressions;
using MealTrace.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence;

internal sealed class EfEntityRepository<TEntity>(DbSet<TEntity> entities) : IEntityRepository<TEntity>, IAsyncEnumerable<TEntity> where TEntity : class
{
    // Delegate expression and provider directly to EF. LINQ joins, filters and paging remain server-side.
    private IQueryable<TEntity> Query => entities;
    public Type ElementType => Query.ElementType;
    public Expression Expression => Query.Expression;
    public IQueryProvider Provider => Query.Provider;
    public IEnumerator<TEntity> GetEnumerator() => Query.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default) => entities.AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
    public void Add(TEntity entity) => entities.Add(entity);
    public void AddRange(IEnumerable<TEntity> values) => entities.AddRange(values);
    public void Remove(TEntity entity) => entities.Remove(entity);
    public void RemoveRange(IEnumerable<TEntity> values) => entities.RemoveRange(values);
    public ValueTask<TEntity?> FindAsync(params object?[] keyValues) => entities.FindAsync(keyValues);
    public ValueTask<TEntity?> FindAsync(object?[] keyValues, CancellationToken cancellationToken) => entities.FindAsync(keyValues, cancellationToken);
}

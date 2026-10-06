using System.Data;
using MealTrace.Application.Abstractions;
namespace MealTrace.Infrastructure.Persistence;
public sealed class EfUnitOfWork(MealTraceDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => PersistenceErrors.ExecuteAsync(() => db.SaveChangesAsync(ct));
    public Task<IDataTransaction> BeginTransactionAsync(CancellationToken ct = default) => db.BeginTransactionAsync(ct);
    public Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken ct = default) => db.BeginTransactionAsync(isolation, ct);
}

using System.Data;

namespace MealTrace.Application.Abstractions;
/// <summary>Coordinates repository and Identity writes in the same scoped database transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default);
}

using MealTrace.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace MealTrace.Infrastructure.Persistence;
internal static class PersistenceErrors
{
    public static PersistenceConflictKind? Classify(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return PersistenceConflictKind.StaleWrite;
            if (current is PostgresException pg)
            {
                if (pg.SqlState == PostgresErrorCodes.UniqueViolation) return PersistenceConflictKind.DuplicateKey;
                if (pg.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                    return PersistenceConflictKind.ConcurrentTransaction;
            }
        }
        return null;
    }
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (Exception ex) when (Classify(ex) is not null) { throw new PersistenceConflictException(Classify(ex)!.Value, ex); }
    }
    public static async Task ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception ex) when (Classify(ex) is not null) { throw new PersistenceConflictException(Classify(ex)!.Value, ex); }
    }
}

using Npgsql;

namespace MealTrace.Api.Features;

internal static class DatabaseConflicts
{
    public static bool IsConflict(Exception exception)
    {
        // Npgsql's execution strategy can wrap serialization failures in InvalidOperationException.
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException pg && pg.SqlState is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                return true;
        return false;
    }
}

namespace MealTrace.Application.Exceptions;
public enum PersistenceConflictKind { DuplicateKey, StaleWrite, ConcurrentTransaction }
public sealed class PersistenceConflictException(PersistenceConflictKind kind, Exception innerException)
    : Exception("The data changed concurrently or violates a unique key.", innerException)
{
    public PersistenceConflictKind Kind { get; } = kind;
}

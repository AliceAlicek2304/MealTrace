using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MealTrace.Kitchen.Services;

/// <summary>
/// Business error. KitchenProblemFilter turns it into application/problem+json
/// carrying the HTTP status and a machine-readable "code".
/// </summary>
public sealed class KitchenException : Exception
{
    public KitchenException(int status, string code, string message,
        IDictionary<string, object?>? extensions = null) : base(message)
    {
        Status = status;
        Code = code;
        Extensions = extensions;
    }

    public int Status { get; }
    public string Code { get; }
    public IDictionary<string, object?>? Extensions { get; }

    public static KitchenException NotFound(string code, string message) => new(404, code, message);

    public static KitchenException NutrientDataMissing(
        IReadOnlyList<object> missingIngredients, DateTimeOffset validAt, DateTimeOffset knownAt) =>
        new(422, "NUTRIENT_DATA_MISSING",
            "Nutrient data is missing for one or more ingredients at the requested time.",
            new Dictionary<string, object?>
            {
                ["missingIngredients"] = missingIngredients,
                ["validAt"] = validAt.ToVn(),
                ["knownAt"] = knownAt.ToVn()
            });
}

internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = new List<string>();
        list.Add(message);
    }

    public void ThrowIfAny()
    {
        if (_errors.Count == 0) return;

        throw new KitchenException(400, "VALIDATION_ERROR", "One or more validation errors occurred.",
            new Dictionary<string, object?>
            {
                ["errors"] = _errors.ToDictionary(e => e.Key, e => e.Value.ToArray())
            });
    }
}

internal static class KitchenTime
{
    public static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    /// <summary>Npgsql only reads/writes timestamptz as UTC (offset 0), so values are stored in UTC and shown as +07:00.</summary>
    public static DateTimeOffset ToVn(this DateTimeOffset value) => value.ToOffset(VnOffset);
}

internal static class KitchenDb
{
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

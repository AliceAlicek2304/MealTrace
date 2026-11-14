using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IIngredientRepository
{
    Task<int> CountIngredientsAsync(string? search, CancellationToken ct);
    Task<List<Ingredient>> SearchIngredientsAsync(string? search, int skip, int size, CancellationToken ct);
    Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken ct);
    Task<bool> IngredientNameExistsAsync(string name, CancellationToken ct);

    /// <summary>
    /// Version history of the given ingredients. With knownUtc, only rows recorded at or before it (what the system
    /// knew then); with null, every row. Validity is NOT filtered: superseding rows can have another interval.
    /// </summary>
    Task<List<IngredientVersion>> ListVersionsAsync(Guid[] ids, DateTimeOffset? knownUtc, CancellationToken ct);

    void AddIngredient(Ingredient value);
    void AddIngredientVersion(IngredientVersion value);
}

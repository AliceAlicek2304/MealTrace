using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IIngredientRepository
{
    Task<int> CountIngredientsAsync(string? search, CancellationToken ct);
    Task<List<Ingredient>> SearchIngredientsAsync(string? search, int skip, int size, CancellationToken ct);
    Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken ct);
    Task<int?> GetLatestVersionNumberAsync(Guid ingredientId, CancellationToken ct);
    Task<List<IngredientVersion>> ListEffectiveVersionCandidatesAsync(Guid[] ids, DateTimeOffset validUtc, DateTimeOffset knownUtc, CancellationToken ct);

    void AddIngredient(Ingredient value);
    void AddIngredientVersion(IngredientVersion value);
}

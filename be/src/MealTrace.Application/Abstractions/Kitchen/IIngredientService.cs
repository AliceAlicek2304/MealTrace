using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface IIngredientService
{
    Task<IngredientCreatedResponse> CreateAsync(CreateIngredientRequest request, CancellationToken ct);

    Task<IngredientListPage> ListAsync(string? search, int? page, int? pageSize, CancellationToken ct);

    Task<IngredientVersionCreatedResponse> AddVersionAsync(
        Guid ingredientId, CreateIngredientVersionRequest request, CancellationToken ct);

    /// <summary>
    /// Picks, per ingredient, the version in force at (validAt, knownAt) using the official temporal rule
    /// (see <c>IngredientTimeline</c>). Ingredients with no matching version are absent from the result
    /// (callers decide how to report that).
    /// </summary>
    Task<Dictionary<Guid, IngredientVersion>> ResolveEffectiveAsync(
        IReadOnlyCollection<Guid> ingredientIds, DateTimeOffset validAt, DateTimeOffset knownAt, CancellationToken ct);
}

using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

public interface IServingRepository
{
    Task<ServingVersion?> FindVersionByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct = default);

    Task<Serving?> FindServingWithVersionAsync(Guid servingId, CancellationToken ct = default);

    Task<Serving?> FindServingByClassAndMealDayAsync(Guid mealDayId, Guid classId, CancellationToken ct = default);

    Task<bool> MealDayIsActiveAndMatchesClassAsync(Guid mealDayId, Guid classId, CancellationToken ct = default);
    Task<bool> SettlementMatchesSessionAsync(Guid settlementId, Guid mealDayId, Guid classId, string snapshotToken, CancellationToken ct = default);
    Task<bool> RecipesExistAsync(Guid[] recipeVersionIds, CancellationToken ct = default);
    Task<bool> IngredientsExistAsync(Guid[] ingredientIds, (Guid IngredientId, Guid NutrientVersionId)[] nutrientPairs, CancellationToken ct = default);

    void AddServing(Serving serving);
    void AddServingVersion(ServingVersion version);
}

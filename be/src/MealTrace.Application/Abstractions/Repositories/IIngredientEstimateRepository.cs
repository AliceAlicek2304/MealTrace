using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

public interface IIngredientEstimateRepository
{
    Task<IngredientEstimate?> FindAsync(Guid id, CancellationToken ct);
    void Add(IngredientEstimate value);
}

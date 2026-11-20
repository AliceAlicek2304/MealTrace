using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class IngredientEstimateRepository(MealTraceDbContext db) : IIngredientEstimateRepository
{
    public async Task<IngredientEstimate?> FindAsync(Guid id, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.IngredientEstimates.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        });
    }
    public void Add(IngredientEstimate value) => db.IngredientEstimates.Add(value);
}

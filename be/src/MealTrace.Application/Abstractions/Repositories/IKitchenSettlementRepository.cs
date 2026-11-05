using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

public interface IKitchenSettlementRepository
{
    Task<List<MealDay>> ListSettledMealDaysAsync(DateOnly? date, int skip, int take, CancellationToken ct);
    Task<int> CountSettledMealDaysAsync(DateOnly? date, CancellationToken ct);
    Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct);
    Task<MealDay?> FindSettledMealDayWithDetailsAsync(Guid mealDayId, CancellationToken ct);
    Task<string> GetActorNameAsync(Guid userId, CancellationToken ct);
    void AddAcknowledgement(SettlementAcknowledgement acknowledgement);
    Task<List<SettlementAcknowledgement>> ListAcknowledgementsAsync(Guid mealDayId, CancellationToken ct);
}

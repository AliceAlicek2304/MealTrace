using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface IKitchenSettlementService
{
    Task<List<KitchenSettlementSummary>> ListSettlementsAsync(DateOnly? date, CancellationToken ct);
    Task<KitchenSettlementDiffResponse> GetSettlementDiffAsync(Guid mealDayId, CancellationToken ct);
    Task<SettlementAcknowledgementResponse> AcknowledgeSettlementAsync(Guid mealDayId, AcknowledgeSettlementRequest request, CancellationToken ct);
    Task<List<SettlementAcknowledgementResponse>> ListAcknowledgementsAsync(Guid mealDayId, CancellationToken ct);
}

using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Api.Features;

internal static class KitchenSettlementEndpoints
{
    public static void MapKitchenSettlementEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/settlements").WithTags("Kitchen - Settlements");

        // GET /api/kitchen/settlements?date=
        group.MapGet("", async (DateOnly? date, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.ListSettlementsAsync(date, ct)))
            .Produces<List<KitchenSettlementSummary>>();

        // GET /api/kitchen/settlements/{id:guid}/diff
        group.MapGet("/{id:guid}/diff", async (Guid id, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.GetSettlementDiffAsync(id, ct)))
            .Produces<KitchenSettlementDiffResponse>();

        // POST /api/kitchen/settlements/{id:guid}/acknowledge
        group.MapPost("/{id:guid}/acknowledge", async (Guid id, AcknowledgeSettlementRequest request, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Json(await service.AcknowledgeSettlementAsync(id, request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<SettlementAcknowledgementResponse>(StatusCodes.Status201Created);

        // GET /api/kitchen/settlements/{id:guid}/acknowledgements
        group.MapGet("/{id:guid}/acknowledgements", async (Guid id, IKitchenSettlementService service, CancellationToken ct) =>
            Results.Ok(await service.ListAcknowledgementsAsync(id, ct)))
            .Produces<List<SettlementAcknowledgementResponse>>();
    }
}

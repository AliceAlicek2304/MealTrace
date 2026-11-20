using System.Text;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Features.Kitchen;

namespace MealTrace.Api.Features;

internal static class IngredientEstimateEndpoints
{
    /// <summary>Planning estimates (not purchasing, not stock). Kitchen staff and Admin.</summary>
    public static void MapIngredientEstimateEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/ingredient-estimates").WithTags("Kitchen - Ingredient estimates");

        // 409 STALE_SETTLEMENT | 422 MENU_NOT_PUBLISHED / UNIT_CONVERSION_MISSING / LEGACY_CONVERSION_UNVERIFIED
        group.MapPost("", async (IngredientEstimateRequest request, IIngredientEstimateService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<IngredientEstimateResponse>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (Guid id, IIngredientEstimateService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct))).Produces<IngredientEstimateResponse>();

        group.MapGet("/{id:guid}/export", async (Guid id, string? format, IIngredientEstimateService service, CancellationToken ct) =>
        {
            if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                throw new KitchenException(400, "UNSUPPORTED_FORMAT", "Only format=csv is supported.");

            var csv = await service.ExportCsvAsync(id, ct);
            // UTF-8 with a byte-order mark so spreadsheets read Vietnamese names correctly.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return Results.File(bytes, "text/csv; charset=utf-8", $"ingredient-estimate-{id}.csv");
        });
    }
}

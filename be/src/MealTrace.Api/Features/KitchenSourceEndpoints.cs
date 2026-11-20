using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Api.Features;

/// <summary>Suppliers (immutable revisions) and dated ingredient source entries. Kitchen staff and Admin only.</summary>
internal static class KitchenSourceEndpoints
{
    public static void MapKitchenSourceEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var suppliers = kitchen.MapGroup("/suppliers").WithTags("Kitchen - Suppliers");

        suppliers.MapPost("", async (SupplierRequest request, ISupplierService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<SupplierCreatedResponse>(StatusCodes.Status201Created);

        // 409 SUPPLIER_VERSION_CONFLICT on a stale expectedVersion
        suppliers.MapPost("/{id:guid}/versions", async (Guid id, SupplierCorrectionRequest request, ISupplierService service, CancellationToken ct) =>
            Results.Json(await service.AddVersionAsync(id, request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<SupplierCreatedResponse>(StatusCodes.Status201Created);

        suppliers.MapGet("", async (string? search, int? page, int? pageSize, ISupplierService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page, pageSize, ct))).Produces<SupplierPage>();

        suppliers.MapGet("/{id:guid}/history", async (Guid id, ISupplierService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(id, ct))).Produces<SupplierHistory>();

        var entries = kitchen.MapGroup("/ingredient-source-entries").WithTags("Kitchen - Ingredient sources");

        entries.MapPost("", async (SourceEntryRequest request, IIngredientSourceService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<SourceEntryCreatedResponse>(StatusCodes.Status201Created);

        // 409 SOURCE_ENTRY_VERSION_CONFLICT on a stale expectedVersion
        entries.MapPost("/{id:guid}/versions", async (Guid id, SourceEntryCorrectionRequest request, IIngredientSourceService service, CancellationToken ct) =>
            Results.Json(await service.AddVersionAsync(id, request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<SourceEntryCreatedResponse>(StatusCodes.Status201Created);

        entries.MapGet("", async (Guid? ingredientId, DateOnly? from, DateOnly? to, int? page, int? pageSize,
                                  IIngredientSourceService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ingredientId, from, to, page, pageSize, ct))).Produces<SourceEntryPage>();

        entries.MapGet("/{id:guid}/history", async (Guid id, IIngredientSourceService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(id, ct))).Produces<SourceEntryHistory>();
    }
}

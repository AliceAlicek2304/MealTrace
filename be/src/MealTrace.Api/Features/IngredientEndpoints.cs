using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Abstractions.Kitchen;

namespace MealTrace.Api.Features;

internal static class IngredientEndpoints
{
    public static void MapIngredientEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/ingredients").WithTags("Kitchen - Ingredients");

        // POST /api/kitchen/ingredients
        group.MapPost("", async (CreateIngredientRequest request, IIngredientService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created)).Produces<IngredientCreatedResponse>(StatusCodes.Status201Created);

        // GET /api/kitchen/ingredients?search=&page=&pageSize=
        group.MapGet("", async (string? search, int? page, int? pageSize, IIngredientService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page, pageSize, ct))).Produces<PagedResult<IngredientListItem>>();

        // POST /api/kitchen/ingredients/{id}/ingredient-versions  (append-only: no PUT/DELETE)
        group.MapPost("/{id:guid}/ingredient-versions",
            async (Guid id, CreateIngredientVersionRequest request, IIngredientService service, CancellationToken ct) =>
                Results.Json(await service.AddVersionAsync(id, request, ct), statusCode: StatusCodes.Status201Created)).Produces<IngredientVersionCreatedResponse>(StatusCodes.Status201Created);
    }
}

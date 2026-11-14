using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Abstractions.Kitchen;

namespace MealTrace.Api.Features;

internal static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/recipes").WithTags("Kitchen - Recipes");

        // POST /api/kitchen/recipes  -> 201 { recipeId, version: 1, versionId, recordedAt, recordedByUserId, roundingRule }
        // 422 NUTRIENT_DATA_MISSING | UNIT_CONVERSION_MISSING
        group.MapPost("", async (CreateRecipeRequest request, IRecipeService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created)).Produces<RecipeVersionCreatedResponse>(StatusCodes.Status201Created);

        // POST /api/kitchen/recipes/{id}/versions  -> 201 { recipeId, version: n+1, previousVersion, previousVersionId, ... }
        // 409 RECIPE_VERSION_CONFLICT on stale expectedVersion
        group.MapPost("/{id:guid}/versions",
            async (Guid id, CreateRecipeVersionRequest request, IRecipeService service, CancellationToken ct) =>
                Results.Json(await service.AddVersionAsync(id, request, ct), statusCode: StatusCodes.Status201Created)).Produces<RecipeVersionCreatedResponse>(StatusCodes.Status201Created);

        // GET /api/kitchen/recipes/{id}/versions/{v}/nutrition?mode=&validAt=&knownAt=&ageGroup=
        //   mode=recompute (default): ingredient versions resolved at validAt/knownAt (default: now)
        //   mode=pinned:              ingredient versions stored on the recipe lines; validAt/knownAt not allowed
        // Note: a "+" in an offset such as +07:00 must be URL-encoded as %2B.
        group.MapGet("/{id:guid}/versions/{v:int}/nutrition",
            async (Guid id, int v, string? mode, DateTimeOffset? validAt, DateTimeOffset? knownAt, string? ageGroup,
                   INutritionService service, CancellationToken ct) =>
                Results.Ok(await service.CalculateAsync(id, v, new NutritionQuery(mode, validAt, knownAt, ageGroup), ct)))
            .Produces<NutritionResponse>();
    }
}

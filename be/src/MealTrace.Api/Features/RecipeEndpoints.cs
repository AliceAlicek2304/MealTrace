using MealTrace.Kitchen.Services;

namespace MealTrace.Kitchen.Endpoints;

internal static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/recipes").WithTags("Kitchen - Recipes");

        // POST /api/kitchen/recipes  -> 201 { recipeId, version: 1 }
        group.MapPost("", async (CreateRecipeRequest request, IRecipeService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created));

        // POST /api/kitchen/recipes/{id}/versions  -> 201 { recipeId, version: n+1 } | 409 on stale expectedVersion
        group.MapPost("/{id:guid}/versions",
            async (Guid id, CreateRecipeVersionRequest request, IRecipeService service, CancellationToken ct) =>
                Results.Json(await service.AddVersionAsync(id, request, ct), statusCode: StatusCodes.Status201Created));

        // GET /api/kitchen/recipes/{id}/versions/{v}/nutrition?validAt=&knownAt=
        // Note: a "+" in an offset such as +07:00 must be URL-encoded as %2B.
        group.MapGet("/{id:guid}/versions/{v:int}/nutrition",
            async (Guid id, int v, DateTimeOffset? validAt, DateTimeOffset? knownAt,
                   INutritionService service, CancellationToken ct) =>
                Results.Ok(await service.CalculateAsync(id, v, validAt, knownAt, ct)));
    }
}

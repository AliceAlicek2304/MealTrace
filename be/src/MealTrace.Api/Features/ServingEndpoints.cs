using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Api.Features;

internal static class ServingEndpoints
{
    public static void MapServingEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/servings").WithTags("Kitchen - Servings");

        group.MapPost("", async (CreateServingRequest request, IServingService service, CancellationToken ct) =>
        {
            var result = await service.RecordServingAsync(request, ct);
            return Results.Json(result, statusCode: StatusCodes.Status201Created);
        }).Produces<ServingResponse>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (Guid id, IServingService service, CancellationToken ct) =>
        {
            var result = await service.GetServingAsync(id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }).Produces<ServingDetailResponse>(StatusCodes.Status200OK).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/versions", async (Guid id, CreateServingVersionRequest request, IServingService service, CancellationToken ct) =>
        {
            var result = await service.AddServingVersionAsync(id, request, ct);
            return Results.Json(result, statusCode: StatusCodes.Status201Created);
        }).Produces<ServingResponse>(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}/history", async (Guid id, IServingService service, CancellationToken ct) =>
        {
            var result = await service.GetServingHistoryAsync(id, ct);
            return Results.Ok(result);
        }).Produces<List<ServingHistoryItem>>(StatusCodes.Status200OK);
    }
}

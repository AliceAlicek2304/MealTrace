using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Api.Features;

internal static class NutritionReferenceEndpoints
{
    /// <summary>Kitchen staff and Admin may read the documented age-group ranges.</summary>
    public static void MapNutritionReferenceEndpoints(this IEndpointRouteBuilder kitchen)
    {
        kitchen.MapGet("/nutrition-references", async (string? ageGroup, DateTimeOffset? validAt, DateTimeOffset? knownAt,
                                                       INutritionReferenceService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ageGroup, validAt, knownAt, ct)))
            .WithTags("Kitchen - Nutrition references").Produces<NutritionReferenceList>();
    }

    /// <summary>Only an Admin records a range, and only with its source; a correction supersedes, it never edits.</summary>
    public static void MapAdminNutritionReferenceEndpoints(this IEndpointRouteBuilder admin)
    {
        // 409 REFERENCE_OVERLAP | REFERENCE_ALREADY_SUPERSEDED | REFERENCE_CONFLICT
        admin.MapPost("/nutrition-references", async (CreateNutritionReferenceRequest request, INutritionReferenceService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created))
            .WithTags("Admin - Nutrition references").Produces<NutritionReferenceCreatedResponse>(StatusCodes.Status201Created);
    }
}

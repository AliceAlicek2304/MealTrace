using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Api.Features;

internal static class MenuEndpoints
{
    /// <summary>Drafting, versioning, submitting and reading menus: Kitchen staff and Admin.</summary>
    public static void MapMenuEndpoints(this IEndpointRouteBuilder kitchen)
    {
        var group = kitchen.MapGroup("/menus").WithTags("Kitchen - Menus");

        // 404 MEAL_DAY_NOT_FOUND | 409 MEAL_DAY_CANCELLED / MENU_ALREADY_EXISTS | 400 unknown recipe version
        group.MapPost("", async (CreateMenuRequest request, IMenuService service, CancellationToken ct) =>
            Results.Json(await service.CreateAsync(request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<MenuCreatedResponse>(StatusCodes.Status201Created);

        group.MapGet("", async (DateOnly? from, DateOnly? to, int? page, int? pageSize, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(from, to, page, pageSize, ct))).Produces<MenuPage>();

        group.MapGet("/{id:guid}", async (Guid id, int? version, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, version, ct))).Produces<MenuView>();

        // 409 MENU_VERSION_CONFLICT (stale expectedVersion) | MENU_STATE_INVALID (a submitted version is under review)
        group.MapPost("/{id:guid}/versions", async (Guid id, CreateMenuVersionRequest request, IMenuService service, CancellationToken ct) =>
            Results.Json(await service.AddVersionAsync(id, request, ct), statusCode: StatusCodes.Status201Created))
            .Produces<MenuCreatedResponse>(StatusCodes.Status201Created);

        group.MapPost("/{id:guid}/submit", async (Guid id, MenuTransitionRequest request, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.SubmitAsync(id, request, ct))).Produces<MenuTransitionResponse>();

        group.MapGet("/{id:guid}/history", async (Guid id, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(id, ct))).Produces<MenuHistory>();

        // mode=pinned (default): the frozen snapshot taken at publication.
        // mode=recompute: a separate view at validAt/knownAt, never stored. Note: "+" in an offset must be sent as %2B.
        group.MapGet("/{id:guid}/nutrition", async (Guid id, int? version, string? mode, string? ageGroup,
                                                    DateTimeOffset? validAt, DateTimeOffset? knownAt,
                                                    IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.GetNutritionAsync(id, version, mode, ageGroup, validAt, knownAt, ct)))
            .Produces<MenuNutritionResponse>();
    }

    /// <summary>Review and publication are Admin decisions.</summary>
    public static void MapAdminMenuEndpoints(this IEndpointRouteBuilder admin)
    {
        var group = admin.MapGroup("/menus").WithTags("Admin - Menus");

        group.MapPost("/{id:guid}/review", async (Guid id, MenuReviewRequest request, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.ReviewAsync(id, request, ct))).Produces<MenuTransitionResponse>();

        // 422 NUTRIENT_DATA_INCOMPLETE | LEGACY_CONVERSION_UNVERIFIED | UNIT_CONVERSION_MISSING: nothing is stored then.
        group.MapPost("/{id:guid}/publish", async (Guid id, MenuPublishRequest request, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.PublishAsync(id, request, ct))).Produces<MenuTransitionResponse>();
    }

    /// <summary>A linked parent sees the published, frozen menu of their own child only.</summary>
    public static void MapParentMenuEndpoints(this IEndpointRouteBuilder parent)
    {
        parent.MapGet("/{studentId:guid}/menus", async (Guid studentId, DateOnly from, DateOnly to, IMenuService service, CancellationToken ct) =>
            Results.Ok(await service.ListForParentAsync(studentId, from, to, ct)))
            .WithTags("Parent - Menus").Produces<ParentMenuResponse>();
    }
}

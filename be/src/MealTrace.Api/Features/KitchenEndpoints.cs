using MealTrace.Kitchen.Services;

namespace MealTrace.Kitchen.Endpoints;

public static class KitchenEndpoints
{
    /// <summary>Call once from Program.cs: app.MapKitchenEndpoints();</summary>
    public static IEndpointRouteBuilder MapKitchenEndpoints(this IEndpointRouteBuilder app)
    {
        var kitchen = app.MapGroup("/api/kitchen")
            // JWT Bearer: anonymous => 401, Teacher/Parent => 403.
            .RequireAuthorization(policy => policy.RequireRole("Kitchen", "Admin"))
            .AddEndpointFilter<KitchenProblemFilter>();

        kitchen.MapIngredientEndpoints();
        kitchen.MapRecipeEndpoints();

        return app;
    }
}

/// <summary>Maps KitchenException to application/problem+json with a business "code".</summary>
internal sealed class KitchenProblemFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (KitchenException ex)
        {
            var extensions = new Dictionary<string, object?>(ex.Extensions ?? new Dictionary<string, object?>())
            {
                ["code"] = ex.Code
            };

            return Results.Problem(statusCode: ex.Status, detail: ex.Message, extensions: extensions);
        }
    }
}

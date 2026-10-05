using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public static class KitchenEndpoints
{
    public static IEndpointRouteBuilder MapKitchenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/kitchen").WithTags("Kitchen").RequireAuthorization();

        api.MapGet("/ingredients", async (MealTraceDbContext db) =>
        {
            var items = await KitchenService.ListIngredientsAsync(db);
            return Results.Ok(items);
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("ListKitchenIngredients");

        api.MapPost("/ingredients", async (CreateIngredientRequest request, MealTraceDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Ingredient name is required." });

            var item = new Ingredient
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Unit = request.Unit.Trim()
            };

            db.Ingredients.Add(item);
            await db.SaveChangesAsync();

            return Results.Created($"/api/kitchen/ingredients/{item.Id}", new
            {
                item.Id,
                item.Name,
                item.Unit
            });
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("CreateKitchenIngredient");

        api.MapPost("/ingredients/{ingredientId:guid}/versions", async (Guid ingredientId, CreateIngredientVersionRequest request, MealTraceDbContext db) =>
        {
            var ingredient = await db.Ingredients
                .Include(x => x.Versions)
                .FirstOrDefaultAsync(x => x.Id == ingredientId);

            if (ingredient is null)
                return Results.NotFound();

            try
            {
                var version = await KitchenService.CreateIngredientVersionAsync(db, ingredientId, request);
                return Results.Created($"/api/kitchen/ingredients/{ingredientId}/versions/{version.Version}", new
                {
                    version.Id,
                    version.IngredientId,
                    version.Version,
                    version.EnergyKcalPer100G,
                    version.ProteinGPer100G,
                    version.PricePerKg,
                    version.EdibleFraction,
                    version.EffectiveFrom,
                    version.RecordedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("CreateKitchenIngredientVersion");

        api.MapGet("/recipes", async (MealTraceDbContext db) =>
        {
            var items = await KitchenService.ListRecipesAsync(db);
            return Results.Ok(items);
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("ListKitchenRecipes");

        api.MapPost("/recipes", async (CreateRecipeRequest request, MealTraceDbContext db) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Recipe name is required." });

            if (request.Ingredients is null || request.Ingredients.Count == 0)
                return Results.BadRequest(new { message = "At least one ingredient is required." });

            try
            {
                var recipe = await KitchenService.CreateRecipeAsync(db, request.Name);
                var version = await KitchenService.CreateRecipeVersionAsync(db, recipe.Id, new CreateRecipeVersionRequest(
                    ExpectedVersion: 0,
                    Ingredients: request.Ingredients));

                return Results.Created($"/api/kitchen/recipes/{recipe.Id}/versions/{version.Version}", new
                {
                    recipe.Id,
                    recipe.Name,
                    version.Version,
                    version.RecordedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("CreateKitchenRecipe");

        api.MapPost("/recipes/{recipeId:guid}/versions", async (Guid recipeId, CreateRecipeVersionRequest request, MealTraceDbContext db) =>
        {
            try
            {
                var version = await KitchenService.CreateRecipeVersionAsync(db, recipeId, request);
                return Results.Created($"/api/kitchen/recipes/{recipeId}/versions/{version.Version}", new
                {
                    version.Id,
                    version.RecipeId,
                    version.Version,
                    version.RecordedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("CreateKitchenRecipeVersion");

        api.MapGet("/recipes/{recipeId:guid}/versions/{version:int}/nutrition", async (Guid recipeId, int version, DateTimeOffset? asOf, MealTraceDbContext db) =>
        {
            var effectiveAsOf = asOf ?? DateTimeOffset.UtcNow;

            try
            {
                var nutrition = await KitchenService.GetRecipeNutritionAsync(db, recipeId, version, effectiveAsOf);
                return Results.Ok(nutrition);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
          .WithName("GetKitchenRecipeNutrition");

        return app;
    }
}

using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MealTrace.Api.Features;

public static class KitchenEndpoints
{
    public static IEndpointRouteBuilder MapKitchenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/kitchen").WithTags("Kitchen").RequireAuthorization();

        // ========== INGREDIENTS ==========

        /// <summary>
        /// POST /api/kitchen/ingredients
        /// Create a new ingredient with basic metadata
        /// </summary>
        api.MapPost("/ingredients", CreateIngredient)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("CreateIngredient")
            .WithOpenApi();

        /// <summary>
        /// GET /api/kitchen/ingredients?search=&page=1&pageSize=25
        /// List ingredients with current nutrient summary
        /// </summary>
        api.MapGet("/ingredients", ListIngredients)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("ListIngredients")
            .WithOpenApi();

        /// <summary>
        /// POST /api/kitchen/ingredients/{id}/ingredient-versions
        /// Append a new nutrient version (bitemporal: validFrom/validTo + recordedAt)
        /// </summary>
        api.MapPost("/ingredients/{id:guid}/ingredient-versions", CreateIngredientVersion)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("CreateIngredientVersion")
            .WithOpenApi();

        // ========== RECIPES ==========

        /// <summary>
        /// POST /api/kitchen/recipes
        /// Create a new recipe with lines and serving basis
        /// </summary>
        api.MapPost("/recipes", CreateRecipe)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("CreateRecipe")
            .WithOpenApi();

        /// <summary>
        /// GET /api/kitchen/recipes?search=&page=1&pageSize=25
        /// List recipes with current version
        /// </summary>
        api.MapGet("/recipes", ListRecipes)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("ListRecipes")
            .WithOpenApi();

        /// <summary>
        /// POST /api/kitchen/recipes/{id}/versions
        /// Create a new recipe version (new set of ingredient lines)
        /// </summary>
        api.MapPost("/recipes/{id:guid}/versions", CreateRecipeVersion)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("CreateRecipeVersion")
            .WithOpenApi();

        /// <summary>
        /// GET /api/kitchen/recipes/{id}/versions/{version}/nutrition
        /// Calculate nutrition for a specific recipe version at validAt/knownAt times
        /// </summary>
        api.MapGet("/recipes/{id:guid}/versions/{version:int}/nutrition", GetRecipeNutrition)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.KitchenStaff, RoleNames.Admin))
            .WithName("GetRecipeNutrition")
            .WithOpenApi();

        return app;
    }

    // ========== HANDLERS ==========

    private static async Task<IResult> CreateIngredient(
        CreateIngredientRequest req,
        MealTraceDbContext db,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.EditablePercent < 0 || req.EditablePercent > 100)
            return Results.BadRequest(new { code = "INVALID_INGREDIENT", message = "Name required, EditablePercent 0-100" });

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Unit = req.Unit.Trim(),
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();

        return Results.Created($"/api/kitchen/ingredients/{ingredient.Id}", new IngredientResponse(
            ingredient.Id,
            ingredient.Name,
            ingredient.Unit,
            req.EditablePercent,
            null));
    }

    private static async Task<IResult> ListIngredients(
        string? search,
        int page = 1,
        int pageSize = 25,
        MealTraceDbContext db = null!)
    {
        if (pageSize < 1 || pageSize > 100) pageSize = 25;
        if (page < 1) page = 1;

        var query = db.Ingredients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Unit,
                CurrentVersion = x.Versions.Where(v => v.EffectiveFrom <= DateTimeOffset.UtcNow)
                    .OrderByDescending(v => v.RecordedAt)
                    .Select(v => new
                    {
                        v.Version,
                        v.EnergyKcalPer100G,
                        v.ProteinGPer100G,
                        v.EffectiveFrom
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Results.Ok(new { total, page, pageSize, items });
    }

    private static async Task<IResult> CreateIngredientVersion(
        Guid id,
        CreateIngredientVersionRequest req,
        MealTraceDbContext db,
        TimeProvider timeProvider)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(x => x.Id == id);
        if (ingredient is null) return Results.NotFound();

        // Validation
        if (req.EnergyKcalPer100G < 0 || req.ProteinGPer100G < 0)
            return Results.BadRequest(new { code = "INVALID_NUTRIENT", message = "Nutrient values must be non-negative" });

        if (string.IsNullOrWhiteSpace(req.Source))
            return Results.BadRequest(new { code = "SOURCE_REQUIRED", message = "Source is required" });

        if (req.ValidTo.HasValue && req.ValidTo <= req.ValidFrom)
            return Results.BadRequest(new { code = "INVALID_DATE_RANGE", message = "ValidTo must be after ValidFrom" });

        var nextVersion = await db.IngredientVersions
            .Where(x => x.IngredientId == id)
            .MaxAsync(x => (int?)x.Version) ?? 0;
        nextVersion++;

        var version = new IngredientVersion
        {
            Id = Guid.NewGuid(),
            IngredientId = id,
            Version = nextVersion,
            EnergyKcalPer100G = req.EnergyKcalPer100G,
            ProteinGPer100G = req.ProteinGPer100G,
            PricePerKg = 0m, // TODO: implement price versioning if available
            EdibleFraction = 1m,
            EffectiveFrom = req.ValidFrom,
            RecordedAt = timeProvider.GetUtcNow()
        };

        db.IngredientVersions.Add(version);
        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/kitchen/ingredients/{id}/ingredient-versions/{version.Id}",
            new IngredientVersionResponse(
                version.Id,
                version.IngredientId,
                version.Version,
                version.EnergyKcalPer100G,
                version.ProteinGPer100G,
                0m, // FatG
                0m, // CarbG
                version.EffectiveFrom,
                null, // ValidTo
                req.Source,
                req.SourceRef,
                req.SupersedesId,
                version.RecordedAt,
                "System"));
    }

    private static async Task<IResult> CreateRecipe(
        CreateRecipeRequest req,
        MealTraceDbContext db,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.ServingBasis <= 0 || !req.Lines.Any())
            return Results.BadRequest(new { code = "INVALID_RECIPE", message = "Name, ServingBasis > 0, and Lines required" });

        // Check all ingredients exist
        var ingredientIds = req.Lines.Select(x => x.IngredientId).ToArray();
        var existingIngredients = await db.Ingredients.Where(x => ingredientIds.Contains(x.Id)).CountAsync();
        if (existingIngredients != ingredientIds.Length)
            return Results.BadRequest(new { code = "INGREDIENT_NOT_FOUND", message = "One or more ingredients do not exist" });

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim()
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        // Create v1
        var version = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Version = 1,
            RecordedAt = timeProvider.GetUtcNow()
        };

        db.RecipeVersions.Add(version);

        foreach (var line in req.Lines)
        {
            var ingredient = new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeVersionId = version.Id,
                IngredientVersionId = line.IngredientId, // TODO: resolve actual version
                GramsPerPortion = line.Quantity
            };
            db.RecipeIngredients.Add(ingredient);
        }

        await db.SaveChangesAsync();

        return Results.Created($"/api/kitchen/recipes/{recipe.Id}", new RecipeResponse(
            recipe.Id,
            recipe.Name,
            req.ServingBasis,
            1));
    }

    private static async Task<IResult> ListRecipes(
        string? search,
        int page = 1,
        int pageSize = 25,
        MealTraceDbContext db = null!)
    {
        if (pageSize < 1 || pageSize > 100) pageSize = 25;
        if (page < 1) page = 1;

        var query = db.Recipes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                CurrentVersion = x.Versions.Max(v => v.Version)
            })
            .ToListAsync();

        return Results.Ok(new { total, page, pageSize, items });
    }

    private static async Task<IResult> CreateRecipeVersion(
        Guid id,
        CreateRecipeVersionRequest req,
        MealTraceDbContext db,
        TimeProvider timeProvider)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(x => x.Id == id);
        if (recipe is null) return Results.NotFound();

        var currentMaxVersion = await db.RecipeVersions
            .Where(x => x.RecipeId == id)
            .MaxAsync(x => x.Version);

        if (req.ExpectedVersion != currentMaxVersion)
            return Results.Conflict(new { code = "VERSION_MISMATCH", message = $"Expected version {currentMaxVersion}" });

        var nextVersion = currentMaxVersion + 1;
        var version = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = id,
            Version = nextVersion,
            RecordedAt = timeProvider.GetUtcNow()
        };

        db.RecipeVersions.Add(version);

        foreach (var line in req.Lines)
        {
            var ingredient = new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeVersionId = version.Id,
                IngredientVersionId = line.IngredientId,
                GramsPerPortion = line.Quantity
            };
            db.RecipeIngredients.Add(ingredient);
        }

        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/kitchen/recipes/{id}/versions/{nextVersion}",
            new RecipeVersionResponse(
                version.Id,
                recipe.Id,
                nextVersion,
                0, // ServingBasis - TODO: store in recipe
                "ROUND_HALF_UP",
                version.RecordedAt,
                req.Lines.Select(l => new RecipeLineWithNutrientDto(
                    Guid.NewGuid(),
                    l.IngredientId,
                    "IngredientName", // TODO: resolve name
                    l.Quantity,
                    l.Unit,
                    Guid.NewGuid(),
                    0, 0, 0, 0)).ToList()));
    }

    private static async Task<IResult> GetRecipeNutrition(
        Guid id,
        int version,
        DateTimeOffset? validAt,
        DateTimeOffset? knownAt,
        string? ageGroup,
        MealTraceDbContext db)
    {
        validAt ??= DateTimeOffset.UtcNow;
        knownAt ??= DateTimeOffset.UtcNow;

        var recipeVersion = await db.RecipeVersions
            .Where(x => x.RecipeId == id && x.Version == version)
            .Include(x => x.Recipe)
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.IngredientVersion)
            .FirstOrDefaultAsync();

        if (recipeVersion is null)
            return Results.NotFound();

        // TODO: Implement bitemporal nutrition calculation
        // 1. For each ingredient line, find nutrient version at validAt with max recordedAt <= knownAt
        // 2. Check for missing data => 422
        // 3. Calculate per-ingredient contribution
        // 4. Sum and divide by serving basis
        // 5. Apply rounding rule

        return Results.Ok(new RecipeNutritionResponse(
            id,
            version,
            0,
            "ROUND_HALF_UP",
            new NutritionPerServingDto(0, 0, 0, 0),
            [],
            new NutritionInputsDto(validAt.Value, knownAt.Value, ageGroup)));
    }
}

using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public sealed record CreateIngredientRequest(string Name, string Unit);
public sealed record CreateIngredientVersionRequest(
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G,
    decimal PricePerKg,
    decimal EdibleFraction,
    DateTimeOffset EffectiveFrom);

public sealed record RecipeLineInput(
    Guid IngredientId,
    Guid? IngredientVersionId,
    decimal GramsPerPortion);

public sealed record CreateRecipeRequest(string Name, List<RecipeLineInput> Ingredients);
public sealed record CreateRecipeVersionRequest(int ExpectedVersion, List<RecipeLineInput> Ingredients);

public sealed record IngredientSummaryDto(
    Guid Id,
    string Name,
    string Unit,
    int Version,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G,
    DateTimeOffset EffectiveFrom);

public sealed record RecipeSummaryDto(
    Guid Id,
    string Name,
    int CurrentVersion);

public sealed record RecipeVersionDetailDto(
    Guid Id,
    Guid RecipeId,
    int Version,
    DateTimeOffset RecordedAt,
    List<RecipeIngredientDetailDto> Ingredients);

public sealed record RecipeIngredientDetailDto(
    Guid IngredientId,
    Guid IngredientVersionId,
    string IngredientName,
    string IngredientUnit,
    decimal GramsPerPortion,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G);

public sealed record RecipeNutritionLineDto(
    Guid IngredientId,
    string IngredientName,
    decimal GramsPerPortion,
    decimal EnergyKcalContribution,
    decimal ProteinGContribution);

public sealed record RecipeNutritionDto(
    Guid RecipeId,
    int Version,
    decimal EnergyKcalPerPortion,
    decimal ProteinGPerPortion,
    List<RecipeNutritionLineDto> Lines);

public static class KitchenService
{
    public static async Task<List<IngredientSummaryDto>> ListIngredientsAsync(MealTraceDbContext db)
    {
        var now = DateTimeOffset.UtcNow;

        var rows = await db.Ingredients
            .AsNoTracking()
            .Include(x => x.Versions)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return rows.Select(x =>
        {
            var latest = x.Versions
                .Where(v => v.EffectiveFrom <= now)
                .OrderByDescending(v => v.EffectiveFrom)
                .ThenByDescending(v => v.Version)
                .FirstOrDefault();

            return new IngredientSummaryDto(
                x.Id,
                x.Name,
                x.Unit,
                latest?.Version ?? 0,
                latest?.EnergyKcalPer100G ?? 0m,
                latest?.ProteinGPer100G ?? 0m,
                latest?.EffectiveFrom ?? DateTimeOffset.MinValue);
        }).ToList();
    }

    public static async Task<IngredientSummaryDto?> GetIngredientAsync(MealTraceDbContext db, Guid id)
    {
        var ingredient = await db.Ingredients
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ingredient is null) return null;

        var now = DateTimeOffset.UtcNow;
        var latest = ingredient.Versions
            .Where(v => v.EffectiveFrom <= now)
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Version)
            .FirstOrDefault();

        return new IngredientSummaryDto(
            ingredient.Id,
            ingredient.Name,
            ingredient.Unit,
            latest?.Version ?? 0,
            latest?.EnergyKcalPer100G ?? 0m,
            latest?.ProteinGPer100G ?? 0m,
            latest?.EffectiveFrom ?? DateTimeOffset.MinValue);
    }

    public static async Task<IngredientVersion> CreateIngredientVersionAsync(
        MealTraceDbContext db,
        Guid ingredientId,
        CreateIngredientVersionRequest request)
    {
        var ingredient = await db.Ingredients
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == ingredientId);

        if (ingredient is null)
            throw new InvalidOperationException("Ingredient not found.");

        if (request.EnergyKcalPer100G < 0m || request.ProteinGPer100G < 0m)
            throw new InvalidOperationException("Nutrient values must be non-negative.");

        if (request.PricePerKg < 0m)
            throw new InvalidOperationException("Price must be non-negative.");

        if (request.EdibleFraction <= 0m || request.EdibleFraction > 1m)
            throw new InvalidOperationException("EdibleFraction must be in (0, 1].");

        var nextVersion = ingredient.Versions
            .Select(v => v.Version)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var version = new IngredientVersion
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredientId,
            Ingredient = ingredient,
            Version = nextVersion,
            EnergyKcalPer100G = request.EnergyKcalPer100G,
            ProteinGPer100G = request.ProteinGPer100G,
            PricePerKg = request.PricePerKg,
            EdibleFraction = request.EdibleFraction,
            EffectiveFrom = request.EffectiveFrom,
            RecordedAt = DateTimeOffset.UtcNow
        };

        db.IngredientVersions.Add(version);
        await db.SaveChangesAsync();

        return version;
    }

    public static async Task<List<RecipeSummaryDto>> ListRecipesAsync(MealTraceDbContext db)
    {
        var rows = await db.Recipes
            .AsNoTracking()
            .Include(x => x.Versions)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return rows.Select(x => new RecipeSummaryDto(
            x.Id,
            x.Name,
            x.Versions.Select(v => v.Version).DefaultIfEmpty(0).Max()
        )).ToList();
    }

    public static async Task<Recipe> CreateRecipeAsync(MealTraceDbContext db, string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Recipe name is required.");

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = trimmed
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        return recipe;
    }

    public static async Task<RecipeVersion> CreateRecipeVersionAsync(
        MealTraceDbContext db,
        Guid recipeId,
        CreateRecipeVersionRequest request)
    {
        var recipe = await db.Recipes
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == recipeId);

        if (recipe is null)
            throw new InvalidOperationException("Recipe not found.");

        var currentVersion = recipe.Versions
            .Select(v => v.Version)
            .DefaultIfEmpty(0)
            .Max();

        if (request.ExpectedVersion != currentVersion)
            throw new InvalidOperationException($"Version mismatch. Expected {currentVersion}.");

        if (request.Ingredients is null || request.Ingredients.Count == 0)
            throw new InvalidOperationException("At least one ingredient line is required.");

        var newVersion = currentVersion + 1;

        var recipeVersion = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            Version = newVersion,
            RecordedAt = DateTimeOffset.UtcNow
        };

        db.RecipeVersions.Add(recipeVersion);

        foreach (var item in request.Ingredients)
        {
            if (item.GramsPerPortion <= 0m)
                throw new InvalidOperationException("Grams per portion must be > 0.");

            var ingredientVersionId = item.IngredientVersionId ?? await ResolveCurrentIngredientVersionIdAsync(db, item.IngredientId);

            var ingredientVersion = await db.IngredientVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ingredientVersionId);

            if (ingredientVersion is null)
                throw new InvalidOperationException($"Ingredient version '{ingredientVersionId}' not found.");

            var line = new RecipeIngredient
            {
                Id = Guid.NewGuid(),
                RecipeVersionId = recipeVersion.Id,
                IngredientVersionId = ingredientVersionId,
                GramsPerPortion = item.GramsPerPortion
            };

            db.RecipeIngredients.Add(line);
        }

        await db.SaveChangesAsync();
        return recipeVersion;
    }

    public static async Task<RecipeVersionDetailDto> GetRecipeVersionDetailAsync(MealTraceDbContext db, Guid recipeId, int version)
    {
        var recipeVersion = await db.RecipeVersions
            .AsNoTracking()
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.IngredientVersion)
            .ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.RecipeId == recipeId && x.Version == version);

        if (recipeVersion is null)
            throw new InvalidOperationException("Recipe version not found.");

        var list = new List<RecipeIngredientDetailDto>();

        foreach (var ri in recipeVersion.Ingredients)
        {
            var iv = ri.IngredientVersion;
            var ingredient = iv.Ingredient;

            list.Add(new RecipeIngredientDetailDto(
                ingredient.Id,
                iv.Id,
                ingredient.Name,
                ingredient.Unit,
                ri.GramsPerPortion,
                iv.EnergyKcalPer100G,
                iv.ProteinGPer100G));
        }

        return new RecipeVersionDetailDto(
            recipeVersion.Id,
            recipeVersion.RecipeId,
            recipeVersion.Version,
            recipeVersion.RecordedAt,
            list);
    }

    public static async Task<RecipeNutritionDto> GetRecipeNutritionAsync(
        MealTraceDbContext db,
        Guid recipeId,
        int version,
        DateTimeOffset asOf)
    {
        var recipeVersion = await db.RecipeVersions
            .AsNoTracking()
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.IngredientVersion)
            .ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.RecipeId == recipeId && x.Version == version);

        if (recipeVersion is null)
            throw new InvalidOperationException("Recipe version not found.");

        decimal totalEnergy = 0m;
        decimal totalProtein = 0m;
        var lines = new List<RecipeNutritionLineDto>();

        foreach (var item in recipeVersion.Ingredients)
        {
            var iv = item.IngredientVersion;
            var ingredient = iv.Ingredient;

            if (iv.EffectiveFrom > asOf)
                throw new InvalidOperationException($"Ingredient '{ingredient.Name}' version is not valid as of {asOf:O}.");

            var grams = item.GramsPerPortion;

            var energyContribution = (iv.EnergyKcalPer100G / 100m) * grams;
            var proteinContribution = (iv.ProteinGPer100G / 100m) * grams;

            totalEnergy += energyContribution;
            totalProtein += proteinContribution;

            lines.Add(new RecipeNutritionLineDto(
                ingredient.Id,
                ingredient.Name,
                grams,
                energyContribution,
                proteinContribution));
        }

        return new RecipeNutritionDto(
            recipeId,
            version,
            totalEnergy,
            totalProtein,
            lines);
    }

    private static async Task<Guid> ResolveCurrentIngredientVersionIdAsync(MealTraceDbContext db, Guid ingredientId)
    {
        var version = await db.IngredientVersions
            .AsNoTracking()
            .Where(x => x.IngredientId == ingredientId)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        if (version is null)
            throw new InvalidOperationException("Ingredient has no nutrient version yet.");

        return version.Id;
    }
}

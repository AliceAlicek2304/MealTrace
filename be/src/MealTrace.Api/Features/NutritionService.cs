using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Kitchen.Services;

// ---- Contracts ------------------------------------------------------------

public sealed record NutritionTotals(decimal Kcal, decimal ProteinG);

public sealed record NutritionRoundingRule(int KcalDecimals, int ProteinDecimals, string Mode);

public sealed record NutritionLine(
    Guid IngredientId,
    string IngredientName,
    Guid PinnedVersionId,     // ingredient version recorded on the recipe line
    Guid NutrientVersionId,   // ingredient version actually used at (validAt, knownAt)
    decimal GramsPerPortion,
    NutritionTotals Contribution);

public sealed record NutritionLineInput(
    Guid IngredientId,
    decimal GramsPerPortion,
    decimal EdibleFraction,
    decimal EnergyKcalPer100G,
    decimal ProteinGPer100G);

public sealed record NutritionInputs(
    DateTimeOffset ValidAt,
    DateTimeOffset KnownAt,
    int RecipeVersion,
    DateTimeOffset RecipeVersionRecordedAt,
    IReadOnlyList<NutritionLineInput> Lines);

public sealed record NutritionResponse(
    NutritionTotals PerServing,
    IReadOnlyList<NutritionLine> Lines,
    NutritionRoundingRule RoundingRule,
    NutritionInputs Inputs);

public interface INutritionService
{
    Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct);
}

// ---- Implementation -------------------------------------------------------

public sealed class NutritionService(MealTraceDbContext db, IIngredientService ingredients, TimeProvider time)
    : INutritionService
{
    // No rounding column exists on RecipeVersions, so one fixed rule applies and is echoed in the response.
    private static readonly NutritionRoundingRule Rounding = new(KcalDecimals: 0, ProteinDecimals: 1, Mode: "AwayFromZero");

    public async Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var valid = (validAt ?? now).ToUniversalTime();
        var known = (knownAt ?? now).ToUniversalTime();

        var recipeVersion = await db.RecipeVersions.AsNoTracking()
            .SingleOrDefaultAsync(v => v.RecipeId == recipeId && v.Version == version, ct);

        if (recipeVersion is null)
        {
            var recipeExists = await db.Recipes.AnyAsync(r => r.Id == recipeId, ct);
            throw recipeExists
                ? KitchenException.NotFound("RECIPE_VERSION_NOT_FOUND", $"Recipe version {version} not found.")
                : KitchenException.NotFound("RECIPE_NOT_FOUND", "Recipe not found.");
        }

        var lines = await (
            from ri in db.RecipeIngredients.AsNoTracking()
            join pinned in db.IngredientVersions.AsNoTracking() on ri.IngredientVersionId equals pinned.Id
            join ingredient in db.Ingredients.AsNoTracking() on pinned.IngredientId equals ingredient.Id
            where ri.RecipeVersionId == recipeVersion.Id
            orderby ingredient.Name, ingredient.Id
            select new
            {
                IngredientId = ingredient.Id,
                IngredientName = ingredient.Name,
                PinnedVersionId = pinned.Id,
                ri.GramsPerPortion
            }).ToListAsync(ct);

        // The pinned version only identifies the ingredient; the numbers come from the version in force at (validAt, knownAt).
        var effective = await ingredients.ResolveEffectiveAsync(
            lines.Select(l => l.IngredientId).ToArray(), valid, known, ct);

        var missing = lines
            .Where(l => !effective.ContainsKey(l.IngredientId))
            .Select(l => (object)new { l.IngredientId, Name = l.IngredientName })
            .ToList();

        if (missing.Count > 0)
            throw KitchenException.NutrientDataMissing(missing, valid, known); // never silently count as 0

        decimal kcalTotal = 0m, proteinTotal = 0m;
        var lineResults = new List<NutritionLine>(lines.Count);
        var lineInputs = new List<NutritionLineInput>(lines.Count);

        foreach (var line in lines)
        {
            var v = effective[line.IngredientId];

            // quantity x (ediblePercent / 100) x valuePer100g / 100 ; quantity is already per portion.
            var factor = line.GramsPerPortion * v.EdibleFraction / 100m;
            var kcal = factor * v.EnergyKcalPer100G;
            var protein = factor * v.ProteinGPer100G;

            kcalTotal += kcal;
            proteinTotal += protein;

            lineResults.Add(new NutritionLine(
                line.IngredientId, line.IngredientName, line.PinnedVersionId, v.Id,
                line.GramsPerPortion, new NutritionTotals(kcal, protein)));

            lineInputs.Add(new NutritionLineInput(
                line.IngredientId, line.GramsPerPortion, v.EdibleFraction,
                v.EnergyKcalPer100G, v.ProteinGPer100G));
        }

        // Round once, at the end, on the unrounded sum.
        var perServing = new NutritionTotals(
            Math.Round(kcalTotal, Rounding.KcalDecimals, MidpointRounding.AwayFromZero),
            Math.Round(proteinTotal, Rounding.ProteinDecimals, MidpointRounding.AwayFromZero));

        var inputs = new NutritionInputs(
            valid.ToVn(), known.ToVn(), recipeVersion.Version, recipeVersion.RecordedAt.ToVn(), lineInputs);

        return new NutritionResponse(perServing, lineResults, Rounding, inputs);
    }
}

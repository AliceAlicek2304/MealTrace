using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Abstractions.Kitchen;

namespace MealTrace.Application.Features.Kitchen;

public sealed class NutritionService(INutritionRepository repository, IIngredientService ingredients, TimeProvider time) : INutritionService
{
    // No rounding column exists on RecipeVersions, so one fixed rule applies and is echoed in the response.
    private static readonly NutritionRoundingRule Rounding = new(KcalDecimals: 0, ProteinDecimals: 1, Mode: "AwayFromZero");

    public async Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var valid = (validAt ?? now).ToUniversalTime();
        var known = (knownAt ?? now).ToUniversalTime();

        var recipeVersion = await repository.FindRecipeVersionAsync(recipeId, version, ct);

        if (recipeVersion is null)
        {
            var recipeExists = await repository.RecipeExistsAsync(recipeId, ct);
            throw recipeExists
                ? KitchenException.NotFound("RECIPE_VERSION_NOT_FOUND", $"Recipe version {version} not found.")
                : KitchenException.NotFound("RECIPE_NOT_FOUND", "Recipe not found.");
        }

        var lines = await repository.ListRecipeNutritionLinesAsync(recipeVersion, ct);

        // The pinned version only identifies the ingredient; the numbers come from the version in force at (validAt, knownAt).
        var effective = await ingredients.ResolveEffectiveAsync(
            lines.Select(l => l.IngredientId).ToArray(), valid, known, ct);

        var missing = lines
            .Where(l => !effective.ContainsKey(l.IngredientId))
            .Select(l => new MissingIngredient(l.IngredientId, l.IngredientName))
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

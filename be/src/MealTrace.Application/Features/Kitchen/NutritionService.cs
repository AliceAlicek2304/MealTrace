using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

public sealed class NutritionService(
    INutritionRepository repository, IIngredientService ingredients, TimeProvider time) : INutritionService
{
    private const string ModePinned = "pinned";
    private const string ModeRecompute = "recompute";

    private sealed record Computation(
        List<NutritionLine> Lines, List<NutritionLineInput> Inputs, NutritionTotals PerServing,
        List<IncompleteNutrientData> Incomplete, List<MissingIngredient> MissingConversion,
        List<MissingIngredient> Unverified);

    public Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct) =>
        CalculateAsync(recipeId, version, new NutritionQuery(null, validAt, knownAt, null), ct);

    public async Task<NutritionResponse> CalculateAsync(
        Guid recipeId, int version, NutritionQuery query, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var mode = string.IsNullOrWhiteSpace(query.Mode) ? ModeRecompute : query.Mode.Trim().ToLowerInvariant();
        if (mode is not (ModePinned or ModeRecompute))
            errors.Add("mode", "mode must be 'pinned' or 'recompute'.");
        else if (mode == ModePinned && (query.ValidAt is not null || query.KnownAt is not null))
            errors.Add("mode", "validAt/knownAt only apply to mode=recompute; mode=pinned uses the stored inputs.");
        errors.ThrowIfAny();

        var pinnedMode = mode == ModePinned;
        var now = time.GetUtcNow();
        var valid = (pinnedMode ? now : query.ValidAt ?? now).ToUniversalTime();
        var known = (pinnedMode ? now : query.KnownAt ?? now).ToUniversalTime();

        var recipeVersion = await repository.FindRecipeVersionAsync(recipeId, version, ct);
        if (recipeVersion is null)
        {
            var recipeExists = await repository.RecipeExistsAsync(recipeId, ct);
            throw recipeExists
                ? KitchenException.NotFound("RECIPE_VERSION_NOT_FOUND", $"Recipe version {version} not found.")
                : KitchenException.NotFound("RECIPE_NOT_FOUND", "Recipe not found.");
        }

        // BE1-BR03: a recipe version does not exist for a knownAt earlier than the moment it was recorded.
        if (recipeVersion.RecordedAt > known)
            throw KitchenException.NotFound("RECIPE_VERSION_NOT_FOUND",
                $"Recipe version {version} was not yet recorded at the requested knownAt.");

        var rows = await repository.ListRecipeNutritionLinesAsync(recipeVersion, ct);

        // Versions recorded before v1.3 have no stored basis: their lines are already per portion.
        var servingBasis = recipeVersion.ServingBasis ?? 1;
        var rounding = RecipeService.RoundingOf(recipeVersion);

        Dictionary<Guid, IngredientVersion>? effective = null;
        if (!pinnedMode)
        {
            effective = await ingredients.ResolveEffectiveAsync(
                rows.Select(r => r.IngredientId).ToArray(), valid, known, ct);

            var missing = rows
                .Where(r => !effective.ContainsKey(r.IngredientId))
                .Select(r => new MissingIngredient(r.IngredientId, r.IngredientName))
                .ToList();

            if (missing.Count > 0)
                throw KitchenException.NutrientDataMissing(missing, valid, known); // never silently count as 0
        }

        var used = Compute(rows, r => pinnedMode ? r.Pinned : effective![r.IngredientId], servingBasis, rounding);
        if (used.MissingConversion.Count > 0)
            throw KitchenException.UnitConversionMissing(used.MissingConversion);

        NutritionTotals? pinnedTotals = null;
        NutritionTotals? delta = null;
        if (!pinnedMode)
        {
            var pinnedRun = Compute(rows, r => r.Pinned, servingBasis, rounding);
            if (pinnedRun.MissingConversion.Count == 0)
            {
                pinnedTotals = pinnedRun.PerServing;
                delta = Delta(used.PerServing, pinnedRun.PerServing);
            }
        }

        AgeGroupComparison? comparison = null;
        if (!string.IsNullOrWhiteSpace(query.AgeGroup))
            comparison = await CompareAsync(query.AgeGroup.Trim(), used.PerServing, query.MealType, valid, known, ct);

        var inputs = new NutritionInputs(
            pinnedMode ? null : valid.ToVn(),
            pinnedMode ? null : known.ToVn(),
            recipeVersion.Version,
            recipeVersion.RecordedAt.ToVn(),
            used.Inputs,
            servingBasis);

        return new NutritionResponse(
            used.PerServing, used.Lines, rounding, inputs,
            mode, pinnedTotals, delta, used.Incomplete, comparison, used.Unverified);
    }

    /// <summary>
    /// Per line: grams = quantity (g) or quantity x gramsPerMl (ml), divided by servingBasis; then, only for gross
    /// weights, x edibleFraction; then x value-per-100-g-edible / 100. Rounding happens once, on the totals.
    /// </summary>
    private static Computation Compute(
        IReadOnlyList<RecipeNutritionLineRow> rows, Func<RecipeNutritionLineRow, IngredientVersion> versionFor,
        int servingBasis, NutritionRoundingRule rounding)
    {
        var lines = new List<NutritionLine>(rows.Count);
        var inputs = new List<NutritionLineInput>(rows.Count);
        var incomplete = new List<IncompleteNutrientData>();
        var missingConversion = new List<MissingIngredient>();
        var unverified = new List<MissingIngredient>();

        decimal kcal = 0m, protein = 0m, fat = 0m, carb = 0m;
        var fatKnown = true;
        var carbKnown = true;

        foreach (var row in rows)
        {
            var v = versionFor(row);

            decimal grams;
            decimal? density = null;
            if (row.Quantity is { } quantity && row.Unit is { } unit)
            {
                if (unit == "ml")
                {
                    if (v.GramsPerMl is not { } d)
                    {
                        missingConversion.Add(new MissingIngredient(row.IngredientId, row.IngredientName));
                        continue;
                    }

                    density = d;
                    grams = quantity * d / servingBasis;
                }
                else
                {
                    grams = quantity / servingBasis;
                }
            }
            else
            {
                grams = row.GramsPerPortion; // line recorded before v1.3: grams per portion as stored
                // A legacy line of an ml ingredient was priced as grams with no density: flag it, do not trust it.
                if (row.IngredientUnit == "ml")
                    unverified.Add(new MissingIngredient(row.IngredientId, row.IngredientName));
            }

            var weightBasis = row.WeightBasis ?? "gross";
            decimal? fractionApplied = weightBasis == "edible" ? null : v.EdibleFraction;
            var edibleGrams = fractionApplied is { } fraction ? grams * fraction : grams;

            var lineKcal = edibleGrams * v.EnergyKcalPer100G / 100m;
            var lineProtein = edibleGrams * v.ProteinGPer100G / 100m;
            decimal? lineFat = v.FatGPer100G is { } fatPer100 ? edibleGrams * fatPer100 / 100m : null;
            decimal? lineCarb = v.CarbGPer100G is { } carbPer100 ? edibleGrams * carbPer100 / 100m : null;

            kcal += lineKcal;
            protein += lineProtein;
            if (lineFat is { } lf) fat += lf; else fatKnown = false;
            if (lineCarb is { } lc) carb += lc; else carbKnown = false;

            var missingFields = new List<string>();
            if (lineFat is null) missingFields.Add("fatG");
            if (lineCarb is null) missingFields.Add("carbG");
            if (missingFields.Count > 0)
                incomplete.Add(new IncompleteNutrientData(row.IngredientId, row.IngredientName, v.Id, missingFields));

            lines.Add(new NutritionLine(
                row.IngredientId,
                row.IngredientName,
                row.PinnedVersionId,
                v.Id,
                grams,
                new NutritionTotals(lineKcal, lineProtein, lineFat, lineCarb),
                v.Id != row.PinnedVersionId,
                new NutritionLineQuantity(row.Quantity, row.Unit, weightBasis, servingBasis),
                new NutritionLineConversion(density, fractionApplied, edibleGrams),
                new NutritionTotals(v.EnergyKcalPer100G, v.ProteinGPer100G, v.FatGPer100G, v.CarbGPer100G),
                v.Source,
                v.SourceRef));

            inputs.Add(new NutritionLineInput(
                row.IngredientId, grams, v.EdibleFraction, v.EnergyKcalPer100G, v.ProteinGPer100G,
                v.FatGPer100G, v.CarbGPer100G, weightBasis));
        }

        var perServing = new NutritionTotals(
            Round(kcal, rounding.KcalDecimals),
            Round(protein, rounding.ProteinDecimals),
            fatKnown ? Round(fat, rounding.ProteinDecimals) : null,
            carbKnown ? Round(carb, rounding.ProteinDecimals) : null);

        return new Computation(lines, inputs, perServing, incomplete, missingConversion, unverified);
    }

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static NutritionTotals Delta(NutritionTotals a, NutritionTotals b) =>
        new(a.Kcal - b.Kcal, a.ProteinG - b.ProteinG, a.FatG - b.FatG, a.CarbG - b.CarbG);

    /// <inheritdoc />
    public async Task<AgeGroupComparison> CompareAsync(
        string ageGroup, NutritionTotals perMeal, string? mealType, DateTimeOffset validAt, DateTimeOffset knownAt,
        CancellationToken ct)
    {
        var valid = validAt.ToUniversalTime();
        var known = knownAt.ToUniversalTime();
        var group = ageGroup.Trim();

        var rows = await repository.ListReferenceRangesAsync(group, known, ct);
        var effective = ReferenceTimeline.Effective(rows, valid, known);

        // Only documented per-meal ranges (for this meal type, or for any meal) may be compared with a per-meal value.
        var usable = effective
            .Where(r => r.Basis == ReferenceBasis.PerMeal
                        && (r.MealType is null || string.Equals(r.MealType, mealType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var picked = usable
            .GroupBy(r => r.Nutrient)
            .Select(g => g.OrderByDescending(r => r.MealType is not null)
                          .ThenByDescending(r => r.EffectiveFrom)
                          .ThenByDescending(r => r.RecordedAt)
                          .First())
            .OrderBy(r => r.Nutrient, StringComparer.Ordinal)
            .ToList();

        var context = new Dictionary<string, object?>
        {
            ["ageGroup"] = group,
            ["validAt"] = valid.ToVn(),
            ["knownAt"] = known.ToVn()
        };

        if (picked.Count == 0)
        {
            if (effective.Count > 0)
                throw new KitchenException(422, "REFERENCE_BASIS_MISMATCH",
                    $"The ranges recorded for age group '{group}' are daily or of unspecified basis, or for another meal type; " +
                    "they are not compared with a per-meal value and no conversion rule exists.", context);

            throw new KitchenException(422, "AGE_GROUP_REFERENCE_MISSING",
                $"No documented reference range is recorded for age group '{group}' at the requested time.", context);
        }

        var items = picked.Select(r =>
        {
            decimal? value = r.Nutrient switch
            {
                "kcal" => perMeal.Kcal,
                "proteinG" => perMeal.ProteinG,
                "fatG" => perMeal.FatG,
                "carbG" => perMeal.CarbG,
                _ => null
            };

            return new AgeGroupComparisonItem(
                r.Nutrient, value, r.MinValue, r.MaxValue, StatusOf(value, r.MinValue, r.MaxValue),
                r.Id, r.Source, r.SourceRef, r.Version, r.Basis, r.MealType);
        }).ToList();

        return new AgeGroupComparison(picked[0].AgeGroup, items);
    }

    private static string StatusOf(decimal? value, decimal? min, decimal? max)
    {
        if (value is not { } v) return "unknown";
        if (min is { } lo && v < lo) return "below";
        if (max is { } hi && v > hi) return "above";
        return "within";
    }
}

using MealTrace.Application.Exceptions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;

namespace MealTrace.Application.Features.Kitchen;

public sealed class RecipeService(
    IRecipeRepository repository, IIngredientService ingredients, TimeProvider time,
    IUnitOfWork unitOfWork, ICurrentActor actor) : IRecipeService
{
    private const int MaxNameLength = 200;
    private const int MaxReasonLength = 500;
    private const decimal MaxQuantity = 1_000_000m;
    private const string RoundingMode = "AwayFromZero";

    /// <summary>
    /// Default for a new recipe without an explicit rule, and for versions recorded before v1.3.
    /// Same behaviour as the previous fixed rule (kcal 0 decimals, protein 1); each version stores the rule applied.
    /// The business rule itself is still to be confirmed (G0).
    /// </summary>
    public static readonly NutritionRoundingRule DefaultRounding = new(KcalDecimals: 0, ProteinDecimals: 1, Mode: RoundingMode);

    public static NutritionRoundingRule RoundingOf(RecipeVersion version) =>
        version.RoundingKcalDecimals is { } kcal && version.RoundingMacroDecimals is { } macro
            ? new NutritionRoundingRule(kcal, macro, RoundingMode)
            : DefaultRounding;

    private sealed record StagedLine(
        Guid IngredientVersionId, decimal Quantity, string Unit, string WeightBasis, decimal GramsPerPortion);

    public async Task<RecipeVersionCreatedResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = request.Name?.Trim();

        if (string.IsNullOrEmpty(name))
            errors.Add("name", "Name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add("name", $"Name must be at most {MaxNameLength} characters.");

        ValidateShape(request.ServingBasis, request.Lines, errors);
        var rounding = ResolveRounding(request.RoundingRule, DefaultRounding, errors);
        errors.ThrowIfAny();

        var now = time.GetUtcNow();
        var lines = await BuildLinesAsync(request.ServingBasis!.Value, request.Lines!, now, ct);

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = name!
        };
        repository.AddRecipe(recipe);
        var version = StageVersion(recipe.Id, 1, request.ServingBasis.Value, rounding, null, now, lines);

        await unitOfWork.SaveChangesAsync(ct); // single SaveChanges => one transaction

        return new RecipeVersionCreatedResponse(
            recipe.Id, 1, version.Id, null, null, version.RecordedAt.ToVn(), version.RecordedByUserId, null, rounding);
    }

    public async Task<RecipeVersionCreatedResponse> AddVersionAsync(
        Guid recipeId, CreateRecipeVersionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var reason = request.Reason?.Trim();

        if (request.ExpectedVersion is null or < 1)
            errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        if (reason is { Length: > MaxReasonLength })
            errors.Add("reason", $"reason must be at most {MaxReasonLength} characters.");
        ValidateShape(request.ServingBasis, request.Lines, errors);
        errors.ThrowIfAny();

        if (!await repository.RecipeExistsAsync(recipeId, ct))
            throw KitchenException.NotFound("RECIPE_NOT_FOUND", "Recipe not found.");

        var previous = await repository.FindLatestVersionAsync(recipeId, ct);
        var current = previous?.Version ?? 0;

        if (current != request.ExpectedVersion)
            throw VersionConflict(current);

        // Omitted rounding parts are inherited from the previous version.
        var roundingErrors = new ValidationErrors();
        var rounding = ResolveRounding(
            request.RoundingRule, previous is null ? DefaultRounding : RoundingOf(previous), roundingErrors);
        roundingErrors.ThrowIfAny();

        var now = time.GetUtcNow();
        var lines = await BuildLinesAsync(request.ServingBasis!.Value, request.Lines!, now, ct);

        var next = current + 1;
        var version = StageVersion(
            recipeId, next, request.ServingBasis.Value, rounding, string.IsNullOrEmpty(reason) ? null : reason, now, lines);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            // Unique index (RecipeId, Version): lost a race against another writer.
            throw VersionConflict(next);
        }

        return new RecipeVersionCreatedResponse(
            recipeId, next, version.Id, previous?.Version, previous?.Id,
            version.RecordedAt.ToVn(), version.RecordedByUserId, version.Reason, rounding);
    }

    private static KitchenException VersionConflict(int currentVersion) =>
        new(409, "RECIPE_VERSION_CONFLICT",
            "The recipe was changed by someone else. Reload it and retry with the latest version.",
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

    private static NutritionRoundingRule ResolveRounding(
        RoundingRuleRequest? requested, NutritionRoundingRule fallback, ValidationErrors errors)
    {
        var kcal = requested?.KcalDecimals ?? fallback.KcalDecimals;
        var macro = requested?.MacroDecimals ?? fallback.ProteinDecimals;

        if (kcal is < 0 or > 3)
            errors.Add("roundingRule.kcalDecimals", "kcalDecimals must be between 0 and 3.");
        if (macro is < 0 or > 3)
            errors.Add("roundingRule.macroDecimals", "macroDecimals must be between 0 and 3.");

        return new NutritionRoundingRule(kcal, macro, RoundingMode);
    }

    private static string? NormalizeBasis(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "gross";
        var t = value.Trim().ToLowerInvariant();
        return t is "gross" or "edible" ? t : null;
    }

    /// <summary>Request-shape checks only (no database access).</summary>
    private static void ValidateShape(int? servingBasis, List<RecipeLineRequest>? lines, ValidationErrors errors)
    {
        if (servingBasis is null or < 1)
            errors.Add("servingBasis", "servingBasis must be at least 1.");

        if (lines is null || lines.Count == 0)
        {
            errors.Add("lines", "At least one line is required.");
            return;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line is null)
            {
                errors.Add($"lines[{i}]", "Line must not be null.");
                continue;
            }

            if (line.IngredientId is null || line.IngredientId == Guid.Empty)
                errors.Add($"lines[{i}].ingredientId", "ingredientId is required.");
            if (line.Quantity is null or <= 0m)
                errors.Add($"lines[{i}].quantity", "quantity must be greater than 0.");
            else if (line.Quantity > MaxQuantity)
                errors.Add($"lines[{i}].quantity", $"quantity must be at most {MaxQuantity:0}.");
            if (string.IsNullOrWhiteSpace(line.Unit))
                errors.Add($"lines[{i}].unit", "unit is required.");
            if (NormalizeBasis(line.WeightBasis) is null)
                errors.Add($"lines[{i}].weightBasis", "weightBasis must be 'gross' or 'edible'.");
        }

        var duplicates = lines
            .Where(l => l?.IngredientId is { } lineId && lineId != Guid.Empty)
            .GroupBy(l => l!.IngredientId!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dupId in duplicates)
            errors.Add("lines", $"Ingredient {dupId} appears more than once.");
    }

    /// <summary>
    /// Validates lines against the database and converts them to the stored form: original quantity/unit/weight
    /// basis, grams per portion, and the ingredient version in force right now (422 if none).
    /// </summary>
    private async Task<List<StagedLine>> BuildLinesAsync(
        int servingBasis, List<RecipeLineRequest> lines, DateTimeOffset now, CancellationToken ct)
    {
        var ids = lines.Select(l => l.IngredientId!.Value).ToArray();
        var found = await repository.GetIngredientsByIdAsync(ids, ct);

        var errors = new ValidationErrors();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!found.TryGetValue(line.IngredientId!.Value, out var ingredient))
            {
                errors.Add($"lines[{i}].ingredientId", "Ingredient not found.");
                continue;
            }

            if (!string.Equals(line.Unit!.Trim(), ingredient.Unit, StringComparison.OrdinalIgnoreCase))
                errors.Add($"lines[{i}].unit", $"Unit must match the ingredient unit '{ingredient.Unit}'.");
        }

        errors.ThrowIfAny();

        var effective = await ingredients.ResolveEffectiveAsync(ids, now, now, ct);
        var missing = ids
            .Where(id => !effective.ContainsKey(id))
            .Select(id => new MissingIngredient(id, found[id].Name))
            .ToList();

        if (missing.Count > 0)
            throw KitchenException.NutrientDataMissing(missing, now, now);

        var staged = new List<StagedLine>(lines.Count);
        var noDensity = new List<MissingIngredient>();
        var sizeErrors = new ValidationErrors();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var ingredientId = line.IngredientId!.Value;
            var ingredient = found[ingredientId];
            var version = effective[ingredientId];
            var quantity = Math.Round(line.Quantity!.Value, 3, MidpointRounding.AwayFromZero);

            decimal grams;
            if (ingredient.Unit == "ml")
            {
                // ml is never assumed to be g: a confirmed density on the nutrient version is required.
                if (version.GramsPerMl is not { } density)
                {
                    noDensity.Add(new MissingIngredient(ingredientId, ingredient.Name));
                    continue;
                }

                grams = quantity * density / servingBasis;
            }
            else
            {
                grams = quantity / servingBasis;
            }

            // The table keeps grams per portion with 3 decimals; the original quantity/unit are stored beside it.
            var perPortion = Math.Round(grams, 3, MidpointRounding.AwayFromZero);
            if (perPortion <= 0m)
                sizeErrors.Add($"lines[{i}].quantity", "Quantity per serving is too small to store (minimum 0.001 g).");

            staged.Add(new StagedLine(version.Id, quantity, ingredient.Unit, NormalizeBasis(line.WeightBasis)!, perPortion));
        }

        if (noDensity.Count > 0)
            throw KitchenException.UnitConversionMissing(noDensity);

        sizeErrors.ThrowIfAny();
        return staged;
    }

    private RecipeVersion StageVersion(
        Guid recipeId, int version, int servingBasis, NutritionRoundingRule rounding, string? reason,
        DateTimeOffset recordedAt, IEnumerable<StagedLine> lines)
    {
        var recipeVersion = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            Version = version,
            ServingBasis = servingBasis,
            RoundingKcalDecimals = rounding.KcalDecimals,
            RoundingMacroDecimals = rounding.ProteinDecimals,
            Reason = reason,
            RecordedByUserId = actor.UserId,
            RecordedAt = recordedAt
        };

        repository.AddRecipeVersion(recipeVersion);
        repository.AddRecipeIngredients(lines.Select(l => new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = recipeVersion.Id,
            IngredientVersionId = l.IngredientVersionId,
            Quantity = l.Quantity,
            Unit = l.Unit,
            WeightBasis = l.WeightBasis,
            GramsPerPortion = l.GramsPerPortion
        }));

        return recipeVersion;
    }
}

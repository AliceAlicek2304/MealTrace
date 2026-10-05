using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Kitchen.Services;

// ---- Contracts ------------------------------------------------------------

/// <summary>Quantity is the total for the whole batch (servingBasis portions), in the ingredient's unit (g or ml).</summary>
public sealed record RecipeLineRequest(Guid? IngredientId, decimal? Quantity, string? Unit);

public sealed record CreateRecipeRequest(string? Name, int? ServingBasis, List<RecipeLineRequest>? Lines);

public sealed record CreateRecipeVersionRequest(int? ExpectedVersion, int? ServingBasis, List<RecipeLineRequest>? Lines);

public sealed record RecipeVersionCreatedResponse(Guid RecipeId, int Version);

public interface IRecipeService
{
    Task<RecipeVersionCreatedResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct);

    Task<RecipeVersionCreatedResponse> AddVersionAsync(
        Guid recipeId, CreateRecipeVersionRequest request, CancellationToken ct);
}

// ---- Implementation -------------------------------------------------------

public sealed class RecipeService(MealTraceDbContext db, IIngredientService ingredients, TimeProvider time)
    : IRecipeService
{
    private const int MaxNameLength = 200;

    public async Task<RecipeVersionCreatedResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = request.Name?.Trim();

        if (string.IsNullOrEmpty(name))
            errors.Add("name", "Name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add("name", $"Name must be at most {MaxNameLength} characters.");

        ValidateShape(request.ServingBasis, request.Lines, errors);
        errors.ThrowIfAny();

        var now = time.GetUtcNow();
        var lines = await BuildLinesAsync(request.ServingBasis!.Value, request.Lines!, now, ct);

        var recipe = new Recipe { Id = Guid.NewGuid(), Name = name! };
        db.Recipes.Add(recipe);
        StageVersion(recipe.Id, 1, now, lines);

        await db.SaveChangesAsync(ct); // single SaveChanges => one transaction

        return new RecipeVersionCreatedResponse(recipe.Id, 1);
    }

    public async Task<RecipeVersionCreatedResponse> AddVersionAsync(
        Guid recipeId, CreateRecipeVersionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        if (request.ExpectedVersion is null or < 1)
            errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        ValidateShape(request.ServingBasis, request.Lines, errors);
        errors.ThrowIfAny();

        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId, ct))
            throw KitchenException.NotFound("RECIPE_NOT_FOUND", "Recipe not found.");

        var current = await db.RecipeVersions
            .Where(v => v.RecipeId == recipeId)
            .MaxAsync(v => (int?)v.Version, ct) ?? 0;

        if (current != request.ExpectedVersion)
            throw VersionConflict(current);

        var now = time.GetUtcNow();
        var lines = await BuildLinesAsync(request.ServingBasis!.Value, request.Lines!, now, ct);

        var next = current + 1;
        StageVersion(recipeId, next, now, lines);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Unique index (RecipeId, Version): lost a race against another writer.
            throw VersionConflict(next);
        }

        return new RecipeVersionCreatedResponse(recipeId, next);
    }

    private static KitchenException VersionConflict(int currentVersion) =>
        new(409, "RECIPE_VERSION_CONFLICT",
            "The recipe was changed by someone else. Reload it and retry with the latest version.",
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

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
            if (string.IsNullOrWhiteSpace(line.Unit))
                errors.Add($"lines[{i}].unit", "unit is required.");
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
    /// Validates lines against the database and converts them to the stored form:
    /// grams per portion + the ingredient version in force right now (422 if none).
    /// </summary>
    private async Task<List<(Guid IngredientVersionId, decimal GramsPerPortion)>> BuildLinesAsync(
        int servingBasis, List<RecipeLineRequest> lines, DateTimeOffset now, CancellationToken ct)
    {
        var ids = lines.Select(l => l.IngredientId!.Value).ToArray();
        var found = await db.Ingredients.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var errors = new ValidationErrors();
        var gramsPerPortion = new Dictionary<Guid, decimal>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var ingredientId = line.IngredientId!.Value;

            if (!found.TryGetValue(ingredientId, out var ingredient))
            {
                errors.Add($"lines[{i}].ingredientId", "Ingredient not found.");
                continue;
            }

            if (!string.Equals(line.Unit!.Trim(), ingredient.Unit, StringComparison.OrdinalIgnoreCase))
                errors.Add($"lines[{i}].unit", $"Unit must match the ingredient unit '{ingredient.Unit}'.");

            // Table stores grams per portion with 3 decimals; the batch quantity is divided by servingBasis here.
            var perPortion = Math.Round(line.Quantity!.Value / servingBasis, 3, MidpointRounding.AwayFromZero);
            if (perPortion <= 0m)
                errors.Add($"lines[{i}].quantity", "Quantity per serving is too small to store (minimum 0.001).");

            gramsPerPortion[ingredientId] = perPortion;
        }

        errors.ThrowIfAny();

        var effective = await ingredients.ResolveEffectiveAsync(ids, now, now, ct);
        var missing = ids
            .Where(id => !effective.ContainsKey(id))
            .Select(id => (object)new { IngredientId = id, found[id].Name })
            .ToList();

        if (missing.Count > 0)
            throw KitchenException.NutrientDataMissing(missing, now, now);

        return lines
            .Select(l => (effective[l.IngredientId!.Value].Id, gramsPerPortion[l.IngredientId!.Value]))
            .ToList();
    }

    private void StageVersion(
        Guid recipeId, int version, DateTimeOffset recordedAt,
        IEnumerable<(Guid IngredientVersionId, decimal GramsPerPortion)> lines)
    {
        var recipeVersion = new RecipeVersion
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            Version = version,
            RecordedAt = recordedAt
        };

        db.RecipeVersions.Add(recipeVersion);
        db.RecipeIngredients.AddRange(lines.Select(l => new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeVersionId = recipeVersion.Id,
            IngredientVersionId = l.IngredientVersionId,
            GramsPerPortion = l.GramsPerPortion
        }));
    }
}

using MealTrace.Application.Exceptions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;

namespace MealTrace.Application.Features.Kitchen;

public sealed class IngredientService(
    IIngredientRepository repository, TimeProvider time, IUnitOfWork unitOfWork, ICurrentActor actor) : IIngredientService
{
    private const int MaxNameLength = 200;
    private const int MaxSourceLength = 200;
    private const int MaxSourceRefLength = 500;
    private const decimal MaxKcalPer100G = 900m; // pure fat is ~900 kcal/100 g

    public async Task<IngredientCreatedResponse> CreateAsync(CreateIngredientRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var name = request.Name?.Trim();
        var unit = request.Unit?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(name))
            errors.Add("name", "Name is required.");
        else if (name.Length > MaxNameLength)
            errors.Add("name", $"Name must be at most {MaxNameLength} characters.");

        if (unit is not ("g" or "ml"))
            errors.Add("unit", "Unit must be 'g' or 'ml'.");

        errors.ThrowIfAny();

        // The edible ratio lives only on ingredient versions (EdibleFraction), never on the ingredient itself.
        if (await repository.IngredientNameExistsAsync(name!, ct))
            throw new KitchenException(409, "INGREDIENT_NAME_DUPLICATE", $"An ingredient named '{name}' already exists.");

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = name!,
            Unit = unit!
        };
        repository.AddIngredient(ingredient);
        await unitOfWork.SaveChangesAsync(ct);

        return new IngredientCreatedResponse(ingredient.Id, ingredient.Name, ingredient.Unit);
    }

    public async Task<IngredientListPage> ListAsync(
        string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var pageNo = Math.Max(page ?? 1, 1);

        var total = await repository.CountIngredientsAsync(search, ct);
        var skip = (int)Math.Min((long)(pageNo - 1) * size, int.MaxValue);

        // Stable order: Name, then Id as tie-breaker.
        var items = await repository.SearchIngredientsAsync(search, skip, size, ct);

        var now = time.GetUtcNow();
        var effective = await ResolveEffectiveAsync(items.Select(i => i.Id).ToArray(), now, now, ct);

        var result = items
            .Select(i => new IngredientListItem(
                i.Id, i.Name, i.Unit,
                effective.TryGetValue(i.Id, out var v) ? ToSnapshot(v) : null))
            .ToList();

        return new IngredientListPage(result, pageNo, size, total, now.ToVn(), now.ToVn());
    }

    public async Task<IngredientVersionCreatedResponse> AddVersionAsync(
        Guid ingredientId, CreateIngredientVersionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var validFrom = request.ValidFrom?.ToUniversalTime();
        var validTo = request.ValidTo?.ToUniversalTime();
        var source = request.Source?.Trim();
        var sourceRef = request.SourceRef?.Trim();

        if (validFrom is null)
            errors.Add("validFrom", "validFrom is required.");
        if (validFrom is not null && validTo is not null && validTo <= validFrom)
            errors.Add("validTo", "validTo must be later than validFrom.");

        if (!InRange(request.EdiblePercent, 0m, 100m))
            errors.Add("ediblePercent", "ediblePercent must be between 0 and 100.");
        if (!InRange(request.EnergyKcal, 0m, MaxKcalPer100G))
            errors.Add("energyKcal", $"energyKcal must be between 0 and {MaxKcalPer100G:0} per 100 g.");
        if (!InRange(request.ProteinG, 0m, 100m))
            errors.Add("proteinG", "proteinG must be between 0 and 100 per 100 g.");
        if (!InRange(request.FatG, 0m, 100m))
            errors.Add("fatG", "fatG is required and must be between 0 and 100 per 100 g.");
        if (!InRange(request.CarbG, 0m, 100m))
            errors.Add("carbG", "carbG is required and must be between 0 and 100 per 100 g.");
        if (request.ProteinG is { } p && request.FatG is { } f && request.CarbG is { } c && p + f + c > 100m)
            errors.Add("proteinG", "proteinG + fatG + carbG cannot exceed 100 g per 100 g.");

        if (request.GramsPerMl is not null && !InRange(request.GramsPerMl, 0.001m, 5m))
            errors.Add("gramsPerMl", "gramsPerMl must be between 0.001 and 5.");
        if (request.PricePerKg is not null && !InRange(request.PricePerKg, 0m, 999_999_999m))
            errors.Add("pricePerKg", "pricePerKg must be between 0 and 999,999,999.");

        ValidateSource(source, sourceRef, errors);
        errors.ThrowIfAny();

        if (!await repository.IngredientExistsAsync(ingredientId, ct))
            throw KitchenException.NotFound("INGREDIENT_NOT_FOUND", "Ingredient not found.");

        var from = validFrom!.Value;
        var history = await repository.ListVersionsAsync([ingredientId], null, ct);
        var now = time.GetUtcNow();
        var live = IngredientTimeline.Live(history, now);

        // supersedesId: an explicit correction of one live version of THIS ingredient.
        if (request.SupersedesId is { } supersedesId)
        {
            if (history.All(v => v.Id != supersedesId))
            {
                errors.Add("supersedesId", "supersedesId must reference an existing version of this ingredient.");
                errors.ThrowIfAny();
            }

            if (live.All(v => v.Id != supersedesId))
                throw new KitchenException(409, "INGREDIENT_VERSION_ALREADY_SUPERSEDED",
                    "That version has already been superseded. Supersede the latest live version instead.");
        }

        // Overlap guard (official rule): overlapping a live row is allowed only when the new interval starts
        // strictly later than that row; otherwise the caller must supersede it explicitly.
        var conflicts = live
            .Where(v => v.Id != request.SupersedesId
                        && Overlaps(v, from, validTo)
                        && from <= v.EffectiveFrom)
            .Select(v => v.Id)
            .ToList();

        if (conflicts.Count > 0)
            throw new KitchenException(409, "INGREDIENT_VERSION_OVERLAP",
                "The validity interval overlaps an existing version that starts at or after validFrom. " +
                "Set supersedesId to correct it, or adjust validFrom/validTo.",
                new Dictionary<string, object?> { ["conflictingVersionIds"] = conflicts });

        var version = new IngredientVersion
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredientId,
            Version = history.Count == 0 ? 1 : history.Max(v => v.Version) + 1,
            EnergyKcalPer100G = Math.Round(request.EnergyKcal!.Value, 3),
            ProteinGPer100G = Math.Round(request.ProteinG!.Value, 3),
            FatGPer100G = Math.Round(request.FatG!.Value, 3),
            CarbGPer100G = Math.Round(request.CarbG!.Value, 3),
            GramsPerMl = request.GramsPerMl is { } density ? Math.Round(density, 4) : null,
            // PricePerKg is NOT NULL in the table; 0 means "no price data" and is never used for nutrition.
            PricePerKg = Math.Round(request.PricePerKg ?? 0m, 2),
            EdibleFraction = Math.Round(request.EdiblePercent!.Value, 2) / 100m,
            EffectiveFrom = from,
            EffectiveTo = validTo,
            Source = source,
            SourceRef = string.IsNullOrEmpty(sourceRef) ? null : sourceRef,
            RecordedByUserId = actor.UserId,
            SupersedesId = request.SupersedesId,
            RecordedAt = now
        };

        repository.AddIngredientVersion(version);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            // Unique indexes (IngredientId, Version) and (SupersedesId): a concurrent writer won.
            throw new KitchenException(409, "INGREDIENT_VERSION_CONFLICT",
                "Another version or correction was recorded concurrently. Reload and retry.");
        }

        return new IngredientVersionCreatedResponse(
            version.Id, version.Version, version.EffectiveFrom.ToVn(), version.RecordedAt.ToVn(),
            version.EffectiveTo?.ToVn(), version.RecordedByUserId, version.SupersedesId, version.Source, version.SourceRef);
    }

    public async Task<Dictionary<Guid, IngredientVersion>> ResolveEffectiveAsync(
        IReadOnlyCollection<Guid> ingredientIds, DateTimeOffset validAt, DateTimeOffset knownAt, CancellationToken ct)
    {
        if (ingredientIds.Count == 0) return new Dictionary<Guid, IngredientVersion>();

        var ids = ingredientIds.Distinct().ToArray();
        var validUtc = validAt.ToUniversalTime();
        var knownUtc = knownAt.ToUniversalTime();

        // The whole known history is loaded (not only rows covering validAt) because a superseding row can have a
        // different interval from the row it replaces. See IngredientTimeline for the rule.
        var history = await repository.ListVersionsAsync(ids, knownUtc, ct);

        var result = new Dictionary<Guid, IngredientVersion>();
        foreach (var group in history.GroupBy(v => v.IngredientId))
        {
            var picked = IngredientTimeline.PickEffective(group, validUtc, knownUtc);
            if (picked is not null) result[group.Key] = picked;
        }

        return result;
    }

    private static NutrientSnapshot ToSnapshot(IngredientVersion v) => new(
        v.Id, v.Version, v.EffectiveFrom.ToVn(), v.RecordedAt.ToVn(),
        v.EdibleFraction * 100m, v.EnergyKcalPer100G, v.ProteinGPer100G,
        v.EffectiveTo?.ToVn(), v.FatGPer100G, v.CarbGPer100G, v.GramsPerMl, v.Source, v.SourceRef);

    private static bool Overlaps(IngredientVersion existing, DateTimeOffset from, DateTimeOffset? to) =>
        existing.EffectiveFrom < (to ?? DateTimeOffset.MaxValue)
        && from < (existing.EffectiveTo ?? DateTimeOffset.MaxValue);

    private static void ValidateSource(string? source, string? sourceRef, ValidationErrors errors)
    {
        if (string.IsNullOrEmpty(source))
            errors.Add("source", "source is required (e.g. the reference table or document name).");
        else if (source.Length > MaxSourceLength)
            errors.Add("source", $"source must be at most {MaxSourceLength} characters.");
        else if (source.Any(char.IsControl))
            errors.Add("source", "source must not contain control characters.");

        if (string.IsNullOrEmpty(sourceRef)) return;

        if (sourceRef.Length > MaxSourceRefLength)
            errors.Add("sourceRef", $"sourceRef must be at most {MaxSourceRefLength} characters.");
        else if (sourceRef.Any(char.IsControl))
            errors.Add("sourceRef", "sourceRef must not contain control characters.");
        else if (sourceRef.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                 && !(Uri.TryCreate(sourceRef, UriKind.Absolute, out var uri)
                      && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            errors.Add("sourceRef", "sourceRef looks like a URL but is not a valid http(s) address.");
    }

    private static bool InRange(decimal? value, decimal min, decimal max) =>
        value is { } x && x >= min && x <= max;
}

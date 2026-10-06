using MealTrace.Application.Exceptions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;

namespace MealTrace.Application.Features.Kitchen;

public sealed class IngredientService(IIngredientRepository repository, TimeProvider time, IUnitOfWork unitOfWork) : IIngredientService
{
    private const int MaxNameLength = 200;
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

    public async Task<PagedResult<IngredientListItem>> ListAsync(
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

        return new PagedResult<IngredientListItem>(result, pageNo, size, total);
    }

    public async Task<IngredientVersionCreatedResponse> AddVersionAsync(
        Guid ingredientId, CreateIngredientVersionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.ValidFrom is null)
            errors.Add("validFrom", "validFrom is required.");
        if (!InRange(request.EdiblePercent, 0m, 100m))
            errors.Add("ediblePercent", "ediblePercent must be between 0 and 100.");
        if (!InRange(request.EnergyKcal, 0m, MaxKcalPer100G))
            errors.Add("energyKcal", $"energyKcal must be between 0 and {MaxKcalPer100G:0} per 100 g.");
        if (!InRange(request.ProteinG, 0m, 100m))
            errors.Add("proteinG", "proteinG must be between 0 and 100 per 100 g.");
        if (request.PricePerKg is not null && !InRange(request.PricePerKg, 0m, 999_999_999m))
            errors.Add("pricePerKg", "pricePerKg must be between 0 and 999,999,999.");

        errors.ThrowIfAny();

        if (!await repository.IngredientExistsAsync(ingredientId, ct))
            throw KitchenException.NotFound("INGREDIENT_NOT_FOUND", "Ingredient not found.");

        var current = await repository.GetLatestVersionNumberAsync(ingredientId, ct) ?? 0;

        var version = new IngredientVersion
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredientId,
            Version = current + 1,
            EnergyKcalPer100G = Math.Round(request.EnergyKcal!.Value, 3),
            ProteinGPer100G = Math.Round(request.ProteinG!.Value, 3),
            // PricePerKg is NOT NULL in the table; 0 means "no price data" and is never used for nutrition.
            PricePerKg = Math.Round(request.PricePerKg ?? 0m, 2),
            EdibleFraction = Math.Round(request.EdiblePercent!.Value, 2) / 100m,
            EffectiveFrom = request.ValidFrom!.Value.ToUniversalTime(),
            RecordedAt = time.GetUtcNow()
        };

        repository.AddIngredientVersion(version);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            // Unique index (IngredientId, Version): someone else appended at the same moment.
            throw new KitchenException(409, "INGREDIENT_VERSION_CONFLICT",
                "Another version was recorded concurrently. Retry the request.");
        }

        return new IngredientVersionCreatedResponse(
            version.Id, version.Version, version.EffectiveFrom.ToVn(), version.RecordedAt.ToVn());
    }

    public async Task<Dictionary<Guid, IngredientVersion>> ResolveEffectiveAsync(
        IReadOnlyCollection<Guid> ingredientIds, DateTimeOffset validAt, DateTimeOffset knownAt, CancellationToken ct)
    {
        if (ingredientIds.Count == 0) return new Dictionary<Guid, IngredientVersion>();

        var ids = ingredientIds.Distinct().ToArray();
        var validUtc = validAt.ToUniversalTime();
        var knownUtc = knownAt.ToUniversalTime();

        // Valid axis: EffectiveFrom <= validAt. Transaction axis: RecordedAt <= knownAt.
        // The table has no ValidTo, so a row stays in force until a later EffectiveFrom (known at
        // knownAt) supersedes it. Ties on EffectiveFrom go to the most recently recorded row.
        var candidates = await repository.ListEffectiveVersionCandidatesAsync(ids, validUtc, knownUtc, ct);

        return candidates
            .GroupBy(v => v.IngredientId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(v => v.EffectiveFrom)
                      .ThenByDescending(v => v.RecordedAt)
                      .ThenByDescending(v => v.Version)
                      .First());
    }

    private static NutrientSnapshot ToSnapshot(IngredientVersion v) => new(
        v.Id, v.Version, v.EffectiveFrom.ToVn(), v.RecordedAt.ToVn(),
        v.EdibleFraction * 100m, v.EnergyKcalPer100G, v.ProteinGPer100G);

    private static bool InRange(decimal? value, decimal min, decimal max) =>
        value is { } x && x >= min && x <= max;

}

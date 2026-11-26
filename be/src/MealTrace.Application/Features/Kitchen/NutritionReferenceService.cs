using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

/// <summary>
/// Selection of documented age-group ranges. Same shape as <see cref="IngredientTimeline"/>: known (RecordedAt &lt;= knownAt),
/// live (not superseded by a known row), applicable (EffectiveFrom &lt;= validAt &lt; EffectiveTo), then, per
/// nutrient + basis + meal type, the latest EffectiveFrom, then RecordedAt, then Version.
/// </summary>
public static class ReferenceTimeline
{
    public static IReadOnlyList<NutrientReferenceRange> Live(IEnumerable<NutrientReferenceRange> rows, DateTimeOffset knownUtc)
    {
        var known = rows.Where(r => r.RecordedAt <= knownUtc).ToList();
        var superseded = known.Where(r => r.SupersedesId is not null).Select(r => r.SupersedesId!.Value).ToHashSet();
        return known.Where(r => !superseded.Contains(r.Id)).ToList();
    }

    public static IReadOnlyList<NutrientReferenceRange> Effective(
        IEnumerable<NutrientReferenceRange> rows, DateTimeOffset validUtc, DateTimeOffset knownUtc) =>
        Live(rows, knownUtc)
            .Where(r => r.EffectiveFrom <= validUtc && (r.EffectiveTo is null || validUtc < r.EffectiveTo))
            .GroupBy(r => (r.Nutrient, r.Basis, MealType: r.MealType?.ToLowerInvariant()))
            .Select(g => g.OrderByDescending(r => r.EffectiveFrom)
                          .ThenByDescending(r => r.RecordedAt)
                          .ThenByDescending(r => r.Version)
                          .First())
            .ToList();

    public static bool SameKey(NutrientReferenceRange a, NutrientReferenceRange b) =>
        string.Equals(a.Nutrient, b.Nutrient, StringComparison.Ordinal)
        && string.Equals(a.Basis, b.Basis, StringComparison.Ordinal)
        && string.Equals(a.MealType, b.MealType, StringComparison.OrdinalIgnoreCase);
}

public sealed class NutritionReferenceService(
    INutritionRepository repository, TimeProvider time, IUnitOfWork unitOfWork, ICurrentActor actor)
    : INutritionReferenceService
{
    private static readonly string[] Nutrients = ["kcal", "proteinG", "fatG", "carbG"];

    public async Task<NutritionReferenceCreatedResponse> CreateAsync(CreateNutritionReferenceRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var ageGroup = request.AgeGroup?.Trim();
        var nutrient = Nutrients.FirstOrDefault(n => string.Equals(n, request.Nutrient?.Trim(), StringComparison.OrdinalIgnoreCase));
        var basis = request.Basis?.Trim().ToLowerInvariant() switch
        {
            "permeal" => ReferenceBasis.PerMeal,
            "daily" => ReferenceBasis.Daily,
            _ => null
        };
        var mealType = string.IsNullOrWhiteSpace(request.MealType) ? null : request.MealType.Trim();
        var source = request.Source?.Trim();
        var sourceRef = request.SourceRef?.Trim();
        var reason = request.Reason?.Trim();
        var from = request.EffectiveFrom?.ToUniversalTime();
        var to = request.EffectiveTo?.ToUniversalTime();

        if (string.IsNullOrEmpty(ageGroup)) errors.Add("ageGroup", "ageGroup is required.");
        else if (ageGroup.Length > 50) errors.Add("ageGroup", "ageGroup must be at most 50 characters.");
        if (nutrient is null) errors.Add("nutrient", "nutrient must be one of: kcal, proteinG, fatG, carbG.");
        if (basis is null) errors.Add("basis", "basis is required and must be 'perMeal' or 'daily'.");
        if (mealType is { Length: > 30 }) errors.Add("mealType", "mealType must be at most 30 characters.");

        if (request.MinValue is null && request.MaxValue is null)
            errors.Add("minValue", "At least one of minValue or maxValue is required.");
        if (request.MinValue is < 0m) errors.Add("minValue", "minValue must not be negative.");
        if (request.MaxValue is < 0m) errors.Add("maxValue", "maxValue must not be negative.");
        if (request.MinValue is { } lo && request.MaxValue is { } hi && lo > hi)
            errors.Add("minValue", "minValue must not be greater than maxValue.");
        if (request.MinValue > 1_000_000_000m || request.MaxValue > 1_000_000_000m)
            errors.Add("maxValue", "Values must be at most 1,000,000,000.");

        IngredientService.ValidateSource(source, sourceRef, errors);
        if (from is null) errors.Add("effectiveFrom", "effectiveFrom is required.");
        if (from is not null && to is not null && to <= from) errors.Add("effectiveTo", "effectiveTo must be later than effectiveFrom.");
        if (string.IsNullOrEmpty(reason)) errors.Add("reason", "reason is required.");
        else if (reason.Length > 500) errors.Add("reason", "reason must be at most 500 characters.");
        errors.ThrowIfAny();

        var candidate = new NutrientReferenceRange
        {
            AgeGroup = ageGroup!, Nutrient = nutrient!, Basis = basis, MealType = mealType, Source = source!
        };

        var rows = await repository.ListReferenceRangesAsync(ageGroup!, null, ct);
        var now = time.GetUtcNow();
        var live = ReferenceTimeline.Live(rows, now);

        NutrientReferenceRange? replaced = null;
        if (request.SupersedesId is { } supersedesId)
        {
            replaced = rows.FirstOrDefault(r => r.Id == supersedesId);
            if (replaced is null || !ReferenceTimeline.SameKey(replaced, candidate))
            {
                errors.Add("supersedesId", "supersedesId must reference a range with the same age group, nutrient, basis and meal type.");
                errors.ThrowIfAny();
            }

            if (live.All(r => r.Id != supersedesId))
                throw new KitchenException(409, "REFERENCE_ALREADY_SUPERSEDED",
                    "That reference has already been superseded. Supersede the latest live one instead.");
        }

        // Same overlap rule as nutrient versions: a new interval may overlap a live range of the same key only if
        // it starts strictly later; otherwise it must supersede explicitly.
        var conflicts = live
            .Where(r => ReferenceTimeline.SameKey(r, candidate) && r.Id != request.SupersedesId
                        && r.EffectiveFrom < (to ?? DateTimeOffset.MaxValue)
                        && from!.Value < (r.EffectiveTo ?? DateTimeOffset.MaxValue)
                        && from.Value <= r.EffectiveFrom)
            .Select(r => r.Id).ToList();
        if (conflicts.Count > 0)
            throw new KitchenException(409, "REFERENCE_OVERLAP",
                "The validity interval overlaps an existing range that starts at or after effectiveFrom. " +
                "Set supersedesId to correct it, or adjust effectiveFrom/effectiveTo.",
                new Dictionary<string, object?> { ["conflictingReferenceIds"] = conflicts });

        var sameKeyAll = rows.Where(r => ReferenceTimeline.SameKey(r, candidate)).ToList();
        var range = new NutrientReferenceRange
        {
            Id = Guid.NewGuid(),
            AgeGroup = ageGroup!,
            Nutrient = nutrient!,
            MinValue = request.MinValue is { } min ? Math.Round(min, 3) : null,
            MaxValue = request.MaxValue is { } max ? Math.Round(max, 3) : null,
            Basis = basis,
            MealType = mealType,
            Source = source!,
            SourceRef = string.IsNullOrEmpty(sourceRef) ? null : sourceRef,
            EffectiveFrom = from!.Value,
            EffectiveTo = to,
            RecordedAt = now,
            RecordedByUserId = actor.UserId,
            SupersedesId = request.SupersedesId,
            Reason = reason,
            Version = replaced is not null ? replaced.Version + 1 : sameKeyAll.Count == 0 ? 1 : sameKeyAll.Max(r => r.Version) + 1
        };

        repository.AddReference(range);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            throw new KitchenException(409, "REFERENCE_CONFLICT", "That reference was superseded concurrently. Reload and retry.");
        }

        return new NutritionReferenceCreatedResponse(range.Id, range.Version, range.RecordedAt.ToVn(), range.RecordedByUserId, range.SupersedesId);
    }

    public async Task<NutritionReferenceList> ListAsync(
        string? ageGroup, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct)
    {
        var group = ageGroup?.Trim();
        if (string.IsNullOrEmpty(group))
        {
            var errors = new ValidationErrors();
            errors.Add("ageGroup", "ageGroup is required.");
            errors.ThrowIfAny();
        }

        var now = time.GetUtcNow();
        var valid = (validAt ?? now).ToUniversalTime();
        var known = (knownAt ?? now).ToUniversalTime();

        var rows = await repository.ListReferenceRangesAsync(group!, known, ct);
        var items = ReferenceTimeline.Effective(rows, valid, known)
            .OrderBy(r => r.Nutrient, StringComparer.Ordinal).ThenBy(r => r.Basis, StringComparer.Ordinal).ThenBy(r => r.MealType)
            .Select(r => new NutritionReferenceView(
                r.Id, r.Version, r.AgeGroup, r.Nutrient, r.MinValue, r.MaxValue, r.Basis, r.MealType, r.Source, r.SourceRef,
                r.EffectiveFrom.ToVn(), r.EffectiveTo?.ToVn(), r.RecordedAt.ToVn(), r.SupersedesId))
            .ToList();

        return new NutritionReferenceList(group!, valid.ToVn(), known.ToVn(), items);
    }
}

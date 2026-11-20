using System.Text;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

/// <summary>
/// Planned ingredient quantities for published menus, from the latest settlement of each class. A planning estimate only:
/// no purchasing, no stock, and it is different from the dated actual receipts in ingredient source entries (LD-07).
/// </summary>
public sealed class IngredientEstimateService(
    IMenuRepository menus, INutritionRepository nutritionRepository, IKitchenSettlementService settlements,
    IKitchenSettlementRepository settlementRepository, IIngredientEstimateRepository estimates,
    TimeProvider time, IUnitOfWork unitOfWork, ICurrentActor actor) : IIngredientEstimateService
{
    private sealed class Accumulator(Guid ingredientId, string name)
    {
        public Guid IngredientId { get; } = ingredientId;
        public string Name { get; } = name;
        public decimal Gross { get; set; }
        public decimal Edible { get; set; }
        public HashSet<Guid> RecipeVersions { get; } = [];
        public HashSet<Guid> NutrientVersions { get; } = [];
        public List<EstimateConversion> Conversions { get; } = [];
    }

    public async Task<IngredientEstimateResponse> CreateAsync(IngredientEstimateRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var versionIds = request.MenuVersionIds?.Where(i => i != Guid.Empty).Distinct().OrderBy(i => i).ToList() ?? [];
        var classFilter = request.ClassIds?.Distinct().ToHashSet();
        if (versionIds.Count == 0) errors.Add("menuVersionIds", "At least one menu version is required.");
        if (versionIds.Count > 31) errors.Add("menuVersionIds", "An estimate covers at most 31 menu versions.");
        errors.ThrowIfAny();

        // 1. Menu versions must be published, and each belongs to one meal day.
        var dayOf = new Dictionary<Guid, Guid>();
        foreach (var versionId in versionIds)
        {
            var version = await menus.FindMenuVersionAsync(versionId, ct)
                ?? throw KitchenException.NotFound("MENU_VERSION_NOT_FOUND", $"Menu version {versionId} not found.");
            var menu = await menus.FindMenuAsync(version.MenuId, ct)
                ?? throw KitchenException.NotFound("MENU_NOT_FOUND", "Menu not found.");
            var transitions = await menus.ListTransitionsAsync([versionId], ct);
            var state = transitions.OrderBy(t => t.Sequence).LastOrDefault()?.State;
            if (state != MenuStates.Published)
                throw new KitchenException(422, "MENU_NOT_PUBLISHED",
                    $"Menu version {versionId} is not published; an estimate only uses published menus.",
                    new Dictionary<string, object?> { ["menuVersionId"] = versionId, ["state"] = state });
            dayOf[versionId] = menu.MealDayId;
        }

        var mealDays = dayOf.Values.Distinct().OrderBy(i => i).ToList();
        if (dayOf.Values.GroupBy(d => d).Any(g => g.Count() > 1))
            errors.Add("menuVersionIds", "Two versions of the same menu cannot be estimated together.");
        if (request.MealDayIds is { Count: > 0 } requestedDays && !requestedDays.OrderBy(i => i).SequenceEqual(mealDays))
            errors.Add("mealDayIds", "mealDayIds must be exactly the meal days of the given menu versions.");
        foreach (var day in mealDays)
            if (request.SnapshotTokens is null || !request.SnapshotTokens.ContainsKey(day))
                errors.Add($"snapshotTokens.{day}", "A snapshot token is required for every meal day.");
        errors.ThrowIfAny();

        // 2. Latest settlement of each class, checked against the token the caller saw.
        var portions = new List<EstimatePortion>();
        var portionsByDay = new Dictionary<Guid, int>();
        foreach (var day in mealDays)
        {
            var diff = await settlements.GetSettlementDiffAsync(day, ct);
            if (!string.Equals(diff.SnapshotToken, request.SnapshotTokens![day], StringComparison.Ordinal))
                throw new KitchenException(409, "STALE_SETTLEMENT",
                    "The settlement changed after you loaded it. Reload the settlement and estimate again.",
                    new Dictionary<string, object?> { ["mealDayId"] = day, ["snapshotToken"] = diff.SnapshotToken });

            var detail = await settlementRepository.FindSettledMealDayWithDetailsAsync(day, ct);
            var latestIds = detail?.Settlements.Where(s => s.ClassId != null)
                .GroupBy(s => s.ClassId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Version).First().Id) ?? [];

            var selected = diff.ClassDiffs.Where(c => classFilter is null || classFilter.Count == 0 || classFilter.Contains(c.ClassId)).ToList();
            if (selected.Count == 0)
            {
                errors.Add("classIds", $"No class of meal day {day} matches classIds.");
                continue;
            }

            foreach (var c in selected)
                portions.Add(new EstimatePortion(day, c.ClassId, c.CurrentVersion, c.AppliedCount,
                    latestIds.TryGetValue(c.ClassId, out var sid) ? sid : null));
            portionsByDay[day] = selected.Sum(c => c.AppliedCount);
        }
        errors.ThrowIfAny();

        // 3. Ingredient needs: every dish is served once per portion; conversions come from the pinned versions.
        var totals = new Dictionary<Guid, Accumulator>();
        var noDensity = new List<MissingIngredient>();
        var legacyMl = new List<MissingIngredient>();
        var badFraction = new List<MissingIngredient>();

        foreach (var versionId in versionIds)
        {
            var count = portionsByDay[dayOf[versionId]];
            var items = (await menus.ListItemsAsync([versionId], ct)).OrderBy(i => i.SortOrder).ToList();
            var refs = (await menus.FindRecipeVersionsAsync(items.Select(i => i.RecipeVersionId).ToArray(), ct))
                .ToDictionary(r => r.Version.Id);

            foreach (var item in items)
            {
                var recipeVersion = refs[item.RecipeVersionId].Version;
                var servingBasis = recipeVersion.ServingBasis ?? 1;

                foreach (var row in await nutritionRepository.ListRecipeNutritionLinesAsync(recipeVersion, ct))
                {
                    decimal grams;
                    decimal? density = null;
                    var unit = row.Unit ?? "g";
                    if (row.Quantity is { } quantity && row.Unit is not null)
                    {
                        if (row.Unit == "ml")
                        {
                            if (row.Pinned.GramsPerMl is not { } d) { noDensity.Add(new MissingIngredient(row.IngredientId, row.IngredientName)); continue; }
                            density = d;
                            grams = quantity * d / servingBasis;
                        }
                        else grams = quantity / servingBasis;
                    }
                    else
                    {
                        if (row.IngredientUnit == "ml") { legacyMl.Add(new MissingIngredient(row.IngredientId, row.IngredientName)); continue; }
                        grams = row.GramsPerPortion;
                    }

                    var basis = row.WeightBasis ?? "gross";
                    var fraction = row.Pinned.EdibleFraction;
                    decimal gross, edible;
                    if (basis == "edible")
                    {
                        if (fraction <= 0m) { badFraction.Add(new MissingIngredient(row.IngredientId, row.IngredientName)); continue; }
                        edible = grams;
                        gross = grams / fraction;
                    }
                    else
                    {
                        gross = grams;
                        edible = grams * fraction;
                    }

                    if (!totals.TryGetValue(row.IngredientId, out var acc))
                        totals[row.IngredientId] = acc = new Accumulator(row.IngredientId, row.IngredientName);
                    acc.Gross += gross * count;
                    acc.Edible += edible * count;
                    acc.RecipeVersions.Add(recipeVersion.Id);
                    acc.NutrientVersions.Add(row.Pinned.Id);
                    acc.Conversions.Add(new EstimateConversion(recipeVersion.Id, unit, basis, density, fraction));
                }
            }
        }

        if (noDensity.Count > 0) throw KitchenException.UnitConversionMissing(noDensity.DistinctBy(m => m.IngredientId).ToList());
        if (legacyMl.Count > 0)
            throw new KitchenException(422, "LEGACY_CONVERSION_UNVERIFIED",
                "Some recipe lines are legacy lines of an ml ingredient with no recorded gram conversion.",
                new Dictionary<string, object?> { ["missingIngredients"] = legacyMl.DistinctBy(m => m.IngredientId).ToList() });
        if (badFraction.Count > 0)
            throw new KitchenException(422, "EDIBLE_FRACTION_INVALID",
                "An edible-weight line cannot be converted to gross weight because the edible fraction is 0.",
                new Dictionary<string, object?> { ["missingIngredients"] = badFraction.DistinctBy(m => m.IngredientId).ToList() });

        // Rounded once, after aggregating across dishes, classes and meal days.
        var lines = totals.Values.OrderBy(a => a.Name, StringComparer.Ordinal).ThenBy(a => a.IngredientId)
            .Select(a =>
            {
                var gross = Math.Round(a.Gross, 3, MidpointRounding.AwayFromZero);
                return new EstimateLine(
                    a.IngredientId, a.Name, gross, "g", gross, Math.Round(a.Edible, 3, MidpointRounding.AwayFromZero),
                    a.RecipeVersions.OrderBy(i => i).ToList(), a.NutrientVersions.OrderBy(i => i).ToList(),
                    a.Conversions.DistinctBy(c => c.RecipeVersionId).OrderBy(c => c.RecipeVersionId).ToList());
            }).ToList();

        var payload = new EstimatePayload(
            versionIds, portions.Select(p => p.SettlementId).Where(i => i != null).Select(i => i!.Value).Distinct().OrderBy(i => i).ToList(),
            portions.OrderBy(p => p.MealDayId).ThenBy(p => p.ClassId).ToList(), lines);
        var payloadJson = KitchenJson.Serialize(payload);
        var inputJson = KitchenJson.Serialize(new
        {
            menuVersionIds = versionIds,
            classIds = classFilter?.OrderBy(i => i).ToList(),
            snapshotTokens = request.SnapshotTokens!.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value)
        });

        var estimate = new IngredientEstimate
        {
            Id = Guid.NewGuid(), InputJson = inputJson, PayloadJson = payloadJson, PayloadHash = KitchenJson.Sha256(payloadJson),
            CreatedAt = time.GetUtcNow(), CreatedByUserId = actor.UserId
        };
        estimates.Add(estimate);
        await unitOfWork.SaveChangesAsync(ct);

        return ToResponse(estimate, payload);
    }

    public async Task<IngredientEstimateResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var estimate = await estimates.FindAsync(id, ct)
            ?? throw KitchenException.NotFound("ESTIMATE_NOT_FOUND", "Estimate not found.");
        // Served from what was stored, never recomputed: later amendments make a new estimate, not a changed one.
        return ToResponse(estimate, KitchenJson.Deserialize<EstimatePayload>(estimate.PayloadJson));
    }

    public async Task<string> ExportCsvAsync(Guid id, CancellationToken ct)
    {
        var response = await GetAsync(id, ct);
        var csv = new StringBuilder();
        csv.Append("ingredientId,ingredientName,unit,grossGrams,edibleGrams\r\n");
        foreach (var line in response.Lines)
            csv.Append(string.Join(',',
                Cell(line.IngredientId.ToString()), Cell(line.IngredientName), Cell(line.Unit),
                Cell(line.GrossGrams.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                Cell(line.EdibleGrams.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))))
               .Append("\r\n");
        return csv.ToString();
    }

    /// <summary>RFC 4180 quoting, plus a leading apostrophe on cells a spreadsheet would run as a formula.</summary>
    internal static string Cell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static IngredientEstimateResponse ToResponse(IngredientEstimate e, EstimatePayload p) =>
        new(e.Id, e.CreatedAt.ToVn(), p.MenuVersionIds, p.SettlementIds, p.Portions, p.Lines, true, e.PayloadHash);
}

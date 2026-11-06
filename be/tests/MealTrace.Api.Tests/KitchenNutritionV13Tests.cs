using System.Data;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Features.Kitchen;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Api.Tests;

/// <summary>
/// BE1 (Kitchen) v1.3 logic tests. They run the real Application services over in-memory repositories, so they need
/// neither PostgreSQL nor SQLite and cover the business rules (temporal rule, gross/edible, ml density, rounding,
/// pinned vs recompute, as-known-at, legacy rows, age-group comparison) independently of persistence.
/// </summary>
public sealed class KitchenNutritionV13Tests
{
    private static DateTimeOffset D(int day, int hour = 0) => new(2026, 1, day, hour, 0, 0, TimeSpan.Zero);

    // ---- Ingredients: create + validation ----------------------------------------------------------------

    [Fact]
    public async Task CreateIngredientRejectsBadUnitAndDuplicateNames()
    {
        var w = new K13World();

        var bad = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.CreateAsync(new("Rice", "kg"), default));
        Assert.Equal("VALIDATION_ERROR", bad.Code);
        Assert.True(K13World.Errors(bad).ContainsKey("unit"));

        var created = await w.Ingredients.CreateAsync(new("  Rice ", "G"), default);
        Assert.Equal("Rice", created.Name);
        Assert.Equal("g", created.Unit);

        var duplicate = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.CreateAsync(new("rice", "g"), default));
        Assert.Equal(409, duplicate.Status);
        Assert.Equal("INGREDIENT_NAME_DUPLICATE", duplicate.Code);
    }

    [Fact]
    public async Task IngredientVersionRequiresSourceAndFullMacros()
    {
        var w = new K13World();
        var rice = await w.AddIngredient("Rice");

        var request = new CreateIngredientVersionRequest(D(1), 100m, 360m, 7m, null);
        var ex = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(rice, request, default));

        var errors = K13World.Errors(ex);
        Assert.Equal(400, ex.Status);
        Assert.True(errors.ContainsKey("source"));
        Assert.True(errors.ContainsKey("fatG"));
        Assert.True(errors.ContainsKey("carbG"));
    }

    [Fact]
    public async Task IngredientVersionRejectsMalformedSourceAndImpossibleMacros()
    {
        var w = new K13World();
        var rice = await w.AddIngredient("Rice");

        var badRef = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 360m, 7m, 1m, 80m, sourceRef: "http://not a url"), default));
        Assert.True(K13World.Errors(badRef).ContainsKey("sourceRef"));

        var badControl = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 360m, 7m, 1m, 80m, source: "bad\u0007source"), default));
        Assert.True(K13World.Errors(badControl).ContainsKey("source"));

        var tooMuch = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 360m, 50m, 40m, 30m), default));
        Assert.True(K13World.Errors(tooMuch).ContainsKey("proteinG"));

        var badInterval = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(5), 360m, 7m, 1m, 80m, to: D(5)), default));
        Assert.True(K13World.Errors(badInterval).ContainsKey("validTo"));

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            Guid.NewGuid(), K13World.V(D(1), 360m, 7m, 1m, 80m), default));
        Assert.Equal(404, unknown.Status);
        Assert.Equal("INGREDIENT_NOT_FOUND", unknown.Code);
    }

    [Fact]
    public async Task IngredientVersionRecordsSourceActorAndIncrementsVersion()
    {
        var w = new K13World();
        var actor = w.Actor.UserId;
        var rice = await w.AddIngredient("Rice");
        w.Clock.Set(D(2));

        var first = await w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 360m, 7m, 1m, 80m, source: "Vietnamese FCT", sourceRef: "https://example.org/fct"), default);
        var second = await w.Ingredients.AddVersionAsync(rice, K13World.V(D(5), 370m, 7m, 1m, 81m), default);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(actor, first.RecordedByUserId);
        Assert.Equal("Vietnamese FCT", first.Source);
        Assert.Equal("https://example.org/fct", first.SourceRef);
        Assert.Equal(D(2), first.RecordedAt);
        Assert.Equal(TimeSpan.FromHours(7), first.ValidFrom.Offset);

        var stored = w.IngredientVersions.Single(v => v.Id == first.VersionId);
        Assert.Equal(actor, stored.RecordedByUserId);
        Assert.Equal(360m, stored.EnergyKcalPer100G);
        Assert.Equal(1m, stored.FatGPer100G);
        Assert.Equal(80m, stored.CarbGPer100G);
    }

    // ---- Ingredients: temporal rule (BE1-BR02) -------------------------------------------------------------

    [Fact]
    public async Task OverlapGuardRequiresSupersedeUnlessNewIntervalStartsLater()
    {
        var w = new K13World();
        var rice = await w.AddIngredient("Rice");
        w.Clock.Set(D(1));
        var v1 = await w.Ingredients.AddVersionAsync(rice, K13World.V(D(1), 360m, 7m, 1m, 80m), default);

        // Same start as a live open-ended row, no supersedesId: ambiguous, rejected.
        var sameStart = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 365m, 7m, 1m, 80m), default));
        Assert.Equal(409, sameStart.Status);
        Assert.Equal("INGREDIENT_VERSION_OVERLAP", sameStart.Code);
        var conflicting = (List<Guid>)sameStart.Extensions!["conflictingVersionIds"]!;
        Assert.Equal(v1.VersionId, Assert.Single(conflicting));

        // Back-dated start that overlaps a later-starting live row: also rejected.
        var later = await w.Ingredients.AddVersionAsync(rice, K13World.V(D(8), 380m, 7m, 1m, 82m), default);
        var backdated = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(3), 390m, 7m, 1m, 82m), default));
        Assert.Equal("INGREDIENT_VERSION_OVERLAP", backdated.Code);
        Assert.Equal(later.VersionId, Assert.Single((List<Guid>)backdated.Extensions!["conflictingVersionIds"]!));

        // Starting strictly later than every overlapped row is a normal "from next week" change.
        var next = await w.Ingredients.AddVersionAsync(rice, K13World.V(D(15), 400m, 7m, 1m, 83m), default);
        Assert.Equal(3, next.Version);
    }

    [Fact]
    public async Task SupersedingRowsCorrectHistoryAndAreResolvedAsKnownAt()
    {
        var w = new K13World();
        var rice = await w.AddIngredient("Rice");

        w.Clock.Set(D(1));
        var v1 = await w.Ingredients.AddVersionAsync(rice, K13World.V(D(1), 360m, 7m, 1m, 80m), default);

        // Day 5: v1 was wrong for Jan 1-8. A bounded correction supersedes it, and a new row takes over from Jan 8.
        w.Clock.Set(D(5));
        var v2 = await w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 350m, 7m, 1m, 78m, to: D(8), supersedes: v1.VersionId), default);
        var v3 = await w.Ingredients.AddVersionAsync(rice, K13World.V(D(8), 380m, 7m, 1m, 82m), default);

        async Task<Guid?> Pick(DateTimeOffset validAt, DateTimeOffset knownAt)
        {
            var map = await w.Ingredients.ResolveEffectiveAsync([rice], validAt, knownAt, default);
            return map.TryGetValue(rice, out var v) ? v.Id : null;
        }

        Assert.Equal(v2.VersionId, await Pick(D(3), D(6)));   // corrected value for early January
        Assert.Equal(v1.VersionId, await Pick(D(3), D(4)));   // what the system knew before the correction
        Assert.Equal(v3.VersionId, await Pick(D(10), D(6)));  // later interval takes over
        Assert.Equal(v1.VersionId, await Pick(D(10), D(4)));  // before day 5 only v1 (open-ended) was known
        Assert.Null(await Pick(D(10), D(1) - TimeSpan.FromDays(1))); // nothing was known yet

        // Superseding a row that is already superseded is refused, and so is an unknown id.
        var twice = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 355m, 7m, 1m, 79m, to: D(8), supersedes: v1.VersionId), default));
        Assert.Equal(409, twice.Status);
        Assert.Equal("INGREDIENT_VERSION_ALREADY_SUPERSEDED", twice.Code);

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(20), 355m, 7m, 1m, 79m, supersedes: Guid.NewGuid()), default));
        Assert.Equal(400, unknown.Status);
        Assert.True(K13World.Errors(unknown).ContainsKey("supersedesId"));
    }

    [Fact]
    public void TimelineTieBreaksAreDeterministicForLegacyRows()
    {
        var ingredientId = Guid.NewGuid();
        IngredientVersion Row(int version, DateTimeOffset from, DateTimeOffset recorded) => new()
        {
            IngredientId = ingredientId, Version = version, EffectiveFrom = from, RecordedAt = recorded
        };

        var a = Row(1, D(1), D(1));
        var b = Row(2, D(1), D(2));   // same start, recorded later: wins
        var c = Row(3, D(1), D(2));   // same start and time: highest version wins

        Assert.Equal(c.Id, IngredientTimeline.PickEffective([a, b, c], D(5), D(9))!.Id);
        Assert.Equal(a.Id, IngredientTimeline.PickEffective([a, b, c], D(5), D(1, 12))!.Id);
        Assert.Null(IngredientTimeline.PickEffective([a, b, c], D(1) - TimeSpan.FromHours(1), D(9)));
    }

    [Fact]
    public async Task ConcurrentIngredientVersionIsReportedAsConflict()
    {
        var w = new K13World();
        var rice = await w.AddIngredient("Rice");
        w.FailNextSaveWithDuplicateKey = true;

        var ex = await Assert.ThrowsAsync<KitchenException>(() => w.Ingredients.AddVersionAsync(
            rice, K13World.V(D(1), 360m, 7m, 1m, 80m), default));

        Assert.Equal(409, ex.Status);
        Assert.Equal("INGREDIENT_VERSION_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task IngredientListShowsEffectiveVersionWithSourceAndTheInstantUsed()
    {
        var w = new K13World();
        var rice = await w.AddIngredient("Rice");
        await w.AddIngredient("Pork");
        w.Clock.Set(D(2));
        await w.Ingredients.AddVersionAsync(rice, K13World.V(D(1), 360m, 7m, 1m, 80m, source: "FCT"), default);
        // Valid only from next week: not effective yet.
        await w.Ingredients.AddVersionAsync(rice, K13World.V(D(9), 400m, 7m, 1m, 80m), default);

        var page = await w.Ingredients.ListAsync("ri", 1, 500, default);

        Assert.Equal(1, page.Total);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(D(2), page.ValidAt);
        Assert.Equal(D(2), page.KnownAt);
        var item = Assert.Single(page.Items);
        Assert.Equal(360m, item.Nutrition!.EnergyKcalPer100G);
        Assert.Equal("FCT", item.Nutrition.Source);
        Assert.Equal(1m, item.Nutrition.FatGPer100G);

        var all = await w.Ingredients.ListAsync(null, 1, 25, default);
        Assert.Equal(new[] { "Pork", "Rice" }, all.Items.Select(i => i.Name).ToArray());
        Assert.Null(all.Items[0].Nutrition);
    }

    // ---- Recipes ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateRecipeStoresOriginalQuantitiesBasisRoundingAndActor()
    {
        var w = new K13World();
        var (rice, pork) = await w.RiceAndPork();

        var created = await w.Recipes.CreateAsync(new("Congee", 2, [
            new(rice, 200m, "g"),
            new(pork, 100m, "g", "gross")]), default);

        Assert.Equal(1, created.Version);
        Assert.Null(created.PreviousVersion);
        Assert.Equal(w.Actor.UserId, created.RecordedByUserId);
        Assert.Equal(0, created.RoundingRule!.KcalDecimals);
        Assert.Equal(1, created.RoundingRule.MacroDecimals);

        var version = Assert.Single(w.RecipeVersions);
        Assert.Equal(2, version.ServingBasis);
        Assert.Equal(0, version.RoundingKcalDecimals);
        Assert.Equal(1, version.RoundingMacroDecimals);
        Assert.Equal(w.Actor.UserId, version.RecordedByUserId);

        var riceLine = w.RecipeIngredients.Single(l => w.IngredientVersions.Single(v => v.Id == l.IngredientVersionId).IngredientId == rice);
        Assert.Equal(200m, riceLine.Quantity);
        Assert.Equal("g", riceLine.Unit);
        Assert.Equal("gross", riceLine.WeightBasis);
        Assert.Equal(100m, riceLine.GramsPerPortion);
    }

    [Fact]
    public async Task CreateRecipeValidatesLinesAndMissingNutrientData()
    {
        var w = new K13World();
        var (rice, _) = await w.RiceAndPork();
        var noData = await w.AddIngredient("Carrot");

        var duplicate = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.CreateAsync(
            new("X", 1, [new(rice, 10m, "g"), new(rice, 20m, "g")]), default));
        Assert.True(K13World.Errors(duplicate).ContainsKey("lines"));

        var badUnit = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.CreateAsync(
            new("X", 1, [new(rice, 10m, "ml")]), default));
        Assert.True(K13World.Errors(badUnit).ContainsKey("lines[0].unit"));

        var badBasis = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.CreateAsync(
            new("X", 1, [new(rice, 10m, "g", "net")]), default));
        Assert.True(K13World.Errors(badBasis).ContainsKey("lines[0].weightBasis"));

        var badRounding = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.CreateAsync(
            new("X", 1, [new(rice, 10m, "g")], new RoundingRuleRequest(9, null)), default));
        Assert.True(K13World.Errors(badRounding).ContainsKey("roundingRule.kcalDecimals"));

        var missing = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.CreateAsync(
            new("X", 1, [new(rice, 10m, "g"), new(noData, 10m, "g")]), default));
        Assert.Equal(422, missing.Status);
        Assert.Equal("NUTRIENT_DATA_MISSING", missing.Code);
        var list = (IReadOnlyList<MissingIngredient>)missing.Extensions!["missingIngredients"]!;
        Assert.Equal(noData, Assert.Single(list).IngredientId);
        Assert.Empty(w.RecipeVersions);
    }

    [Fact]
    public async Task MlLinesNeedAConfirmedDensityAndAreNeverTreatedAsGrams()
    {
        var w = new K13World();
        var oilNoDensity = await w.AddIngredient("Oil A", "ml");
        var oil = await w.AddIngredient("Oil B", "ml");
        w.Clock.Set(D(2));
        await w.Ingredients.AddVersionAsync(oilNoDensity, K13World.V(D(1), 884m, 0m, 100m, 0m), default);
        await w.Ingredients.AddVersionAsync(oil, K13World.V(D(1), 884m, 0m, 100m, 0m, density: 0.92m), default);

        var ex = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.CreateAsync(
            new("Fry", 1, [new(oilNoDensity, 10m, "ml")]), default));
        Assert.Equal(422, ex.Status);
        Assert.Equal("UNIT_CONVERSION_MISSING", ex.Code);
        Assert.Empty(w.RecipeVersions);

        var created = await w.Recipes.CreateAsync(new("Fry", 1, [new(oil, 10m, "ml")]), default);
        Assert.Equal(9.2m, w.RecipeIngredients.Single().GramsPerPortion);

        var nutrition = await w.Nutrition.CalculateAsync(created.RecipeId, 1, null, null, default);
        var line = Assert.Single(nutrition.Lines);
        Assert.Equal(0.92m, line.Conversion!.GramsPerMl);
        Assert.Equal(9.2m, line.GramsPerPortion);
        Assert.Equal("ml", line.Input!.Unit);
        Assert.Equal(81m, nutrition.PerServing.Kcal); // 9.2 g x 884 / 100 = 81.328
    }

    [Fact]
    public async Task NewRecipeVersionChecksExpectedVersionAndInheritsRoundingAndLinksPrevious()
    {
        var w = new K13World();
        var (rice, pork) = await w.RiceAndPork();
        var created = await w.Recipes.CreateAsync(new("Congee", 2, [new(rice, 200m, "g")]), default);

        var stale = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.AddVersionAsync(
            created.RecipeId, new(5, 2, [new(rice, 200m, "g")]), default));
        Assert.Equal(409, stale.Status);
        Assert.Equal("RECIPE_VERSION_CONFLICT", stale.Code);
        Assert.Equal(1, (int)stale.Extensions!["currentVersion"]!);

        w.Clock.Set(D(3));
        var next = await w.Recipes.AddVersionAsync(created.RecipeId, new(
            1, 2, [new(rice, 200m, "g"), new(pork, 50m, "g")],
            Reason: "  add pork  ", RoundingRule: new RoundingRuleRequest(2, null)), default);

        Assert.Equal(2, next.Version);
        Assert.Equal(1, next.PreviousVersion);
        Assert.Equal(created.VersionId, next.PreviousVersionId);
        Assert.Equal("add pork", next.Reason);
        Assert.Equal(2, next.RoundingRule!.KcalDecimals);
        Assert.Equal(1, next.RoundingRule.MacroDecimals); // inherited
        Assert.Equal(2, w.RecipeVersions.Count);
        Assert.Equal("add pork", w.RecipeVersions.Single(v => v.Version == 2).Reason);

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.AddVersionAsync(
            Guid.NewGuid(), new(1, 1, [new(rice, 1m, "g")]), default));
        Assert.Equal("RECIPE_NOT_FOUND", unknown.Code);

        w.FailNextSaveWithDuplicateKey = true;
        var raced = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.AddVersionAsync(
            created.RecipeId, new(2, 2, [new(rice, 1m, "g")]), default));
        Assert.Equal("RECIPE_VERSION_CONFLICT", raced.Code);
    }

    // ---- Nutrition --------------------------------------------------------------------------------------------

    [Fact]
    public async Task NutritionPerServingCoversKcalAndAllMacros()
    {
        var w = new K13World();
        var (rice, pork) = await w.RiceAndPork();
        var recipe = await w.Recipes.CreateAsync(new("Congee", 2, [new(rice, 200m, "g"), new(pork, 100m, "g")]), default);

        var result = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, null, null, default);

        Assert.Equal("recompute", result.Mode);
        Assert.Equal(460m, result.PerServing.Kcal);   // rice 100 g x 3.6 + pork 50 g x 2.0
        Assert.Equal(17m, result.PerServing.ProteinG);
        Assert.Equal(6m, result.PerServing.FatG);
        Assert.Equal(80m, result.PerServing.CarbG);
        Assert.Empty(result.IncompleteData!);
        Assert.Equal(2, result.Lines.Count);
        var riceLine = result.Lines.Single(l => l.IngredientId == rice);
        Assert.Equal(100m, riceLine.GramsPerPortion);
        Assert.Equal(360m, riceLine.Contribution.Kcal);
        Assert.Equal("Vietnamese FCT", riceLine.Source);
        Assert.Equal(riceLine.PinnedVersionId, riceLine.NutrientVersionId);
        Assert.False(riceLine.ChangedFromPinned);
        Assert.Equal(1, result.Inputs.RecipeVersion);
        Assert.Equal(2, result.Inputs.ServingBasis);
        Assert.Equal(2, result.Inputs.Lines.Count);
        Assert.Equal(0, result.RoundingRule.KcalDecimals);
        Assert.Equal(1, result.RoundingRule.ProteinDecimals);
        Assert.Equal(1, result.RoundingRule.MacroDecimals);
    }

    [Fact]
    public async Task EdibleRatioIsAppliedOnlyToGrossWeights()
    {
        var w = new K13World();
        var carrot = await w.AddIngredient("Carrot");
        w.Clock.Set(D(2));
        await w.Ingredients.AddVersionAsync(carrot, K13World.V(D(1), 40m, 1m, 0m, 9m, edible: 80m), default);

        var gross = await w.Recipes.CreateAsync(new("Gross", 1, [new(carrot, 100m, "g", "gross")]), default);
        var edible = await w.Recipes.CreateAsync(new("Edible", 1, [new(carrot, 100m, "g", "edible")]), default);

        var g = await w.Nutrition.CalculateAsync(gross.RecipeId, 1, null, null, default);
        var e = await w.Nutrition.CalculateAsync(edible.RecipeId, 1, null, null, default);

        Assert.Equal(32m, g.PerServing.Kcal);                       // 100 g x 0.8 x 40 / 100
        Assert.Equal(0.8m, g.Lines[0].Conversion!.EdibleFractionApplied);
        Assert.Equal(80m, g.Lines[0].Conversion!.EdibleGramsPerPortion);

        Assert.Equal(40m, e.PerServing.Kcal);                       // ratio is not applied a second time
        Assert.Null(e.Lines[0].Conversion!.EdibleFractionApplied);
        Assert.Equal(100m, e.Lines[0].Conversion!.EdibleGramsPerPortion);
        Assert.Equal("edible", e.Lines[0].Input!.WeightBasis);
    }

    [Fact]
    public async Task RoundingHappensOnceOnTheTotalNotPerLine()
    {
        var w = new K13World();
        var a = await w.AddIngredient("A");
        var b = await w.AddIngredient("B");
        w.Clock.Set(D(2));
        await w.Ingredients.AddVersionAsync(a, K13World.V(D(1), 40m, 0m, 0m, 10m), default);
        await w.Ingredients.AddVersionAsync(b, K13World.V(D(1), 40m, 0m, 0m, 10m), default);
        var recipe = await w.Recipes.CreateAsync(new("Tiny", 1, [new(a, 1m, "g"), new(b, 1m, "g")]), default);

        var result = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, null, null, default);

        Assert.Equal(0.4m, result.Lines[0].Contribution.Kcal);
        Assert.Equal(0.4m, result.Lines[1].Contribution.Kcal);
        Assert.Equal(1m, result.PerServing.Kcal); // 0.8 rounded once; rounding each line would give 0
        Assert.Equal(0.2m, result.PerServing.CarbG);
    }

    [Fact]
    public async Task PinnedAndRecomputeAreDistinguishedAndReproducible()
    {
        var w = new K13World();
        var (rice, pork) = await w.RiceAndPork();                       // recorded day 2, kcal rice 360
        var recipe = await w.Recipes.CreateAsync(new("Congee", 2, [new(rice, 200m, "g")]), default);

        w.Clock.Set(D(10));
        await w.Ingredients.AddVersionAsync(rice, K13World.V(D(8), 380m, 7m, 1m, 82m), default); // valid from day 8

        var recompute = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, new NutritionQuery(null, D(12), D(12), null), default);
        var oldKnowledge = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, new NutritionQuery("recompute", D(12), D(9), null), default);
        var beforeChange = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, new NutritionQuery("recompute", D(5), D(12), null), default);
        var pinned = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, new NutritionQuery("pinned", null, null, null), default);

        Assert.Equal(380m, recompute.PerServing.Kcal);
        Assert.Equal(360m, recompute.PinnedPerServing!.Kcal);
        Assert.Equal(20m, recompute.DeltaFromPinned!.Kcal);
        Assert.True(recompute.Lines[0].ChangedFromPinned);
        Assert.NotNull(recompute.Inputs.ValidAt);

        Assert.Equal(360m, oldKnowledge.PerServing.Kcal);   // knownAt before the correction was recorded
        Assert.Equal(360m, beforeChange.PerServing.Kcal);   // validAt before the change takes effect
        Assert.Equal(0m, beforeChange.DeltaFromPinned!.Kcal);
        Assert.False(beforeChange.Lines[0].ChangedFromPinned);

        Assert.Equal("pinned", pinned.Mode);
        Assert.Equal(360m, pinned.PerServing.Kcal);
        Assert.Null(pinned.PinnedPerServing);
        Assert.Null(pinned.DeltaFromPinned);
        Assert.Null(pinned.Inputs.ValidAt);
        Assert.Null(pinned.Inputs.KnownAt);
        Assert.Equal(pinned.Lines[0].PinnedVersionId, pinned.Lines[0].NutrientVersionId);

        var withTime = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(
            recipe.RecipeId, 1, new NutritionQuery("pinned", D(12), null, null), default));
        Assert.True(K13World.Errors(withTime).ContainsKey("mode"));

        var badMode = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(
            recipe.RecipeId, 1, new NutritionQuery("latest", null, null, null), default));
        Assert.Equal("VALIDATION_ERROR", badMode.Code);
    }

    [Fact]
    public async Task RecipeVersionIsInvisibleBeforeItWasRecorded()
    {
        var w = new K13World();
        var (rice, _) = await w.RiceAndPork();                    // clock is day 2
        var recipe = await w.Recipes.CreateAsync(new("Congee", 1, [new(rice, 100m, "g")]), default);

        var early = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(
            recipe.RecipeId, 1, new NutritionQuery(null, D(5), D(1, 12), null), default));
        Assert.Equal(404, early.Status);
        Assert.Equal("RECIPE_VERSION_NOT_FOUND", early.Code);

        var ok = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, new NutritionQuery(null, D(5), D(2, 12), null), default);
        Assert.Equal(360m, ok.PerServing.Kcal);

        var noVersion = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(recipe.RecipeId, 9, null, null, default));
        Assert.Equal("RECIPE_VERSION_NOT_FOUND", noVersion.Code);
        var noRecipe = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(Guid.NewGuid(), 1, null, null, default));
        Assert.Equal("RECIPE_NOT_FOUND", noRecipe.Code);
    }

    [Fact]
    public async Task MissingNutrientDataAtValidAtIsAnErrorNeverZero()
    {
        var w = new K13World();
        var (rice, _) = await w.RiceAndPork();
        var recipe = await w.Recipes.CreateAsync(new("Congee", 1, [new(rice, 100m, "g")]), default);

        // Rice data is valid from day 1; a validAt before that has no data.
        var ex = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(
            recipe.RecipeId, 1, new NutritionQuery(null, D(1) - TimeSpan.FromDays(10), D(3), null), default));

        Assert.Equal(422, ex.Status);
        Assert.Equal("NUTRIENT_DATA_MISSING", ex.Code);
        Assert.Equal(rice, Assert.Single((IReadOnlyList<MissingIngredient>)ex.Extensions!["missingIngredients"]!).IngredientId);
    }

    [Fact]
    public async Task RowsRecordedBeforeV13GiveUnknownMacrosNotZeros()
    {
        var w = new K13World();
        var ingredient = new Ingredient { Name = "Legacy", Unit = "g" };
        var version = new IngredientVersion
        {
            IngredientId = ingredient.Id, Version = 1, EnergyKcalPer100G = 200m, ProteinGPer100G = 10m,
            EdibleFraction = 1m, EffectiveFrom = D(1), RecordedAt = D(1)
        };
        var recipe = new Recipe { Name = "Old" };
        var recipeVersion = new RecipeVersion { RecipeId = recipe.Id, Version = 1, RecordedAt = D(2) };
        var line = new RecipeIngredient
        {
            RecipeVersionId = recipeVersion.Id, IngredientVersionId = version.Id, GramsPerPortion = 50m
        };
        w.Ingredient_.Add(ingredient);
        w.IngredientVersions.Add(version);
        w.Recipe_.Add(recipe);
        w.RecipeVersions.Add(recipeVersion);
        w.RecipeIngredients.Add(line);
        w.Clock.Set(D(3));

        var result = await w.Nutrition.CalculateAsync(recipe.Id, 1, null, null, default);

        Assert.Equal(100m, result.PerServing.Kcal);   // 50 g as stored x 200 / 100
        Assert.Equal(5m, result.PerServing.ProteinG);
        Assert.Null(result.PerServing.FatG);
        Assert.Null(result.PerServing.CarbG);
        var incomplete = Assert.Single(result.IncompleteData!);
        Assert.Equal(new[] { "fatG", "carbG" }, incomplete.MissingFields.ToArray());
        Assert.Null(result.Lines[0].Source);
        Assert.Equal(1, result.Inputs.ServingBasis);
        Assert.Equal(0, result.RoundingRule.KcalDecimals);  // default rule for versions without a stored one
        Assert.Equal(1, result.RoundingRule.ProteinDecimals);
    }

    [Fact]
    public async Task AgeGroupComparisonUsesRecordedRangesWithTheirSource()
    {
        var w = new K13World();
        var (rice, pork) = await w.RiceAndPork();
        var recipe = await w.Recipes.CreateAsync(new("Congee", 2, [new(rice, 200m, "g"), new(pork, 100m, "g")]), default);
        w.Ranges.Add(new NutrientReferenceRange
        {
            AgeGroup = "Toddler", Nutrient = "kcal", MinValue = 500m, MaxValue = 700m, Source = "Council table 2026",
            SourceRef = "p.12", EffectiveFrom = D(1), RecordedAt = D(1)
        });
        w.Ranges.Add(new NutrientReferenceRange
        {
            AgeGroup = "Toddler", Nutrient = "proteinG", MinValue = 10m, MaxValue = 15m, Source = "Council table 2026",
            EffectiveFrom = D(1), RecordedAt = D(1)
        });
        w.Ranges.Add(new NutrientReferenceRange
        {
            AgeGroup = "Toddler", Nutrient = "fatG", MinValue = 5m, MaxValue = 10m, Source = "Council table 2026",
            EffectiveFrom = D(1), RecordedAt = D(1)
        });

        var result = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, new NutritionQuery(null, null, null, "toddler"), default);

        var comparison = result.AgeGroupComparison!;
        Assert.Equal("Toddler", comparison.AgeGroup);
        Assert.Equal(3, comparison.Items.Count);
        var kcal = comparison.Items.Single(i => i.Nutrient == "kcal");
        Assert.Equal("below", kcal.Status);       // 460 < 500
        Assert.Equal("Council table 2026", kcal.Source);
        Assert.Equal("p.12", kcal.SourceRef);
        Assert.Equal("above", comparison.Items.Single(i => i.Nutrient == "proteinG").Status);  // 17 > 15
        Assert.Equal("within", comparison.Items.Single(i => i.Nutrient == "fatG").Status);     // 6

        var missing = await Assert.ThrowsAsync<KitchenException>(() => w.Nutrition.CalculateAsync(
            recipe.RecipeId, 1, new NutritionQuery(null, null, null, "teen"), default));
        Assert.Equal(422, missing.Status);
        Assert.Equal("AGE_GROUP_REFERENCE_MISSING", missing.Code);

        var plain = await w.Nutrition.CalculateAsync(recipe.RecipeId, 1, null, null, default);
        Assert.Null(plain.AgeGroupComparison);   // no ageGroup: no comparison, no invented ranges
    }
}

// ---- In-memory world: the real services over fake repositories --------------------------------------------

internal sealed class K13Clock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public void Set(DateTimeOffset value) => _now = value;
    public override DateTimeOffset GetUtcNow() => _now;
}

internal sealed class K13Actor : ICurrentActor
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public bool IsInRole(string role) => true;
}

internal sealed class K13World
{
    public readonly K13Clock Clock = new(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
    public readonly K13Actor Actor = new();
    public bool FailNextSaveWithDuplicateKey;

    public readonly List<Ingredient> Ingredient_ = [];
    public readonly List<IngredientVersion> IngredientVersions = [];
    public readonly List<Recipe> Recipe_ = [];
    public readonly List<RecipeVersion> RecipeVersions = [];
    public readonly List<RecipeIngredient> RecipeIngredients = [];
    public readonly List<NutrientReferenceRange> Ranges = [];

    private readonly List<object> _pending = [];

    public readonly IngredientService Ingredients;
    public readonly RecipeService Recipes;
    public readonly NutritionService Nutrition;

    public K13World()
    {
        var uow = new Uow(this);
        var ingredientService = new IngredientService(new IngredientRepo(this), Clock, uow, Actor);
        Ingredients = ingredientService;
        Recipes = new RecipeService(new RecipeRepo(this), ingredientService, Clock, uow, Actor);
        Nutrition = new NutritionService(new NutritionRepo(this), ingredientService, Clock);
    }

    public static Dictionary<string, string[]> Errors(KitchenException ex) =>
        (Dictionary<string, string[]>)ex.Extensions!["errors"]!;

    public static CreateIngredientVersionRequest V(
        DateTimeOffset from, decimal kcal, decimal protein, decimal fat, decimal carb, decimal edible = 100m,
        string source = "Vietnamese FCT", string? sourceRef = null, DateTimeOffset? to = null,
        Guid? supersedes = null, decimal? density = null) =>
        new(from, edible, kcal, protein, null, fat, carb, to, density, source, sourceRef, supersedes);

    public async Task<Guid> AddIngredient(string name, string unit = "g") =>
        (await Ingredients.CreateAsync(new CreateIngredientRequest(name, unit), default)).Id;

    /// <summary>Rice (360 kcal, P7 F1 C80) and pork (200 kcal, P20 F10 C0), both valid from day 1, recorded on day 2.</summary>
    public async Task<(Guid Rice, Guid Pork)> RiceAndPork()
    {
        var rice = await AddIngredient("Rice");
        var pork = await AddIngredient("Pork");
        Clock.Set(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var d1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await Ingredients.AddVersionAsync(rice, V(d1, 360m, 7m, 1m, 80m), default);
        await Ingredients.AddVersionAsync(pork, V(d1, 200m, 20m, 10m, 0m), default);
        return (rice, pork);
    }

    private sealed class Uow(K13World w) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (w.FailNextSaveWithDuplicateKey)
            {
                w.FailNextSaveWithDuplicateKey = false;
                w._pending.Clear();
                throw new PersistenceConflictException(PersistenceConflictKind.DuplicateKey, new InvalidOperationException("unique"));
            }

            var count = w._pending.Count;
            foreach (var item in w._pending)
            {
                switch (item)
                {
                    case Ingredient x: w.Ingredient_.Add(x); break;
                    case IngredientVersion x: w.IngredientVersions.Add(x); break;
                    case Recipe x: w.Recipe_.Add(x); break;
                    case RecipeVersion x: w.RecipeVersions.Add(x); break;
                    case RecipeIngredient x: w.RecipeIngredients.Add(x); break;
                }
            }

            w._pending.Clear();
            return Task.FromResult(count);
        }

        public Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class IngredientRepo(K13World w) : IIngredientRepository
    {
        private IEnumerable<Ingredient> Filter(string? search) =>
            string.IsNullOrWhiteSpace(search)
                ? w.Ingredient_
                : w.Ingredient_.Where(i => i.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

        public Task<int> CountIngredientsAsync(string? search, CancellationToken ct) => Task.FromResult(Filter(search).Count());

        public Task<List<Ingredient>> SearchIngredientsAsync(string? search, int skip, int size, CancellationToken ct) =>
            Task.FromResult(Filter(search).OrderBy(i => i.Name).ThenBy(i => i.Id).Skip(skip).Take(size).ToList());

        public Task<bool> IngredientExistsAsync(Guid ingredientId, CancellationToken ct) =>
            Task.FromResult(w.Ingredient_.Any(i => i.Id == ingredientId));

        public Task<bool> IngredientNameExistsAsync(string name, CancellationToken ct) =>
            Task.FromResult(w.Ingredient_.Any(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<List<IngredientVersion>> ListVersionsAsync(Guid[] ids, DateTimeOffset? knownUtc, CancellationToken ct) =>
            Task.FromResult(w.IngredientVersions
                .Where(v => ids.Contains(v.IngredientId) && (knownUtc is null || v.RecordedAt <= knownUtc))
                .ToList());

        public void AddIngredient(Ingredient value) => w._pending.Add(value);
        public void AddIngredientVersion(IngredientVersion value) => w._pending.Add(value);
    }

    private sealed class RecipeRepo(K13World w) : IRecipeRepository
    {
        public Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct) =>
            Task.FromResult(w.Recipe_.Any(r => r.Id == recipeId));

        public Task<RecipeVersion?> FindLatestVersionAsync(Guid recipeId, CancellationToken ct) =>
            Task.FromResult(w.RecipeVersions.Where(v => v.RecipeId == recipeId).OrderByDescending(v => v.Version).FirstOrDefault());

        public Task<Dictionary<Guid, Ingredient>> GetIngredientsByIdAsync(Guid[] ids, CancellationToken ct) =>
            Task.FromResult(w.Ingredient_.Where(i => ids.Contains(i.Id)).ToDictionary(i => i.Id));

        public void AddRecipeIngredients(IEnumerable<RecipeIngredient> value) => w._pending.AddRange(value);
        public void AddRecipe(Recipe value) => w._pending.Add(value);
        public void AddRecipeVersion(RecipeVersion value) => w._pending.Add(value);
    }

    private sealed class NutritionRepo(K13World w) : INutritionRepository
    {
        public Task<RecipeVersion?> FindRecipeVersionAsync(Guid recipeId, int version, CancellationToken ct) =>
            Task.FromResult(w.RecipeVersions.SingleOrDefault(v => v.RecipeId == recipeId && v.Version == version));

        public Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken ct) =>
            Task.FromResult(w.Recipe_.Any(r => r.Id == recipeId));

        public Task<List<RecipeNutritionLineRow>> ListRecipeNutritionLinesAsync(RecipeVersion recipeVersion, CancellationToken ct)
        {
            var rows = (
                from line in w.RecipeIngredients
                where line.RecipeVersionId == recipeVersion.Id
                join pinned in w.IngredientVersions on line.IngredientVersionId equals pinned.Id
                join ingredient in w.Ingredient_ on pinned.IngredientId equals ingredient.Id
                orderby ingredient.Name, ingredient.Id
                select new RecipeNutritionLineRow
                {
                    IngredientId = ingredient.Id,
                    IngredientName = ingredient.Name,
                    PinnedVersionId = pinned.Id,
                    GramsPerPortion = line.GramsPerPortion,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    WeightBasis = line.WeightBasis,
                    Pinned = pinned
                }).ToList();
            return Task.FromResult(rows);
        }

        public Task<List<NutrientReferenceRange>> ListReferenceRangesAsync(string ageGroup, DateTimeOffset knownUtc, CancellationToken ct) =>
            Task.FromResult(w.Ranges
                .Where(r => string.Equals(r.AgeGroup, ageGroup, StringComparison.OrdinalIgnoreCase) && r.RecordedAt <= knownUtc)
                .ToList());
    }
}

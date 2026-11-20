using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Features.Kitchen;
using MealTrace.Domain.Entities;

namespace MealTrace.Api.Tests;

/// <summary>
/// BE1 Phase 2 and 3 logic (suppliers, dated source entries, versioned menus, frozen publication nutrition, parent
/// view, ingredient estimates, reference ranges, recipe catalogue). The real services run over in-memory repositories,
/// so these need neither PostgreSQL nor SQLite; persistence is covered by the PostgreSQL suites.
/// </summary>
public sealed class KitchenBe1Tests
{
    private static DateOnly Day(int d) => new(2026, 1, d);
    private static DateTimeOffset D(int day, int hour = 0) => new(2026, 1, day, hour, 0, 0, TimeSpan.Zero);
    private static Dictionary<string, string[]> Err(KitchenException ex) => K13World.Errors(ex);

    /// <summary>Rice + pork, and "Congee" (rice 200 g + pork 100 g for 2 portions): 460 kcal, P17 F6 C80 per portion.</summary>
    private static async Task<(Guid Rice, Guid Pork, Guid RecipeId, Guid VersionId)> Congee(K13World w)
    {
        var (rice, pork) = await w.RiceAndPork();
        var created = await w.Recipes.CreateAsync(new("Congee", 2, [new(rice, 200m, "g"), new(pork, 100m, "g")]), default);
        return (rice, pork, created.RecipeId, created.VersionId!.Value);
    }

    /// <summary>A recipe version as written before v1.3: no quantity/unit on the line, and maybe no fat/carbohydrate.</summary>
    private static Guid LegacyRecipe(K13World w, string name, string unit, decimal? fat, decimal? carb)
    {
        var ingredient = new Ingredient { Name = name, Unit = unit };
        var version = new IngredientVersion
        {
            IngredientId = ingredient.Id, Version = 1, EnergyKcalPer100G = 100m, ProteinGPer100G = 5m, EdibleFraction = 1m,
            EffectiveFrom = D(1), RecordedAt = D(1), FatGPer100G = fat, CarbGPer100G = carb
        };
        var recipe = new Recipe { Name = name + " recipe" };
        var recipeVersion = new RecipeVersion { RecipeId = recipe.Id, Version = 1, RecordedAt = D(1) };
        var line = new RecipeIngredient { RecipeVersionId = recipeVersion.Id, IngredientVersionId = version.Id, GramsPerPortion = 100m };
        w.Ingredient_.Add(ingredient);
        w.IngredientVersions.Add(version);
        w.Recipe_.Add(recipe);
        w.RecipeVersions.Add(recipeVersion);
        w.RecipeIngredients.Add(line);
        return recipeVersion.Id;
    }

    private static CreateNutritionReferenceRequest Ref(
        string nutrient = "kcal", decimal? min = 400m, decimal? max = 600m, string basis = "perMeal", string? mealType = "Lunch",
        Guid? supersedes = null, string ageGroup = "Toddler", int from = 1) =>
        new(ageGroup, nutrient, min, max, basis, mealType, "Council table 2026", "p.3", D(from), null, supersedes, "initial");

    // ---- P2-H01: suppliers and dated source entries -----------------------------------------------------------

    [Fact]
    public async Task SupplierCorrectionsKeepEveryRevision()
    {
        var w = new K13World();

        var blank = await Assert.ThrowsAsync<KitchenException>(() => w.SupplierSvc.CreateAsync(new("  ", null, null), default));
        Assert.True(Err(blank).ContainsKey("name"));

        var created = await w.SupplierSvc.CreateAsync(new("Fresh Farm", "0900 000 000", "SUP-1"), default);
        Assert.Equal(1, created.Version);
        Assert.Equal(w.Actor.UserId, created.RecordedBy);

        var stale = await Assert.ThrowsAsync<KitchenException>(() => w.SupplierSvc.AddVersionAsync(
            created.Id, new(5, "typo", "Fresh Farm Co", null, null), default));
        Assert.Equal(409, stale.Status);
        Assert.Equal("SUPPLIER_VERSION_CONFLICT", stale.Code);
        Assert.Equal(1, (int)stale.Extensions!["currentVersion"]!);

        var noReason = await Assert.ThrowsAsync<KitchenException>(() => w.SupplierSvc.AddVersionAsync(
            created.Id, new(1, null, "Fresh Farm Co", null, null), default));
        Assert.True(Err(noReason).ContainsKey("reason"));

        var second = await w.SupplierSvc.AddVersionAsync(created.Id, new(1, "renamed", "Fresh Farm Co", "x", "SUP-1"), default);
        Assert.Equal(2, second.Version);
        Assert.Equal(created.VersionId, second.PriorVersionId);

        var history = await w.SupplierSvc.HistoryAsync(created.Id, default);
        Assert.Equal(new[] { "Fresh Farm", "Fresh Farm Co" }, history.Items.Select(i => i.Name).ToArray());
        Assert.Equal("renamed", history.Items[1].Reason);

        var page = await w.SupplierSvc.ListAsync("farm", 1, 25, default);
        var view = Assert.Single(page.Items);
        Assert.Equal("Fresh Farm Co", view.Name);
        Assert.Equal(2, view.Version);

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.SupplierSvc.HistoryAsync(Guid.NewGuid(), default));
        Assert.Equal("SUPPLIER_NOT_FOUND", unknown.Code);
    }

    [Fact]
    public async Task OneIngredientCanHaveTwoSourcesAndCorrectionsKeepHistory()
    {
        var w = new K13World();
        var (rice, _) = await w.RiceAndPork();
        var farmA = await w.SupplierSvc.CreateAsync(new("Farm A", null, null), default);
        var farmB = await w.SupplierSvc.CreateAsync(new("Farm B", null, null), default);

        var a = await w.SourceSvc.CreateAsync(new(rice, farmA.VersionId, Day(5), 600m, "g", "DN-1", "LOT-1"), default);
        await w.SourceSvc.CreateAsync(new(rice, farmB.VersionId, Day(5), 400m, "g", null, null), default);

        var page = await w.SourceSvc.ListAsync(rice, Day(5), Day(5), 1, 25, default);
        Assert.Equal(2, page.Total);
        Assert.Equal(1000m, page.Items.Sum(i => i.Quantity));
        Assert.Equal(new[] { "Farm A", "Farm B" }, page.Items.Select(i => i.SupplierName).OrderBy(n => n).ToArray());

        // Renaming the supplier later must not rewrite what was recorded.
        await w.SupplierSvc.AddVersionAsync(farmA.Id, new(1, "renamed", "Farm A Co", null, null), default);
        var firstHistory = await w.SourceSvc.HistoryAsync(a.Id, default);
        Assert.Equal("Farm A", firstHistory.Items[0].SupplierName);

        var corrected = await w.SourceSvc.AddVersionAsync(
            a.Id, new(1, "weighed again", farmA.VersionId, Day(5), 580m, "g", "DN-1", "LOT-1"), default);
        Assert.Equal(2, corrected.Version);
        Assert.Equal(a.VersionId, corrected.PriorVersionId);

        var history = await w.SourceSvc.HistoryAsync(a.Id, default);
        Assert.Equal(new[] { 600m, 580m }, history.Items.Select(i => i.Quantity).ToArray());
        Assert.Equal("weighed again", history.Items[1].Reason);
        var latest = await w.SourceSvc.ListAsync(rice, null, null, 1, 25, default);
        Assert.Equal(980m, latest.Items.Sum(i => i.Quantity));

        var stale = await Assert.ThrowsAsync<KitchenException>(() => w.SourceSvc.AddVersionAsync(
            a.Id, new(1, "again", farmA.VersionId, Day(5), 1m, "g", null, null), default));
        Assert.Equal("SOURCE_ENTRY_VERSION_CONFLICT", stale.Code);
        Assert.Equal(2, (int)stale.Extensions!["currentVersion"]!);
    }

    [Fact]
    public async Task SourceEntriesRejectBadQuantityUnitAndUnknownReferences()
    {
        var w = new K13World();
        var (rice, _) = await w.RiceAndPork();
        var farm = await w.SupplierSvc.CreateAsync(new("Farm", null, null), default);

        var negative = await Assert.ThrowsAsync<KitchenException>(() => w.SourceSvc.CreateAsync(
            new(rice, farm.VersionId, Day(5), -1m, "g", null, null), default));
        Assert.True(Err(negative).ContainsKey("quantity"));

        var wrongUnit = await Assert.ThrowsAsync<KitchenException>(() => w.SourceSvc.CreateAsync(
            new(rice, farm.VersionId, Day(5), 10m, "ml", null, null), default));
        Assert.True(Err(wrongUnit).ContainsKey("unit"));

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.SourceSvc.CreateAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), null, 10m, "g", null, null), default));
        var errors = Err(unknown);
        Assert.True(errors.ContainsKey("referenceDate"));

        var unknownRefs = await Assert.ThrowsAsync<KitchenException>(() => w.SourceSvc.CreateAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), Day(5), 10m, "g", null, null), default));
        Assert.True(Err(unknownRefs).ContainsKey("ingredientId"));
        Assert.True(Err(unknownRefs).ContainsKey("supplierVersionId"));
        Assert.Empty(w.SourceEntries);
    }

    // ---- P2-H02: menu lifecycle ------------------------------------------------------------------------------

    [Fact]
    public async Task MenuCreationRejectsMissingCancelledDuplicateAndUnknownDishes()
    {
        var w = new K13World();
        var (_, _, _, recipeVersion) = await Congee(w);
        var day = w.AddMealDay(Day(6));

        var noDay = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.CreateAsync(
            new(Guid.NewGuid(), [new(recipeVersion, null)], null), default));
        Assert.Equal("MEAL_DAY_NOT_FOUND", noDay.Code);

        var cancelled = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.CreateAsync(
            new(w.AddMealDay(Day(7), "Lunch", true), [new(recipeVersion, null)], null), default));
        Assert.Equal(409, cancelled.Status);
        Assert.Equal("MEAL_DAY_CANCELLED", cancelled.Code);

        var unknownDish = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.CreateAsync(
            new(day, [new(Guid.NewGuid(), null)], null), default));
        Assert.True(Err(unknownDish).ContainsKey("dishes[0].recipeVersionId"));

        var twice = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.CreateAsync(
            new(day, [new(recipeVersion, null), new(recipeVersion, 5)], null), default));
        Assert.True(Err(twice).ContainsKey("dishes"));

        var none = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.CreateAsync(new(day, [], null), default));
        Assert.True(Err(none).ContainsKey("dishes"));
        Assert.Empty(w.MenuRows);

        var created = await w.MenuSvc.CreateAsync(new(day, [new(recipeVersion, 20)], "first draft"), default);
        Assert.Equal("draft", created.State);

        var duplicate = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.CreateAsync(
            new(day, [new(recipeVersion, null)], null), default));
        Assert.Equal("MENU_ALREADY_EXISTS", duplicate.Code);
        Assert.Equal(created.Id, (Guid)duplicate.Extensions!["menuId"]!);
    }

    [Fact]
    public async Task MenuVersionsFollowDraftSubmitReviewPublishAndKeepRejectedHistory()
    {
        var w = new K13World();
        var (_, _, _, recipeVersion) = await Congee(w);
        var day = w.AddMealDay(Day(8));
        var created = await w.MenuSvc.CreateAsync(new(day, [new(recipeVersion, 20)], null), default);

        var stale = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.SubmitAsync(created.Id, new(2), default));
        Assert.Equal("MENU_VERSION_CONFLICT", stale.Code);
        Assert.Equal(1, (int)stale.Extensions!["currentVersion"]!);

        var notYet = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.ReviewAsync(created.Id, new(1, "approve", null), default));
        Assert.Equal("MENU_STATE_INVALID", notYet.Code);

        var submitted = await w.MenuSvc.SubmitAsync(created.Id, new(1), default);
        Assert.Equal("submitted", submitted.State);

        var replaceWhileReview = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.AddVersionAsync(
            created.Id, new(1, [new(recipeVersion, 10)], "change"), default));
        Assert.Equal("MENU_STATE_INVALID", replaceWhileReview.Code);

        var badDecision = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.ReviewAsync(created.Id, new(1, "maybe", null), default));
        Assert.True(Err(badDecision).ContainsKey("decision"));
        var noReason = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.ReviewAsync(created.Id, new(1, "reject", null), default));
        Assert.True(Err(noReason).ContainsKey("reason"));

        var rejected = await w.MenuSvc.ReviewAsync(created.Id, new(1, "reject", "too much rice"), default);
        Assert.Equal("rejected", rejected.State);

        var publishRejected = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.PublishAsync(created.Id, new(1), default));
        Assert.Equal("MENU_STATE_INVALID", publishRejected.Code);

        var noVersionReason = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.AddVersionAsync(
            created.Id, new(1, [new(recipeVersion, 20)], null), default));
        Assert.True(Err(noVersionReason).ContainsKey("reason"));

        var second = await w.MenuSvc.AddVersionAsync(created.Id, new(1, [new(recipeVersion, 25)], "less rice"), default);
        Assert.Equal(2, second.Version);
        Assert.Equal(created.VersionId, second.PriorVersionId);
        Assert.Equal("draft", second.State);

        var oldVersionSubmit = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.SubmitAsync(created.Id, new(1), default));
        Assert.Equal("MENU_VERSION_CONFLICT", oldVersionSubmit.Code);

        await w.MenuSvc.SubmitAsync(created.Id, new(2), default);
        await w.MenuSvc.ReviewAsync(created.Id, new(2, "approve", null), default);
        var published = await w.MenuSvc.PublishAsync(created.Id, new(2), default);
        Assert.Equal("published", published.State);
        Assert.NotNull(published.NutritionSnapshotId);

        // A correction after publication is a new draft version; the published one stays what parents see.
        var third = await w.MenuSvc.AddVersionAsync(created.Id, new(2, [new(recipeVersion, 30)], "headcount"), default);
        Assert.Equal(3, third.Version);

        var view = await w.MenuSvc.GetAsync(created.Id, null, default);
        Assert.Equal(3, view.Latest.Version);
        Assert.Equal("draft", view.Latest.State);
        Assert.Equal(2, view.PublishedVersion);

        var history = await w.MenuSvc.HistoryAsync(created.Id, default);
        Assert.Equal(new[] { "rejected", "published", "draft" }, history.Items.Select(i => i.State).ToArray());
        Assert.Equal(new[] { "draft", "submitted", "rejected" }, history.Items[0].Transitions.Select(t => t.State).ToArray());
        Assert.Equal("too much rice", history.Items[0].Transitions[2].Reason);
        Assert.Equal(25, Assert.Single(history.Items[1].Dishes).PlannedPortions);
        Assert.Equal(20, Assert.Single(history.Items[0].Dishes).PlannedPortions);

        var listed = await w.MenuSvc.ListAsync(Day(1), Day(31), 1, 25, default);
        Assert.Equal(1, listed.Total);
    }

    [Fact]
    public async Task ConcurrentPublishIsAConflictAndLeavesTheMenuApproved()
    {
        var w = new K13World();
        var (_, _, _, recipeVersion) = await Congee(w);
        var day = w.AddMealDay(Day(9));
        var created = await w.MenuSvc.CreateAsync(new(day, [new(recipeVersion, null)], null), default);
        await w.MenuSvc.SubmitAsync(created.Id, new(1), default);
        await w.MenuSvc.ReviewAsync(created.Id, new(1, "approve", null), default);

        w.FailNextSaveWithDuplicateKey = true;
        var ex = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.PublishAsync(created.Id, new(1), default));
        Assert.Equal(409, ex.Status);
        Assert.Equal("MENU_VERSION_CONFLICT", ex.Code);
        Assert.Empty(w.Snapshots);
        Assert.Equal("approved", (await w.MenuSvc.GetAsync(created.Id, null, default)).Latest.State);

        await w.MenuSvc.PublishAsync(created.Id, new(1), default);
        Assert.Single(w.Snapshots);
    }

    // ---- P2-H03: frozen publication nutrition and the parent view -----------------------------------------------

    [Fact]
    public async Task PublicationFreezesNutritionAndLaterChangesOnlyShowInAnExplicitRecompute()
    {
        var w = new K13World();
        var (rice, _, recipeId, v1) = await Congee(w);

        // Recipe version 2 uses less rice; a menu that picked v1 must keep getting v1.
        await w.Recipes.AddVersionAsync(recipeId, new(1, 2, [new(rice, 100m, "g")], Reason: "less rice"), default);
        var menu = await w.PublishedMenu(v1, Day(10));

        var frozen = await w.MenuSvc.GetNutritionAsync(menu.MenuId, null, null, null, null, null, default);
        Assert.Equal("pinned", frozen.Mode);
        Assert.Equal(460m, frozen.Payload.Total.Kcal);
        Assert.Equal(17m, frozen.Payload.Total.ProteinG);
        Assert.Equal(6m, frozen.Payload.Total.FatG);
        Assert.Equal(80m, frozen.Payload.Total.CarbG);
        Assert.Equal(v1, Assert.Single(frozen.Payload.Dishes).RecipeVersionId);
        Assert.Equal(MenuService.AlgorithmVersion, frozen.Payload.AlgorithmVersion);
        Assert.Equal("not_requested", frozen.Payload.AgeComparisonStatus);
        Assert.Equal(64, frozen.PayloadHash!.Length);

        // The rice nutrient data is corrected after publication.
        w.Clock.Set(D(12));
        await w.Ingredients.AddVersionAsync(rice, K13World.V(D(11), 380m, 7m, 1m, 82m), default);

        var again = await w.MenuSvc.GetNutritionAsync(menu.MenuId, null, null, null, null, null, default);
        Assert.Equal(frozen.PayloadHash, again.PayloadHash);
        Assert.Equal(460m, again.Payload.Total.Kcal);

        var recompute = await w.MenuSvc.GetNutritionAsync(menu.MenuId, null, "recompute", null, D(13), D(13), default);
        Assert.Equal("recompute", recompute.Mode);
        Assert.Equal(480m, recompute.Payload.Total.Kcal);            // rice 100 g x 3.8 + pork 50 g x 2
        Assert.Equal(460m, recompute.AsPublished!.Total.Kcal);
        Assert.Equal(20m, recompute.DeltaFromPublished!.Kcal);
        Assert.Equal(2, recompute.ChangedNutrientVersionIds!.Count);  // the old and the new rice version
        Assert.Single(w.Snapshots);                                   // a recompute never writes

        var parent = await w.MenuSvc.ListForParentAsync(Guid.NewGuid(), Day(1), Day(31), default);
        Assert.Equal(460m, Assert.Single(parent.Items).Nutrition!.Kcal);

        var pinnedWithTime = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.GetNutritionAsync(
            menu.MenuId, null, "pinned", null, D(13), null, default));
        Assert.True(Err(pinnedWithTime).ContainsKey("mode"));
    }

    [Fact]
    public async Task MenuTotalIsTheRoundedSumOfDishesAndAnUnpublishedMenuHasNoNutrition()
    {
        var w = new K13World();
        var (_, pork, _, congee) = await Congee(w);
        var soup = (await w.Recipes.CreateAsync(new("Soup", 1, [new(pork, 100m, "g")]), default)).VersionId!.Value;

        var day = w.AddMealDay(Day(11));
        var created = await w.MenuSvc.CreateAsync(new(day, [new(congee, null), new(soup, null)], null), default);

        var early = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.GetNutritionAsync(created.Id, null, null, null, null, null, default));
        Assert.Equal("MENU_NOT_PUBLISHED", early.Code);

        await w.MenuSvc.SubmitAsync(created.Id, new(1), default);
        await w.MenuSvc.ReviewAsync(created.Id, new(1, "approve", null), default);
        await w.MenuSvc.PublishAsync(created.Id, new(1), default);

        var total = (await w.MenuSvc.GetNutritionAsync(created.Id, null, null, null, null, null, default)).Payload.Total;
        Assert.Equal(660m, total.Kcal);     // 460 + 200
        Assert.Equal(37m, total.ProteinG);
        Assert.Equal(16m, total.FatG);
        Assert.Equal(80m, total.CarbG);
    }

    [Fact]
    public async Task PublicationIsBlockedByIncompleteMacrosOrLegacyMlLines()
    {
        var w = new K13World();
        await w.RiceAndPork();
        var noFat = LegacyRecipe(w, "Legacy rice", "g", null, null);
        var legacyOil = LegacyRecipe(w, "Legacy oil", "ml", 100m, 0m);

        async Task<Guid> Approved(Guid recipeVersion, int day)
        {
            var created = await w.MenuSvc.CreateAsync(new(w.AddMealDay(Day(day)), [new(recipeVersion, null)], null), default);
            await w.MenuSvc.SubmitAsync(created.Id, new(1), default);
            await w.MenuSvc.ReviewAsync(created.Id, new(1, "approve", null), default);
            return created.Id;
        }

        var incompleteMenu = await Approved(noFat, 12);
        var incomplete = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.PublishAsync(incompleteMenu, new(1), default));
        Assert.Equal(422, incomplete.Status);
        Assert.Equal("NUTRIENT_DATA_INCOMPLETE", incomplete.Code);
        Assert.True(incomplete.Extensions!.ContainsKey("dishes"));

        var legacyMenu = await Approved(legacyOil, 13);
        var legacy = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.PublishAsync(legacyMenu, new(1), default));
        Assert.Equal(422, legacy.Status);
        Assert.Equal("LEGACY_CONVERSION_UNVERIFIED", legacy.Code);

        Assert.Empty(w.Snapshots);
        Assert.Equal("approved", (await w.MenuSvc.GetAsync(incompleteMenu, null, default)).Latest.State);
        Assert.Equal("approved", (await w.MenuSvc.GetAsync(legacyMenu, null, default)).Latest.State);
    }

    [Fact]
    public async Task PublicationRecordsHowTheAgeComparisonWentWithoutInventingRanges()
    {
        var w = new K13World();
        var (_, _, _, recipeVersion) = await Congee(w);

        var plain = await w.PublishedMenu(recipeVersion, Day(14));
        Assert.Equal("not_requested", w.Snapshots.Single(s => s.MenuVersionId == plain.VersionId).AgeComparisonStatus);

        var missing = await w.PublishedMenu(recipeVersion, Day(15), ageGroup: "Toddler");
        Assert.Equal("reference_missing", w.Snapshots.Single(s => s.MenuVersionId == missing.VersionId).AgeComparisonStatus);

        await w.ReferenceSvc.CreateAsync(Ref(basis: "daily"), default);
        var daily = await w.PublishedMenu(recipeVersion, Day(16), ageGroup: "Toddler");
        Assert.Equal("reference_basis_mismatch", w.Snapshots.Single(s => s.MenuVersionId == daily.VersionId).AgeComparisonStatus);

        var range = await w.ReferenceSvc.CreateAsync(Ref(), default);
        var compared = await w.PublishedMenu(recipeVersion, Day(17), ageGroup: "Toddler");
        var snapshot = w.Snapshots.Single(s => s.MenuVersionId == compared.VersionId);
        Assert.Equal("compared", snapshot.AgeComparisonStatus);

        var nutrition = await w.MenuSvc.GetNutritionAsync(compared.MenuId, null, null, null, null, null, default);
        var item = Assert.Single(nutrition.Payload.AgeComparison!.Items);
        Assert.Equal("within", item.Status);              // 460 kcal in 400-600 per meal
        Assert.Equal(range.Id, item.ReferenceId);
        Assert.Equal(range.Id, Assert.Single(nutrition.Payload.ReferenceVersionIds));

        // The reference is corrected later: the frozen comparison does not move, a recompute shows the change.
        w.Clock.Set(D(20));
        var corrected = await w.ReferenceSvc.CreateAsync(Ref(min: 500m, max: 700m, supersedes: range.Id), default);
        var frozen = await w.MenuSvc.GetNutritionAsync(compared.MenuId, null, null, null, null, null, default);
        Assert.Equal(nutrition.PayloadHash, frozen.PayloadHash);
        var recompute = await w.MenuSvc.GetNutritionAsync(compared.MenuId, null, "recompute", null, D(21), D(21), default);
        Assert.Equal("below", Assert.Single(recompute.Payload.AgeComparison!.Items).Status);   // 460 < 500
        Assert.Equal(2, recompute.ChangedReferenceVersionIds!.Count);
        Assert.Equal(corrected.Id, Assert.Single(recompute.Payload.ReferenceVersionIds));
    }

    [Fact]
    public async Task ParentsOnlySeeThePublishedFrozenVersionOfEnrolledLinkedChildren()
    {
        var w = new K13World();
        var (_, _, _, recipeVersion) = await Congee(w);
        var student = Guid.NewGuid();

        w.ParentLinked = false;
        var unlinked = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.ListForParentAsync(student, Day(1), Day(31), default));
        Assert.Equal(403, unlinked.Status);
        w.ParentLinked = true;

        var day = w.AddMealDay(Day(18));
        var created = await w.MenuSvc.CreateAsync(new(day, [new(recipeVersion, null)], null), default);
        await w.MenuSvc.SubmitAsync(created.Id, new(1), default);
        await w.MenuSvc.ReviewAsync(created.Id, new(1, "approve", null), default);
        Assert.Empty((await w.MenuSvc.ListForParentAsync(student, Day(1), Day(31), default)).Items);   // approved is not published

        var published = await w.MenuSvc.PublishAsync(created.Id, new(1), default);
        var seen = Assert.Single((await w.MenuSvc.ListForParentAsync(student, Day(1), Day(31), default)).Items);
        Assert.Equal(created.VersionId, seen.MenuVersionId);
        Assert.Equal("published", seen.State);
        Assert.Equal(published.NutritionSnapshotId, seen.CalculationSnapshotId);
        Assert.Equal(460m, seen.Nutrition!.Kcal);
        Assert.Equal("Congee", Assert.Single(seen.Dishes).RecipeName);

        // A newer draft is invisible: the parent keeps seeing what was published.
        await w.MenuSvc.AddVersionAsync(created.Id, new(1, [new(recipeVersion, 5)], "draft change"), default);
        Assert.Equal(created.VersionId, Assert.Single((await w.MenuSvc.ListForParentAsync(student, Day(1), Day(31), default)).Items).MenuVersionId);

        // A date on which the child was not enrolled in any class is left out.
        w.CurrentClass = date => date == Day(18) ? null : Guid.NewGuid();
        Assert.Empty((await w.MenuSvc.ListForParentAsync(student, Day(1), Day(31), default)).Items);

        var backwards = await Assert.ThrowsAsync<KitchenException>(() => w.MenuSvc.ListForParentAsync(student, Day(5), Day(1), default));
        Assert.Equal("VALIDATION_ERROR", backwards.Code);
    }

    // ---- P3-H01: ingredient estimates -----------------------------------------------------------------------

    [Fact]
    public async Task EstimateUsesLatestClassCountsRejectsStaleTokensAndNeverChangesAfterwards()
    {
        var w = new K13World();
        var (rice, pork, _, recipeVersion) = await Congee(w);
        var menu = await w.PublishedMenu(recipeVersion, Day(19));
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        w.Settle(menu.MealDayId, "tok-1", (classA, 20), (classB, 10));

        IngredientEstimateRequest Request(string token, List<Guid>? classes = null) =>
            new([menu.VersionId], [menu.MealDayId], classes, new() { [menu.MealDayId] = token });

        var estimate = await w.EstimateSvc.CreateAsync(Request("tok-1"), default);
        Assert.True(estimate.IsPlanningEstimate);
        var riceLine = estimate.Lines.Single(l => l.IngredientId == rice);
        Assert.Equal(3000m, riceLine.GrossGrams);                 // 100 g per portion x 30 portions
        Assert.Equal("g", riceLine.Unit);
        Assert.Equal(1500m, estimate.Lines.Single(l => l.IngredientId == pork).GrossGrams);
        Assert.Equal(2, estimate.Portions.Count);
        Assert.Equal(2, estimate.SettlementIds.Count);
        Assert.Equal(recipeVersion, Assert.Single(riceLine.RecipeVersionIds));
        Assert.Equal(64, estimate.PayloadHash.Length);

        var stale = await Assert.ThrowsAsync<KitchenException>(() => w.EstimateSvc.CreateAsync(Request("old"), default));
        Assert.Equal(409, stale.Status);
        Assert.Equal("STALE_SETTLEMENT", stale.Code);

        var noToken = await Assert.ThrowsAsync<KitchenException>(() => w.EstimateSvc.CreateAsync(
            new([menu.VersionId], null, null, new()), default));
        Assert.True(Err(noToken).Keys.Any(k => k.StartsWith("snapshotTokens")));

        var onlyA = await w.EstimateSvc.CreateAsync(Request("tok-1", [classA]), default);
        Assert.Equal(2000m, onlyA.Lines.Single(l => l.IngredientId == rice).GrossGrams);

        // The settlement is amended: the stored estimate is untouched, a new one reflects it.
        w.Settle(menu.MealDayId, "tok-2", (classA, 25), (classB, 10));
        var stored = await w.EstimateSvc.GetAsync(estimate.Id, default);
        Assert.Equal(3000m, stored.Lines.Single(l => l.IngredientId == rice).GrossGrams);
        Assert.Equal(estimate.PayloadHash, stored.PayloadHash);
        var amended = await w.EstimateSvc.CreateAsync(Request("tok-2"), default);
        Assert.Equal(3500m, amended.Lines.Single(l => l.IngredientId == rice).GrossGrams);
        Assert.NotEqual(estimate.PayloadHash, amended.PayloadHash);

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.EstimateSvc.GetAsync(Guid.NewGuid(), default));
        Assert.Equal("ESTIMATE_NOT_FOUND", unknown.Code);
    }

    [Fact]
    public async Task EstimateRequiresPublishedMenusAndConfirmedConversions()
    {
        var w = new K13World();
        var (_, _, _, recipeVersion) = await Congee(w);

        var draftDay = w.AddMealDay(Day(21));
        var draft = await w.MenuSvc.CreateAsync(new(draftDay, [new(recipeVersion, null)], null), default);
        w.Settle(draftDay, "t", (Guid.NewGuid(), 5));
        var unpublished = await Assert.ThrowsAsync<KitchenException>(() => w.EstimateSvc.CreateAsync(
            new([draft.VersionId], null, null, new() { [draftDay] = "t" }), default));
        Assert.Equal(422, unpublished.Status);
        Assert.Equal("MENU_NOT_PUBLISHED", unpublished.Code);

        // A menu published before the ml rule existed: the estimate refuses to guess the grams.
        var legacyDay = w.AddMealDay(Day(22));
        var legacyMenu = new Menu { MealDayId = legacyDay };
        var legacyVersion = new MenuVersion { MenuId = legacyMenu.Id, Version = 1 };
        w.MenuRows.Add(legacyMenu);
        w.MenuVersions.Add(legacyVersion);
        w.MenuItems.Add(new MenuItem { MenuVersionId = legacyVersion.Id, RecipeVersionId = LegacyRecipe(w, "Old oil", "ml", 100m, 0m) });
        w.Transitions.Add(new MenuTransition { MenuVersionId = legacyVersion.Id, Sequence = 1, State = "published" });
        w.Settle(legacyDay, "t", (Guid.NewGuid(), 5));
        var legacy = await Assert.ThrowsAsync<KitchenException>(() => w.EstimateSvc.CreateAsync(
            new([legacyVersion.Id], null, null, new() { [legacyDay] = "t" }), default));
        Assert.Equal("LEGACY_CONVERSION_UNVERIFIED", legacy.Code);
    }

    [Fact]
    public async Task EstimateConvertsMlWithDensityAndGrossFromEdibleWeights()
    {
        var w = new K13World();
        var oil = await w.AddIngredient("Oil", "ml");
        var carrot = await w.AddIngredient("Carrot");
        var peel = await w.AddIngredient("Peel");
        w.Clock.Set(D(2));
        await w.Ingredients.AddVersionAsync(oil, K13World.V(D(1), 884m, 0m, 100m, 0m, density: 0.92m), default);
        await w.Ingredients.AddVersionAsync(carrot, K13World.V(D(1), 40m, 1m, 0m, 9m, edible: 50m), default);
        await w.Ingredients.AddVersionAsync(peel, K13World.V(D(1), 40m, 1m, 0m, 9m, edible: 0m), default);

        var fry = (await w.Recipes.CreateAsync(new("Fry", 1, [new(oil, 100m, "ml")]), default)).VersionId!.Value;
        var salad = (await w.Recipes.CreateAsync(new("Salad", 1, [new(carrot, 100m, "g", "edible")]), default)).VersionId!.Value;
        var zero = (await w.Recipes.CreateAsync(new("Zero", 1, [new(peel, 100m, "g", "edible")]), default)).VersionId!.Value;

        var fryMenu = await w.PublishedMenu(fry, Day(23));
        var saladMenu = await w.PublishedMenu(salad, Day(24));
        var zeroMenu = await w.PublishedMenu(zero, Day(25));
        w.Settle(fryMenu.MealDayId, "a", (Guid.NewGuid(), 10));
        w.Settle(saladMenu.MealDayId, "b", (Guid.NewGuid(), 10));
        w.Settle(zeroMenu.MealDayId, "c", (Guid.NewGuid(), 10));

        var estimate = await w.EstimateSvc.CreateAsync(new(
            [fryMenu.VersionId, saladMenu.VersionId], [fryMenu.MealDayId, saladMenu.MealDayId], null,
            new() { [fryMenu.MealDayId] = "a", [saladMenu.MealDayId] = "b" }), default);

        var oilLine = estimate.Lines.Single(l => l.IngredientId == oil);
        Assert.Equal(920m, oilLine.GrossGrams);                   // 100 ml x 0.92 g/ml x 10 portions
        Assert.Equal(0.92m, Assert.Single(oilLine.Conversion).GramsPerMl);
        var carrotLine = estimate.Lines.Single(l => l.IngredientId == carrot);
        Assert.Equal(1000m, carrotLine.EdibleGrams);              // 100 g edible x 10
        Assert.Equal(2000m, carrotLine.GrossGrams);               // gross = edible / 0.5, not edible x 0.5
        Assert.Equal("edible", Assert.Single(carrotLine.Conversion).WeightBasis);

        var invalid = await Assert.ThrowsAsync<KitchenException>(() => w.EstimateSvc.CreateAsync(
            new([zeroMenu.VersionId], null, null, new() { [zeroMenu.MealDayId] = "c" }), default));
        Assert.Equal(422, invalid.Status);
        Assert.Equal("EDIBLE_FRACTION_INVALID", invalid.Code);
    }

    [Fact]
    public async Task CsvExportNeutralisesFormulasAndQuotesSpecialCharacters()
    {
        var w = new K13World();
        var names = new[] { "=SUM(1+1)", "Pork, lean", "Say \"hi\"" };
        var ids = new List<Guid>();
        w.Clock.Set(D(2));
        foreach (var name in names)
        {
            var id = await w.AddIngredient(name);
            await w.Ingredients.AddVersionAsync(id, K13World.V(D(1), 100m, 5m, 1m, 10m), default);
            ids.Add(id);
        }

        var recipe = await w.Recipes.CreateAsync(new("Odd", 1, ids.Select(id => new RecipeLineRequest(id, 10m, "g")).ToList()), default);
        var menu = await w.PublishedMenu(recipe.VersionId!.Value, Day(26));
        w.Settle(menu.MealDayId, "t", (Guid.NewGuid(), 3));
        var estimate = await w.EstimateSvc.CreateAsync(new([menu.VersionId], null, null, new() { [menu.MealDayId] = "t" }), default);

        var csv = await w.EstimateSvc.ExportCsvAsync(estimate.Id, default);

        Assert.True(csv.StartsWith("ingredientId,ingredientName,unit,grossGrams,edibleGrams\r\n"));
        Assert.Contains(",'=SUM(1+1),g,30,30\r\n", csv);
        Assert.Contains(",\"Pork, lean\",g,30,30\r\n", csv);
        Assert.Contains(",\"Say \"\"hi\"\"\",g,30,30\r\n", csv);
        Assert.False(csv.Contains(",=SUM", StringComparison.Ordinal));
    }

    // ---- P3-H02: reference ranges -----------------------------------------------------------------------------

    [Fact]
    public async Task ReferenceRangesAreValidatedVersionedAndCorrectedBySuperseding()
    {
        var w = new K13World();

        foreach (var bad in new[]
                 {
                     Ref(min: null, max: null), Ref(min: 700m, max: 400m), Ref(basis: "weekly"), Ref(nutrient: "salt"),
                     Ref() with { Reason = null }, Ref() with { Source = null }, Ref() with { EffectiveTo = D(1) }
                 })
        {
            var ex = await Assert.ThrowsAsync<KitchenException>(() => w.ReferenceSvc.CreateAsync(bad, default));
            Assert.Equal("VALIDATION_ERROR", ex.Code);
        }

        var first = await w.ReferenceSvc.CreateAsync(Ref(), default);
        Assert.Equal(1, first.Version);

        var overlap = await Assert.ThrowsAsync<KitchenException>(() => w.ReferenceSvc.CreateAsync(Ref(min: 450m, max: 650m), default));
        Assert.Equal("REFERENCE_OVERLAP", overlap.Code);
        await w.ReferenceSvc.CreateAsync(Ref(mealType: "Breakfast"), default);   // another meal type is another key

        w.Clock.Set(D(5));
        var second = await w.ReferenceSvc.CreateAsync(Ref(min: 450m, max: 650m, supersedes: first.Id), default);
        Assert.Equal(2, second.Version);
        Assert.Equal(first.Id, second.SupersedesId);

        var again = await Assert.ThrowsAsync<KitchenException>(() => w.ReferenceSvc.CreateAsync(Ref(supersedes: first.Id), default));
        Assert.Equal("REFERENCE_ALREADY_SUPERSEDED", again.Code);
        var otherKey = await Assert.ThrowsAsync<KitchenException>(() => w.ReferenceSvc.CreateAsync(Ref(nutrient: "proteinG", supersedes: second.Id), default));
        Assert.True(Err(otherKey).ContainsKey("supersedesId"));

        var now = await w.ReferenceSvc.ListAsync("toddler", null, null, default);
        var lunch = now.Items.Single(i => i.Nutrient == "kcal" && i.MealType == "Lunch");
        Assert.Equal(second.Id, lunch.Id);
        Assert.Equal(450m, lunch.MinValue);
        Assert.Equal(2, now.Items.Count);

        var before = await w.ReferenceSvc.ListAsync("Toddler", D(6), D(3), default);   // what was known on day 3
        Assert.Equal(first.Id, before.Items.Single(i => i.MealType == "Lunch").Id);

        var noGroup = await Assert.ThrowsAsync<KitchenException>(() => w.ReferenceSvc.ListAsync(null, null, null, default));
        Assert.True(Err(noGroup).ContainsKey("ageGroup"));
    }

    [Fact]
    public async Task ComparisonNeverUsesDailyUnspecifiedOrOtherMealRanges()
    {
        var w = new K13World();
        var (_, _, recipeId, _) = await Congee(w);

        Task<MealTrace.Application.Dtos.Kitchen.NutritionResponse> Compare(string? mealType) =>
            w.Nutrition.CalculateAsync(recipeId, 1, new NutritionQuery(AgeGroup: "Toddler", MealType: mealType), default);

        var none = await Assert.ThrowsAsync<KitchenException>(() => Compare("Lunch"));
        Assert.Equal("AGE_GROUP_REFERENCE_MISSING", none.Code);

        await w.ReferenceSvc.CreateAsync(Ref(basis: "daily", min: 1000m, max: 1400m, mealType: null), default);
        w.Ranges.Add(new NutrientReferenceRange
        {
            AgeGroup = "Toddler", Nutrient = "proteinG", MinValue = 1m, MaxValue = 2m, Source = "old", EffectiveFrom = D(1), RecordedAt = D(1)
        });
        var onlyDaily = await Assert.ThrowsAsync<KitchenException>(() => Compare("Lunch"));
        Assert.Equal(422, onlyDaily.Status);
        Assert.Equal("REFERENCE_BASIS_MISMATCH", onlyDaily.Code);

        await w.ReferenceSvc.CreateAsync(Ref(mealType: "Breakfast", min: 100m, max: 200m), default);
        var wrongMeal = await Assert.ThrowsAsync<KitchenException>(() => Compare("Lunch"));
        Assert.Equal("REFERENCE_BASIS_MISMATCH", wrongMeal.Code);

        var breakfast = (await Compare("Breakfast")).AgeGroupComparison!;
        Assert.Equal("above", Assert.Single(breakfast.Items).Status);   // 460 kcal against 100-200
        Assert.Equal("perMeal", breakfast.Items[0].Basis);

        await w.ReferenceSvc.CreateAsync(Ref(mealType: null, min: 400m, max: 600m), default);
        var lunch = (await Compare("Lunch")).AgeGroupComparison!;
        Assert.Equal("within", Assert.Single(lunch.Items).Status);
    }

    // ---- P2-H03: recipe catalogue ------------------------------------------------------------------------------

    [Fact]
    public async Task RecipeCatalogueAndVersionListKeepEveryVersionAddressable()
    {
        var w = new K13World();
        var (rice, _, recipeId, v1) = await Congee(w);
        var second = await w.Recipes.AddVersionAsync(recipeId, new(1, 2, [new(rice, 150m, "g")], Reason: "less rice"), default);
        var legacyVersion = LegacyRecipe(w, "Old oil", "ml", 100m, 0m);

        var catalogue = await w.Recipes.ListAsync(null, 1, 500, default);
        Assert.Equal(100, catalogue.PageSize);
        Assert.Equal(2, catalogue.Total);
        var congee = catalogue.Items.Single(i => i.Name == "Congee");
        Assert.Equal(2, congee.Version);
        Assert.Equal(second.VersionId, congee.VersionId);
        Assert.Equal(1, (await w.Recipes.ListAsync("cong", 1, 25, default)).Total);
        Assert.Equal(0, (await w.Recipes.ListAsync("zzz", 1, 25, default)).Total);

        var versions = await w.Recipes.ListVersionsAsync(recipeId, default);
        Assert.Equal(new[] { 1, 2 }, versions.Items.Select(i => i.Version).ToArray());
        Assert.Equal(v1, versions.Items[0].VersionId);
        Assert.Equal(2, versions.Items[0].Lines.Count);
        Assert.Equal("less rice", versions.Items[1].Reason);
        Assert.Equal(w.Actor.UserId, versions.Items[1].RecordedBy);
        Assert.Equal(1, versions.Items[1].Lines.Count);

        var oldRecipe = w.Recipe_.Single(r => r.Name == "Old oil recipe");
        var legacy = await w.Recipes.ListVersionsAsync(oldRecipe.Id, default);
        Assert.Equal(legacyVersion, legacy.Items[0].VersionId);
        Assert.True(Assert.Single(legacy.Items[0].Lines).LegacyConversionUnverified);
        Assert.False(versions.Items[0].Lines[0].LegacyConversionUnverified);

        var unknown = await Assert.ThrowsAsync<KitchenException>(() => w.Recipes.ListVersionsAsync(Guid.NewGuid(), default));
        Assert.Equal("RECIPE_NOT_FOUND", unknown.Code);
    }
}

using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

/// <summary>
/// Menu versions are immutable; their state is the latest append-only transition (draft, submitted, approved or
/// rejected, published). Publishing freezes the nutrition result, so later edits to recipes, nutrient data or
/// reference ranges can never change what a parent was shown.
/// </summary>
public sealed class MenuService(
    IMenuRepository repository, INutritionService nutrition, IParentLinkRepository parentLinks,
    TimeProvider time, IUnitOfWork unitOfWork, ICurrentActor actor) : IMenuService
{
    public const string AlgorithmVersion = "menu-nutrition/1";
    private const int MaxReasonLength = 500;
    private const int MaxDishes = 30;

    private sealed record Loaded(Menu Menu, List<MenuVersion> Versions, MenuVersion Latest, List<MenuTransition> LatestTransitions)
    {
        public string State => StateOf(LatestTransitions);
    }

    // ---- lifecycle ----------------------------------------------------------------------------------------

    public async Task<MenuCreatedResponse> CreateAsync(CreateMenuRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var reason = CleanReason(request.Reason, false, errors);
        if (request.MealDayId is null || request.MealDayId == Guid.Empty) errors.Add("mealDayId", "mealDayId is required.");
        ValidateDishes(request.Dishes, errors);
        errors.ThrowIfAny();

        var day = await repository.FindMealDayAsync(request.MealDayId!.Value, ct)
            ?? throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Meal session not found.");
        if (day.IsCancelled)
            throw new KitchenException(409, "MEAL_DAY_CANCELLED", "This meal session is cancelled; it cannot have a menu.");

        var existing = await repository.FindMenuByMealDayAsync(day.Id, ct);
        if (existing is not null)
            throw new KitchenException(409, "MENU_ALREADY_EXISTS",
                "This meal session already has a menu; add a new version to it instead.",
                new Dictionary<string, object?> { ["menuId"] = existing.Id });

        await ResolveDishesAsync(request.Dishes!, errors, ct);

        var now = time.GetUtcNow();
        var menu = new Menu { Id = Guid.NewGuid(), MealDayId = day.Id, CreatedAt = now, CreatedByUserId = actor.UserId };
        repository.AddMenu(menu);
        var version = StageVersion(menu.Id, 1, null, reason, request.Dishes!, now);

        await SaveOrConflictAsync(1, ct);
        return new MenuCreatedResponse(menu.Id, version.Id, 1, MenuStates.Draft, now.ToVn(), actor.UserId);
    }

    public async Task<MenuCreatedResponse> AddVersionAsync(Guid menuId, CreateMenuVersionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var reason = CleanReason(request.Reason, true, errors);
        if (request.ExpectedVersion is null or < 1) errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        ValidateDishes(request.Dishes, errors);
        errors.ThrowIfAny();

        var loaded = await LoadAsync(menuId, ct);
        EnsureExpected(loaded, request.ExpectedVersion!.Value);

        // A version under review cannot be replaced; every other state can be followed by a new draft version.
        if (loaded.State == MenuStates.Submitted)
            throw StateInvalid(loaded.State, "A submitted version must be approved or rejected before a new version is added.");

        await ResolveDishesAsync(request.Dishes!, errors, ct);

        var now = time.GetUtcNow();
        var next = loaded.Latest.Version + 1;
        var version = StageVersion(menuId, next, loaded.Latest.Id, reason, request.Dishes!, now);

        await SaveOrConflictAsync(loaded.Latest.Version, ct);
        return new MenuCreatedResponse(menuId, version.Id, next, MenuStates.Draft, now.ToVn(), actor.UserId, loaded.Latest.Id);
    }

    public async Task<MenuTransitionResponse> SubmitAsync(Guid menuId, MenuTransitionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var reason = CleanReason(request.Reason, false, errors);
        if (request.ExpectedVersion is null or < 1) errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        errors.ThrowIfAny();

        var loaded = await LoadAsync(menuId, ct);
        EnsureExpected(loaded, request.ExpectedVersion!.Value);
        if (loaded.State != MenuStates.Draft)
            throw StateInvalid(loaded.State, "Only a draft version can be submitted.");

        var transition = Stage(loaded, MenuStates.Submitted, reason);
        await SaveOrConflictAsync(loaded.Latest.Version, ct);
        return Response(menuId, loaded.Latest, transition);
    }

    public async Task<MenuTransitionResponse> ReviewAsync(Guid menuId, MenuReviewRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var reason = CleanReason(request.Reason, false, errors);
        var decision = request.Decision?.Trim().ToLowerInvariant();
        if (request.ExpectedVersion is null or < 1) errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        if (decision is not ("approve" or "reject")) errors.Add("decision", "decision must be 'approve' or 'reject'.");
        if (decision == "reject" && string.IsNullOrEmpty(reason)) errors.Add("reason", "reason is required when rejecting a menu.");
        errors.ThrowIfAny();

        var loaded = await LoadAsync(menuId, ct);
        EnsureExpected(loaded, request.ExpectedVersion!.Value);
        if (loaded.State != MenuStates.Submitted)
            throw StateInvalid(loaded.State, "Only a submitted version can be reviewed.");

        var transition = Stage(loaded, decision == "approve" ? MenuStates.Approved : MenuStates.Rejected, reason);
        await SaveOrConflictAsync(loaded.Latest.Version, ct);
        return Response(menuId, loaded.Latest, transition);
    }

    public async Task<MenuTransitionResponse> PublishAsync(Guid menuId, MenuPublishRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var ageGroup = string.IsNullOrWhiteSpace(request.AgeGroup) ? null : request.AgeGroup.Trim();
        if (request.ExpectedVersion is null or < 1) errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        if (ageGroup is { Length: > 50 }) errors.Add("ageGroup", "ageGroup must be at most 50 characters.");
        errors.ThrowIfAny();

        var loaded = await LoadAsync(menuId, ct);
        EnsureExpected(loaded, request.ExpectedVersion!.Value);
        if (loaded.State != MenuStates.Approved)
            throw StateInvalid(loaded.State, "Only an approved version can be published.");

        var day = await repository.FindMealDayAsync(loaded.Menu.MealDayId, ct)
            ?? throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Meal session not found.");
        if (day.IsCancelled)
            throw new KitchenException(409, "MEAL_DAY_CANCELLED", "This meal session is cancelled; its menu cannot be published.");

        var items = (await repository.ListItemsAsync([loaded.Latest.Id], ct)).OrderBy(i => i.SortOrder).ToList();
        var now = time.GetUtcNow();

        // Everything below happens before anything is staged, so a 422 leaves no trace.
        var payload = await BuildPayloadAsync(items, ageGroup, day.MealType, now, ct);
        var json = KitchenJson.Serialize(payload);

        var snapshot = new MenuNutritionSnapshot
        {
            Id = Guid.NewGuid(),
            MenuVersionId = loaded.Latest.Id,
            AlgorithmVersion = AlgorithmVersion,
            AgeGroup = ageGroup,
            AgeComparisonStatus = payload.AgeComparisonStatus,
            PayloadJson = json,
            PayloadHash = KitchenJson.Sha256(json),
            RecordedAt = now,
            RecordedByUserId = actor.UserId
        };
        repository.AddSnapshot(snapshot);
        var transition = Stage(loaded, MenuStates.Published, null);

        await SaveOrConflictAsync(loaded.Latest.Version, ct);
        return Response(menuId, loaded.Latest, transition, snapshot.Id, now.ToVn());
    }

    // ---- reads --------------------------------------------------------------------------------------------

    public async Task<MenuPage> ListAsync(DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNo, size, skip) = KitchenPaging.Normalize(page, pageSize);
        var total = await repository.CountMenusAsync(from, to, ct);
        var rows = await repository.ListMenusAsync(from, to, skip, size, ct);

        var views = new List<MenuView>();
        foreach (var row in rows)
            views.Add(await BuildViewAsync(row.Menu, row.MealDay, null, ct));

        return new MenuPage(views, total, pageNo, size);
    }

    public async Task<MenuView> GetAsync(Guid menuId, int? version, CancellationToken ct)
    {
        var menu = await repository.FindMenuAsync(menuId, ct)
            ?? throw KitchenException.NotFound("MENU_NOT_FOUND", "Menu not found.");
        var day = await repository.FindMealDayAsync(menu.MealDayId, ct)
            ?? throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Meal session not found.");
        return await BuildViewAsync(menu, day, version, ct);
    }

    public async Task<MenuHistory> HistoryAsync(Guid menuId, CancellationToken ct)
    {
        var menu = await repository.FindMenuAsync(menuId, ct)
            ?? throw KitchenException.NotFound("MENU_NOT_FOUND", "Menu not found.");
        var versions = (await repository.ListVersionsAsync(menuId, ct)).OrderBy(v => v.Version).ToList();
        var views = await BuildVersionViewsAsync(versions, ct);
        return new MenuHistory(menu.Id, menu.MealDayId, views);
    }

    public async Task<MenuNutritionResponse> GetNutritionAsync(
        Guid menuId, int? version, string? mode, string? ageGroup, DateTimeOffset? validAt, DateTimeOffset? knownAt,
        CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var selected = string.IsNullOrWhiteSpace(mode) ? "pinned" : mode.Trim().ToLowerInvariant();
        if (selected is not ("pinned" or "recompute")) errors.Add("mode", "mode must be 'pinned' or 'recompute'.");
        else if (selected == "pinned" && (validAt is not null || knownAt is not null))
            errors.Add("mode", "validAt/knownAt only apply to mode=recompute; mode=pinned returns the frozen snapshot.");
        errors.ThrowIfAny();

        var menu = await repository.FindMenuAsync(menuId, ct)
            ?? throw KitchenException.NotFound("MENU_NOT_FOUND", "Menu not found.");
        var versions = await repository.ListVersionsAsync(menuId, ct);
        var transitions = await repository.ListTransitionsAsync(versions.Select(v => v.Id).ToArray(), ct);

        // Default: the latest published version. An explicit version must itself be published.
        var published = versions.OrderByDescending(v => v.Version)
            .Where(v => IsPublished(transitions, v.Id)).ToList();
        var target = version is null
            ? published.FirstOrDefault()
            : versions.FirstOrDefault(v => v.Version == version);
        if (target is null)
            throw KitchenException.NotFound(version is null ? "MENU_NOT_PUBLISHED" : "MENU_VERSION_NOT_FOUND",
                version is null ? "This menu has no published version." : $"Menu version {version} not found.");
        if (!IsPublished(transitions, target.Id))
            throw new KitchenException(409, "MENU_NOT_PUBLISHED", $"Menu version {target.Version} is not published.");

        var snapshot = (await repository.ListSnapshotsAsync([target.Id], ct)).Single();
        var frozen = KitchenJson.Deserialize<MenuNutritionPayload>(snapshot.PayloadJson);

        if (selected == "pinned")
            return new MenuNutritionResponse("pinned", target.Id, target.Version, snapshot.Id, snapshot.PayloadHash, frozen);

        var day = await repository.FindMealDayAsync(menu.MealDayId, ct)
            ?? throw KitchenException.NotFound("MEAL_DAY_NOT_FOUND", "Meal session not found.");
        var items = (await repository.ListItemsAsync([target.Id], ct)).OrderBy(i => i.SortOrder).ToList();
        var now = time.GetUtcNow();
        var group = string.IsNullOrWhiteSpace(ageGroup) ? snapshot.AgeGroup : ageGroup.Trim();

        // A recompute is a separate view: it is never stored and never replaces the snapshot.
        var recomputed = await BuildPayloadAsync(items, group, day.MealType, now, ct, recompute: true, validAt, knownAt);

        var changedNutrients = frozen.Dishes.SelectMany(d => d.Calculation.Lines.Select(l => l.NutrientVersionId))
            .Except(recomputed.Dishes.SelectMany(d => d.Calculation.Lines.Select(l => l.NutrientVersionId)))
            .Concat(recomputed.Dishes.SelectMany(d => d.Calculation.Lines.Select(l => l.NutrientVersionId))
                .Except(frozen.Dishes.SelectMany(d => d.Calculation.Lines.Select(l => l.NutrientVersionId))))
            .Distinct().ToList();
        var changedReferences = frozen.ReferenceVersionIds.Except(recomputed.ReferenceVersionIds)
            .Concat(recomputed.ReferenceVersionIds.Except(frozen.ReferenceVersionIds)).Distinct().ToList();

        return new MenuNutritionResponse(
            "recompute", target.Id, target.Version, snapshot.Id, snapshot.PayloadHash, recomputed, frozen,
            Delta(recomputed.Total, frozen.Total), changedReferences, changedNutrients);
    }

    public async Task<ParentMenuResponse> ListForParentAsync(Guid studentId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var parentId = actor.UserId
            ?? throw new KitchenException(401, "ACTOR_NOT_RESOLVED", "The authenticated user id could not be resolved.");
        if (to < from || to.DayNumber - from.DayNumber > 62)
        {
            var errors = new ValidationErrors();
            errors.Add("to", "The range must be at most 62 days and 'to' must not be before 'from'.");
            errors.ThrowIfAny();
        }

        // Unknown student and unlinked student look identical: no enumeration of children.
        if (!await parentLinks.LinkedAsync(parentId, studentId))
            throw new KitchenException(403, "FORBIDDEN", "You do not have access to this student.");

        var rows = await repository.ListMenusAsync(from, to, 0, int.MaxValue, ct);
        var items = new List<ParentMenuItem>();
        foreach (var row in rows)
        {
            // A child sees the meals of dates on which they were enrolled in a class.
            if (await parentLinks.CurrentClassAsync(studentId, row.MealDay.Date) is null) continue;

            var versions = await repository.ListVersionsAsync(row.Menu.Id, ct);
            var transitions = await repository.ListTransitionsAsync(versions.Select(v => v.Id).ToArray(), ct);
            var publishedVersion = versions.OrderByDescending(v => v.Version).FirstOrDefault(v => IsPublished(transitions, v.Id));
            if (publishedVersion is null || row.MealDay.IsCancelled) continue;

            var snapshot = (await repository.ListSnapshotsAsync([publishedVersion.Id], ct)).Single();
            var frozen = KitchenJson.Deserialize<MenuNutritionPayload>(snapshot.PayloadJson);
            var dishes = (await BuildVersionViewsAsync([publishedVersion], ct)).Single().Dishes;

            items.Add(new ParentMenuItem(
                row.MealDay.Id, row.MealDay.Date, row.MealDay.MealType, publishedVersion.Id, MenuStates.Published, dishes,
                frozen.Total, snapshot.Id, frozen.ReferenceVersionIds, snapshot.AgeComparisonStatus, snapshot.AgeGroup));
        }

        return new ParentMenuResponse(items.OrderBy(i => i.Date).ThenBy(i => i.MealType, StringComparer.Ordinal).ToList(), items.Count);
    }

    // ---- publication maths --------------------------------------------------------------------------------

    private async Task<MenuNutritionPayload> BuildPayloadAsync(
        IReadOnlyList<MenuItem> items, string? ageGroup, string mealType, DateTimeOffset now, CancellationToken ct,
        bool recompute = false, DateTimeOffset? validAt = null, DateTimeOffset? knownAt = null)
    {
        var refs = (await repository.FindRecipeVersionsAsync(items.Select(i => i.RecipeVersionId).ToArray(), ct))
            .ToDictionary(r => r.Version.Id);

        var dishes = new List<MenuDishNutrition>();
        var incomplete = new List<object>();
        var unverified = new List<object>();
        decimal kcal = 0m, protein = 0m, fat = 0m, carb = 0m;

        foreach (var item in items)
        {
            var rv = refs[item.RecipeVersionId];
            var query = recompute ? new NutritionQuery("recompute", validAt, knownAt) : new NutritionQuery("pinned");
            var calc = await nutrition.CalculateAsync(rv.Version.RecipeId, rv.Version.Version, query, ct);

            if (calc.IncompleteData is { Count: > 0 } missing)
                incomplete.Add(new
                {
                    recipeVersionId = rv.Version.Id, recipeName = rv.RecipeName,
                    ingredients = missing.Select(m => new { m.IngredientId, m.IngredientName, m.MissingFields })
                });
            if (calc.UnverifiedConversions is { Count: > 0 } legacy)
                unverified.Add(new
                {
                    recipeVersionId = rv.Version.Id, recipeName = rv.RecipeName,
                    ingredients = legacy.Select(m => new { m.IngredientId, m.Name })
                });

            foreach (var line in calc.Lines)
            {
                kcal += line.Contribution.Kcal;
                protein += line.Contribution.ProteinG;
                fat += line.Contribution.FatG ?? 0m;
                carb += line.Contribution.CarbG ?? 0m;
            }

            dishes.Add(new MenuDishNutrition(
                rv.Version.Id, rv.Version.RecipeId, rv.RecipeName, rv.Version.Version, item.PlannedPortions,
                calc.PerServing, calc));
        }

        if (incomplete.Count > 0)
            throw new KitchenException(422, "NUTRIENT_DATA_INCOMPLETE",
                "Some dishes use ingredients whose nutrient data lacks fat or carbohydrate, so the menu's nutrition is not complete.",
                new Dictionary<string, object?> { ["dishes"] = incomplete });
        if (unverified.Count > 0)
            throw new KitchenException(422, "LEGACY_CONVERSION_UNVERIFIED",
                "Some dishes use a legacy line of an ml ingredient whose gram conversion was never recorded; re-enter it in a new recipe version.",
                new Dictionary<string, object?> { ["dishes"] = unverified });

        var rounding = RecipeService.DefaultRounding;
        var total = new NutritionTotals(
            Math.Round(kcal, rounding.KcalDecimals, MidpointRounding.AwayFromZero),
            Math.Round(protein, rounding.ProteinDecimals, MidpointRounding.AwayFromZero),
            Math.Round(fat, rounding.ProteinDecimals, MidpointRounding.AwayFromZero),
            Math.Round(carb, rounding.ProteinDecimals, MidpointRounding.AwayFromZero));

        AgeGroupComparison? comparison = null;
        var status = "not_requested";
        if (ageGroup is not null)
        {
            try
            {
                comparison = await nutrition.CompareAsync(
                    ageGroup, total, mealType, (recompute ? validAt : null) ?? now, (recompute ? knownAt : null) ?? now, ct);
                status = "compared";
            }
            catch (KitchenException ex) when (ex.Code == "AGE_GROUP_REFERENCE_MISSING")
            {
                status = "reference_missing";
            }
            catch (KitchenException ex) when (ex.Code == "REFERENCE_BASIS_MISMATCH")
            {
                status = "reference_basis_mismatch";
            }
        }

        return new MenuNutritionPayload(
            AlgorithmVersion, rounding, dishes, total, ageGroup, status, comparison,
            comparison?.Items.Select(i => i.ReferenceId).Distinct().ToList() ?? []);
    }

    private static NutritionTotals Delta(NutritionTotals a, NutritionTotals b) =>
        new(a.Kcal - b.Kcal, a.ProteinG - b.ProteinG, a.FatG - b.FatG, a.CarbG - b.CarbG);

    // ---- helpers ------------------------------------------------------------------------------------------

    private static string StateOf(IEnumerable<MenuTransition> transitions) =>
        transitions.OrderBy(t => t.Sequence).LastOrDefault()?.State ?? MenuStates.Draft;

    private static bool IsPublished(IEnumerable<MenuTransition> all, Guid versionId) =>
        StateOf(all.Where(t => t.MenuVersionId == versionId)) == MenuStates.Published;

    private async Task<Loaded> LoadAsync(Guid menuId, CancellationToken ct)
    {
        var menu = await repository.FindMenuAsync(menuId, ct)
            ?? throw KitchenException.NotFound("MENU_NOT_FOUND", "Menu not found.");
        var versions = await repository.ListVersionsAsync(menuId, ct);
        var latest = versions.OrderByDescending(v => v.Version).First();
        var transitions = await repository.ListTransitionsAsync([latest.Id], ct);
        return new Loaded(menu, versions, latest, transitions);
    }

    private static void EnsureExpected(Loaded loaded, int expectedVersion)
    {
        if (loaded.Latest.Version != expectedVersion)
            throw new KitchenException(409, "MENU_VERSION_CONFLICT",
                "The menu was changed by someone else. Reload it and retry with the latest version.",
                new Dictionary<string, object?> { ["currentVersion"] = loaded.Latest.Version, ["state"] = loaded.State });
    }

    private static KitchenException StateInvalid(string state, string message) =>
        new(409, "MENU_STATE_INVALID", message, new Dictionary<string, object?> { ["state"] = state });

    private MenuTransition Stage(Loaded loaded, string state, string? reason)
    {
        var transition = new MenuTransition
        {
            Id = Guid.NewGuid(), MenuVersionId = loaded.Latest.Id,
            Sequence = (loaded.LatestTransitions.Count == 0 ? 0 : loaded.LatestTransitions.Max(t => t.Sequence)) + 1,
            State = state, Reason = reason, ActorUserId = actor.UserId, At = time.GetUtcNow()
        };
        repository.AddTransition(transition);
        return transition;
    }

    private MenuVersion StageVersion(
        Guid menuId, int version, Guid? priorId, string? reason, List<MenuDishRequest> dishes, DateTimeOffset now)
    {
        var row = new MenuVersion
        {
            Id = Guid.NewGuid(), MenuId = menuId, Version = version, Reason = reason, PriorVersionId = priorId,
            RecordedAt = now, RecordedByUserId = actor.UserId
        };
        repository.AddMenuVersion(row);
        repository.AddMenuItems(dishes.Select((d, i) => new MenuItem
        {
            Id = Guid.NewGuid(), MenuVersionId = row.Id, RecipeVersionId = d.RecipeVersionId!.Value,
            PlannedPortions = d.PlannedPortions, SortOrder = i
        }));
        repository.AddTransition(new MenuTransition
        {
            Id = Guid.NewGuid(), MenuVersionId = row.Id, Sequence = 1, State = MenuStates.Draft,
            ActorUserId = actor.UserId, At = now
        });
        return row;
    }

    private async Task SaveOrConflictAsync(int currentVersion, CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            // Unique (MenuId, Version), (MenuVersionId, Sequence) or (MealDayId): a concurrent writer won.
            throw new KitchenException(409, "MENU_VERSION_CONFLICT",
                "The menu was changed concurrently. Reload it and retry.",
                new Dictionary<string, object?> { ["currentVersion"] = currentVersion });
        }
    }

    private MenuTransitionResponse Response(
        Guid menuId, MenuVersion version, MenuTransition t, Guid? snapshotId = null, DateTimeOffset? publishedAt = null) =>
        new(menuId, version.Id, version.Version, t.State, t.At.ToVn(), t.ActorUserId, snapshotId, publishedAt);

    private static string? CleanReason(string? reason, bool required, ValidationErrors errors)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason is null && required) errors.Add("reason", "reason is required when adding a new menu version.");
        else if (reason is { Length: > MaxReasonLength }) errors.Add("reason", $"reason must be at most {MaxReasonLength} characters.");
        return reason;
    }

    private static void ValidateDishes(List<MenuDishRequest>? dishes, ValidationErrors errors)
    {
        if (dishes is null || dishes.Count == 0) { errors.Add("dishes", "At least one dish is required."); return; }
        if (dishes.Count > MaxDishes) errors.Add("dishes", $"A menu can have at most {MaxDishes} dishes.");

        for (var i = 0; i < dishes.Count; i++)
        {
            var d = dishes[i];
            if (d is null) { errors.Add($"dishes[{i}]", "Dish must not be null."); continue; }
            if (d.RecipeVersionId is null || d.RecipeVersionId == Guid.Empty)
                errors.Add($"dishes[{i}].recipeVersionId", "recipeVersionId is required.");
            if (d.PlannedPortions is < 1 or > 100_000)
                errors.Add($"dishes[{i}].plannedPortions", "plannedPortions must be between 1 and 100000.");
        }

        foreach (var dup in dishes.Where(d => d?.RecipeVersionId is { } id && id != Guid.Empty)
                     .GroupBy(d => d.RecipeVersionId!.Value).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add("dishes", $"Recipe version {dup} appears more than once.");
    }

    private async Task ResolveDishesAsync(List<MenuDishRequest> dishes, ValidationErrors errors, CancellationToken ct)
    {
        var found = (await repository.FindRecipeVersionsAsync(dishes.Select(d => d.RecipeVersionId!.Value).ToArray(), ct))
            .Select(r => r.Version.Id).ToHashSet();
        for (var i = 0; i < dishes.Count; i++)
            if (!found.Contains(dishes[i].RecipeVersionId!.Value))
                errors.Add($"dishes[{i}].recipeVersionId", "Recipe version not found.");
        errors.ThrowIfAny();
    }

    private async Task<MenuView> BuildViewAsync(Menu menu, MealDay day, int? version, CancellationToken ct)
    {
        var versions = (await repository.ListVersionsAsync(menu.Id, ct)).OrderBy(v => v.Version).ToList();
        var target = version is null ? versions.Last() : versions.FirstOrDefault(v => v.Version == version)
            ?? throw KitchenException.NotFound("MENU_VERSION_NOT_FOUND", $"Menu version {version} not found.");

        var views = await BuildVersionViewsAsync(versions, ct);
        var latest = views.Single(v => v.VersionId == target.Id);
        var publishedView = views.LastOrDefault(v => v.State == MenuStates.Published);
        return new MenuView(menu.Id, menu.MealDayId, day.Date, day.MealType, latest, publishedView?.VersionId, publishedView?.Version);
    }

    private async Task<List<MenuVersionView>> BuildVersionViewsAsync(IReadOnlyList<MenuVersion> versions, CancellationToken ct)
    {
        var ids = versions.Select(v => v.Id).ToArray();
        var items = await repository.ListItemsAsync(ids, ct);
        var transitions = await repository.ListTransitionsAsync(ids, ct);
        var snapshots = (await repository.ListSnapshotsAsync(ids, ct)).ToDictionary(s => s.MenuVersionId);
        var recipes = (await repository.FindRecipeVersionsAsync(items.Select(i => i.RecipeVersionId).Distinct().ToArray(), ct))
            .ToDictionary(r => r.Version.Id);

        return versions.OrderBy(v => v.Version).Select(v =>
        {
            var own = transitions.Where(t => t.MenuVersionId == v.Id).OrderBy(t => t.Sequence).ToList();
            return new MenuVersionView(
                v.Id, v.Version, v.PriorVersionId, StateOf(own), v.Reason, v.RecordedAt.ToVn(), v.RecordedByUserId,
                items.Where(i => i.MenuVersionId == v.Id).OrderBy(i => i.SortOrder).Select(i =>
                {
                    var r = recipes[i.RecipeVersionId];
                    return new MenuDishView(i.RecipeVersionId, r.Version.RecipeId, r.RecipeName, r.Version.Version, i.PlannedPortions);
                }).ToList(),
                own.Select(t => new MenuTransitionView(t.Sequence, t.State, t.Reason, t.ActorUserId, t.At.ToVn())).ToList(),
                snapshots.TryGetValue(v.Id, out var s) ? s.Id : null);
        }).ToList();
    }
}

using System.Reflection;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Features.Kitchen;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Api.Tests;

/// <summary>Creates a fake of any interface from per-method handlers; unhandled methods fail loudly.</summary>
public class K13FakeProxy : DispatchProxy
{
    public Dictionary<string, Func<object?[], object?>> Handlers { get; set; } = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (Handlers.TryGetValue(targetMethod!.Name, out var handler)) return handler(args ?? []);
        throw new NotSupportedException($"{targetMethod.DeclaringType?.Name}.{targetMethod.Name} is not faked.");
    }

    public static T Create<T>(Dictionary<string, Func<object?[], object?>> handlers) where T : class
    {
        var proxy = DispatchProxy.Create<T, K13FakeProxy>();
        ((K13FakeProxy)(object)proxy).Handlers = handlers;
        return proxy;
    }
}

/// <summary>One settled meal day as the estimate service sees it: a token and the latest count of each class.</summary>
internal sealed record K13Settlement(string Token, List<(Guid ClassId, int Applied, int Version, Guid SettlementId)> Classes);

internal sealed partial class K13World
{
    public readonly List<Supplier> SupplierRows = [];
    public readonly List<SupplierRevision> SupplierRevisions = [];
    public readonly List<IngredientSourceEntry> SourceEntries = [];
    public readonly List<IngredientSourceEntryVersion> EntryVersions = [];
    public readonly List<MealDay> MealDays = [];
    public readonly List<Menu> MenuRows = [];
    public readonly List<MenuVersion> MenuVersions = [];
    public readonly List<MenuItem> MenuItems = [];
    public readonly List<MenuTransition> Transitions = [];
    public readonly List<MenuNutritionSnapshot> Snapshots = [];
    public readonly List<IngredientEstimate> EstimateRows = [];
    public readonly Dictionary<Guid, K13Settlement> Settlements = new();

    public bool ParentLinked = true;
    public Func<DateOnly, Guid?> CurrentClass = _ => Guid.NewGuid();

    public ISupplierService SupplierSvc = null!;
    public IIngredientSourceService SourceSvc = null!;
    public IMenuService MenuSvc = null!;
    public IIngredientEstimateService EstimateSvc = null!;
    public INutritionReferenceService ReferenceSvc = null!;

    partial void CommitExtra(object item)
    {
        switch (item)
        {
            case Supplier x: SupplierRows.Add(x); break;
            case SupplierRevision x: SupplierRevisions.Add(x); break;
            case IngredientSourceEntry x: SourceEntries.Add(x); break;
            case IngredientSourceEntryVersion x: EntryVersions.Add(x); break;
            case Menu x: MenuRows.Add(x); break;
            case MenuVersion x: MenuVersions.Add(x); break;
            case MenuItem x: MenuItems.Add(x); break;
            case MenuTransition x: Transitions.Add(x); break;
            case MenuNutritionSnapshot x: Snapshots.Add(x); break;
            case IngredientEstimate x: EstimateRows.Add(x); break;
        }
    }

    partial void InitBe1(IUnitOfWork uow, IIngredientService ingredients, INutritionRepository nutritionRepository, INutritionService nutritionService)
    {
        SupplierSvc = new SupplierService(new SourceRepo(this), Clock, uow, Actor);
        SourceSvc = new IngredientSourceService(new SourceRepo(this), Clock, uow, Actor);
        ReferenceSvc = new NutritionReferenceService(nutritionRepository, Clock, uow, Actor);

        var parentLinks = K13FakeProxy.Create<IParentLinkRepository>(new()
        {
            ["LinkedAsync"] = _ => Task.FromResult(ParentLinked),
            ["CurrentClassAsync"] = a => Task.FromResult(CurrentClass((DateOnly)a[1]!))
        });
        var menuRepo = new MenuRepo(this);
        MenuSvc = new MenuService(menuRepo, nutritionService, parentLinks, Clock, uow, Actor);

        var settlementService = K13FakeProxy.Create<IKitchenSettlementService>(new()
        {
            ["GetSettlementDiffAsync"] = a =>
            {
                var day = (Guid)a[0]!;
                if (!Settlements.TryGetValue(day, out var s))
                    throw new KitchenException(404, "SETTLEMENT_NOT_FOUND", "No settlement for this meal day.");
                var classes = s.Classes.Select(c => new KitchenClassDiff(
                    c.ClassId, "Class", c.Applied, c.Applied, 0, 0, c.Version, [], [], true)).ToList();
                var total = classes.Sum(c => c.AppliedCount);
                return Task.FromResult(new KitchenSettlementDiffResponse(
                    day, DateOnly.FromDateTime(Clock.GetUtcNow().Date), "Lunch", Clock.GetUtcNow(), Clock.GetUtcNow(),
                    total, total, 0, 0, classes, s.Token, false));
            }
        });
        var settlementRepository = K13FakeProxy.Create<IKitchenSettlementRepository>(new()
        {
            ["FindSettledMealDayWithDetailsAsync"] = a =>
            {
                var day = (Guid)a[0]!;
                var meal = new MealDay { Id = day, MealType = "Lunch" };
                if (Settlements.TryGetValue(day, out var s))
                    meal.Settlements = s.Classes.Select(c => new PortionSettlement
                    {
                        Id = c.SettlementId, MealDayId = day, ClassId = c.ClassId, Version = c.Version, Count = c.Applied, SettledBy = "t"
                    }).ToList();
                return Task.FromResult<MealDay?>(meal);
            }
        });
        EstimateSvc = new IngredientEstimateService(
            menuRepo, nutritionRepository, settlementService, settlementRepository, new EstimateRepo(this), Clock, uow, Actor);
    }

    public Guid AddMealDay(DateOnly date, string mealType = "Lunch", bool cancelled = false)
    {
        var day = new MealDay { Date = date, MealType = mealType, IsCancelled = cancelled };
        MealDays.Add(day);
        return day.Id;
    }

    public void Settle(Guid mealDayId, string token, params (Guid ClassId, int Applied)[] classes) =>
        Settlements[mealDayId] = new K13Settlement(token,
            classes.Select(c => (c.ClassId, c.Applied, 1, Guid.NewGuid())).ToList());

    /// <summary>Walks a new menu through draft, submitted, approved and published; returns its ids.</summary>
    public async Task<(Guid MenuId, Guid MealDayId, Guid VersionId)> PublishedMenu(
        Guid recipeVersionId, DateOnly date, string mealType = "Lunch", string? ageGroup = null)
    {
        var day = AddMealDay(date, mealType);
        var created = await MenuSvc.CreateAsync(new CreateMenuRequest(day, [new MenuDishRequest(recipeVersionId, null)], null), default);
        await MenuSvc.SubmitAsync(created.Id, new MenuTransitionRequest(1), default);
        await MenuSvc.ReviewAsync(created.Id, new MenuReviewRequest(1, "approve", null), default);
        await MenuSvc.PublishAsync(created.Id, new MenuPublishRequest(1, ageGroup), default);
        return (created.Id, day, created.VersionId);
    }

    private sealed class SourceRepo(K13World w) : ISourceRepository
    {
        private IEnumerable<SupplierRevision> LatestSuppliers() =>
            w.SupplierRevisions.GroupBy(r => r.SupplierId).Select(g => g.OrderByDescending(r => r.Version).First());

        private IEnumerable<SupplierRevision> FilterSuppliers(string? search) =>
            LatestSuppliers().Where(r => string.IsNullOrWhiteSpace(search) || r.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

        public Task<SupplierRevision?> FindSupplierRevisionAsync(Guid revisionId, CancellationToken ct) =>
            Task.FromResult(w.SupplierRevisions.SingleOrDefault(r => r.Id == revisionId));

        public Task<SupplierRevision?> GetLatestSupplierRevisionAsync(Guid supplierId, CancellationToken ct) =>
            Task.FromResult(w.SupplierRevisions.Where(r => r.SupplierId == supplierId).OrderByDescending(r => r.Version).FirstOrDefault());

        public Task<List<SupplierRevision>> ListSupplierRevisionsAsync(Guid supplierId, CancellationToken ct) =>
            Task.FromResult(w.SupplierRevisions.Where(r => r.SupplierId == supplierId).ToList());

        public Task<int> CountSuppliersAsync(string? search, CancellationToken ct) => Task.FromResult(FilterSuppliers(search).Count());

        public Task<List<SupplierRevision>> ListLatestSupplierRevisionsAsync(string? search, int skip, int size, CancellationToken ct) =>
            Task.FromResult(FilterSuppliers(search).OrderBy(r => r.Name).ThenBy(r => r.SupplierId).Skip(skip).Take(size).ToList());

        public Task<List<SupplierRevision>> GetSupplierRevisionsByIdAsync(Guid[] revisionIds, CancellationToken ct) =>
            Task.FromResult(w.SupplierRevisions.Where(r => revisionIds.Contains(r.Id)).ToList());

        public Task<Ingredient?> FindIngredientAsync(Guid ingredientId, CancellationToken ct) =>
            Task.FromResult(w.Ingredient_.SingleOrDefault(i => i.Id == ingredientId));

        public Task<IngredientSourceEntry?> FindSourceEntryAsync(Guid entryId, CancellationToken ct) =>
            Task.FromResult(w.SourceEntries.SingleOrDefault(e => e.Id == entryId));

        public Task<IngredientSourceEntryVersion?> GetLatestEntryVersionAsync(Guid entryId, CancellationToken ct) =>
            Task.FromResult(w.EntryVersions.Where(v => v.SourceEntryId == entryId).OrderByDescending(v => v.Version).FirstOrDefault());

        public Task<List<IngredientSourceEntryVersion>> ListEntryVersionsAsync(Guid entryId, CancellationToken ct) =>
            Task.FromResult(w.EntryVersions.Where(v => v.SourceEntryId == entryId).ToList());

        private IEnumerable<SourceEntryRow> Latest(Guid? ingredientId, DateOnly? from, DateOnly? to) =>
            w.SourceEntries.Where(e => ingredientId is null || e.IngredientId == ingredientId)
                .Select(e =>
                {
                    var v = w.EntryVersions.Where(x => x.SourceEntryId == e.Id).OrderByDescending(x => x.Version).First();
                    return new SourceEntryRow(e, v, w.Ingredient_.Single(i => i.Id == e.IngredientId).Name,
                        w.SupplierRevisions.Single(s => s.Id == v.SupplierRevisionId));
                })
                .Where(r => (from is null || r.Version.ReferenceDate >= from) && (to is null || r.Version.ReferenceDate <= to));

        public Task<int> CountEntriesAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Task.FromResult(Latest(ingredientId, from, to).Count());

        public Task<List<SourceEntryRow>> ListLatestEntriesAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, int skip, int size, CancellationToken ct) =>
            Task.FromResult(Latest(ingredientId, from, to).OrderByDescending(r => r.Version.ReferenceDate).ThenBy(r => r.Entry.Id).Skip(skip).Take(size).ToList());

        public void AddSupplier(Supplier value) => w._pending.Add(value);
        public void AddSupplierRevision(SupplierRevision value) => w._pending.Add(value);
        public void AddSourceEntry(IngredientSourceEntry value) => w._pending.Add(value);
        public void AddSourceEntryVersion(IngredientSourceEntryVersion value) => w._pending.Add(value);
    }

    private sealed class MenuRepo(K13World w) : IMenuRepository
    {
        public Task<MealDay?> FindMealDayAsync(Guid mealDayId, CancellationToken ct) =>
            Task.FromResult(w.MealDays.SingleOrDefault(d => d.Id == mealDayId));

        public Task<Menu?> FindMenuAsync(Guid menuId, CancellationToken ct) =>
            Task.FromResult(w.MenuRows.SingleOrDefault(m => m.Id == menuId));

        public Task<Menu?> FindMenuByMealDayAsync(Guid mealDayId, CancellationToken ct) =>
            Task.FromResult(w.MenuRows.SingleOrDefault(m => m.MealDayId == mealDayId));

        public Task<MenuVersion?> FindMenuVersionAsync(Guid versionId, CancellationToken ct) =>
            Task.FromResult(w.MenuVersions.SingleOrDefault(v => v.Id == versionId));

        public Task<List<MenuVersion>> ListVersionsAsync(Guid menuId, CancellationToken ct) =>
            Task.FromResult(w.MenuVersions.Where(v => v.MenuId == menuId).ToList());

        public Task<List<MenuItem>> ListItemsAsync(Guid[] versionIds, CancellationToken ct) =>
            Task.FromResult(w.MenuItems.Where(i => versionIds.Contains(i.MenuVersionId)).ToList());

        public Task<List<MenuTransition>> ListTransitionsAsync(Guid[] versionIds, CancellationToken ct) =>
            Task.FromResult(w.Transitions.Where(t => versionIds.Contains(t.MenuVersionId)).ToList());

        public Task<List<MenuNutritionSnapshot>> ListSnapshotsAsync(Guid[] versionIds, CancellationToken ct) =>
            Task.FromResult(w.Snapshots.Where(s => versionIds.Contains(s.MenuVersionId)).ToList());

        public Task<List<RecipeVersionRef>> FindRecipeVersionsAsync(Guid[] recipeVersionIds, CancellationToken ct) =>
            Task.FromResult(w.RecipeVersions.Where(v => recipeVersionIds.Contains(v.Id))
                .Select(v => new RecipeVersionRef(v, w.Recipe_.Single(r => r.Id == v.RecipeId).Name)).ToList());

        private IEnumerable<MenuDayRow> InRange(DateOnly? from, DateOnly? to) =>
            w.MenuRows.Select(m => new MenuDayRow(m, w.MealDays.Single(d => d.Id == m.MealDayId)))
                .Where(r => (from is null || r.MealDay.Date >= from) && (to is null || r.MealDay.Date <= to));

        public Task<int> CountMenusAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Task.FromResult(InRange(from, to).Count());

        public Task<List<MenuDayRow>> ListMenusAsync(DateOnly? from, DateOnly? to, int skip, int size, CancellationToken ct) =>
            Task.FromResult(InRange(from, to).OrderBy(r => r.MealDay.Date).ThenBy(r => r.MealDay.MealType).ThenBy(r => r.Menu.Id)
                .Skip(skip).Take(size).ToList());

        public void AddMenu(Menu value) => w._pending.Add(value);
        public void AddMenuVersion(MenuVersion value) => w._pending.Add(value);
        public void AddMenuItems(IEnumerable<MenuItem> value) => w._pending.AddRange(value);
        public void AddTransition(MenuTransition value) => w._pending.Add(value);
        public void AddSnapshot(MenuNutritionSnapshot value) => w._pending.Add(value);
    }

    private sealed class EstimateRepo(K13World w) : IIngredientEstimateRepository
    {
        public Task<IngredientEstimate?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(w.EstimateRows.SingleOrDefault(e => e.Id == id));

        public void Add(IngredientEstimate value) => w._pending.Add(value);
    }
}

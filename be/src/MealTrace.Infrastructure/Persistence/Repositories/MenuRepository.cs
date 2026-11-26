using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class MenuRepository(MealTraceDbContext db) : IMenuRepository
{
    public async Task<MealDay?> FindMealDayAsync(Guid mealDayId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MealDays.AsNoTracking().SingleOrDefaultAsync(d => d.Id == mealDayId, ct);
        });
    }
    public async Task<Menu?> FindMenuAsync(Guid menuId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Menus.AsNoTracking().SingleOrDefaultAsync(m => m.Id == menuId, ct);
        });
    }
    public async Task<Menu?> FindMenuByMealDayAsync(Guid mealDayId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Menus.AsNoTracking().SingleOrDefaultAsync(m => m.MealDayId == mealDayId, ct);
        });
    }
    public async Task<MenuVersion?> FindMenuVersionAsync(Guid versionId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MenuVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, ct);
        });
    }
    public async Task<List<MenuVersion>> ListVersionsAsync(Guid menuId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MenuVersions.AsNoTracking().Where(v => v.MenuId == menuId).ToListAsync(ct);
        });
    }
    public async Task<List<MenuItem>> ListItemsAsync(Guid[] versionIds, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MenuItems.AsNoTracking().Where(i => versionIds.Contains(i.MenuVersionId)).ToListAsync(ct);
        });
    }
    public async Task<List<MenuTransition>> ListTransitionsAsync(Guid[] versionIds, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MenuTransitions.AsNoTracking().Where(t => versionIds.Contains(t.MenuVersionId)).ToListAsync(ct);
        });
    }
    public async Task<List<MenuNutritionSnapshot>> ListSnapshotsAsync(Guid[] versionIds, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.MenuNutritionSnapshots.AsNoTracking().Where(s => versionIds.Contains(s.MenuVersionId)).ToListAsync(ct);
        });
    }
    public async Task<List<RecipeVersionRef>> FindRecipeVersionsAsync(Guid[] recipeVersionIds, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var rows = await (
                from v in db.RecipeVersions.AsNoTracking()
                join r in db.Recipes.AsNoTracking() on v.RecipeId equals r.Id
                where recipeVersionIds.Contains(v.Id)
                select new { Version = v, RecipeName = r.Name }).ToListAsync(ct);
            return rows.Select(x => new RecipeVersionRef(x.Version, x.RecipeName)).ToList();
        });
    }
    public async Task<int> CountMenusAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await InRange(from, to).CountAsync(ct);
        });
    }
    public async Task<List<MenuDayRow>> ListMenusAsync(DateOnly? from, DateOnly? to, int skip, int size, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var rows = await InRange(from, to)
                .OrderBy(x => x.Day.Date).ThenBy(x => x.Day.MealType).ThenBy(x => x.Menu.Id)
                .Skip(skip).Take(size).ToListAsync(ct);
            return rows.Select(x => new MenuDayRow(x.Menu, x.Day)).ToList();
        });
    }

    private IQueryable<MenuDay> InRange(DateOnly? from, DateOnly? to)
    {
        var query =
            from m in db.Menus.AsNoTracking()
            join d in db.MealDays.AsNoTracking() on m.MealDayId equals d.Id
            select new MenuDay { Menu = m, Day = d };
        if (from is { } start) query = query.Where(x => x.Day.Date >= start);
        if (to is { } end) query = query.Where(x => x.Day.Date <= end);
        return query;
    }

    private sealed class MenuDay
    {
        public Menu Menu { get; set; } = null!;
        public MealDay Day { get; set; } = null!;
    }

    public void AddMenu(Menu value) => db.Menus.Add(value);
    public void AddMenuVersion(MenuVersion value) => db.MenuVersions.Add(value);
    public void AddMenuItems(IEnumerable<MenuItem> value) => db.MenuItems.AddRange(value);
    public void AddTransition(MenuTransition value) => db.MenuTransitions.Add(value);
    public void AddSnapshot(MenuNutritionSnapshot value) => db.MenuNutritionSnapshots.Add(value);
}

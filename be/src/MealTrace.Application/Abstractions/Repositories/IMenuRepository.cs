using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for menus, their versions, transitions and frozen nutrition. Mutations do not commit.</summary>
public interface IMenuRepository
{
    Task<MealDay?> FindMealDayAsync(Guid mealDayId, CancellationToken ct);
    Task<Menu?> FindMenuAsync(Guid menuId, CancellationToken ct);
    Task<Menu?> FindMenuByMealDayAsync(Guid mealDayId, CancellationToken ct);
    Task<MenuVersion?> FindMenuVersionAsync(Guid versionId, CancellationToken ct);
    Task<List<MenuVersion>> ListVersionsAsync(Guid menuId, CancellationToken ct);
    Task<List<MenuItem>> ListItemsAsync(Guid[] versionIds, CancellationToken ct);
    Task<List<MenuTransition>> ListTransitionsAsync(Guid[] versionIds, CancellationToken ct);
    Task<List<MenuNutritionSnapshot>> ListSnapshotsAsync(Guid[] versionIds, CancellationToken ct);
    Task<List<RecipeVersionRef>> FindRecipeVersionsAsync(Guid[] recipeVersionIds, CancellationToken ct);
    Task<int> CountMenusAsync(DateOnly? from, DateOnly? to, CancellationToken ct);
    /// <summary>Menus whose meal day falls in the range, ordered by date, meal type, id.</summary>
    Task<List<MenuDayRow>> ListMenusAsync(DateOnly? from, DateOnly? to, int skip, int size, CancellationToken ct);

    void AddMenu(Menu value);
    void AddMenuVersion(MenuVersion value);
    void AddMenuItems(IEnumerable<MenuItem> value);
    void AddTransition(MenuTransition value);
    void AddSnapshot(MenuNutritionSnapshot value);
}

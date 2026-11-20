using MealTrace.Domain.Entities;

namespace MealTrace.Application.Models.Persistence;

public sealed record SourceEntryRow(
    IngredientSourceEntry Entry, IngredientSourceEntryVersion Version, string IngredientName, SupplierRevision Supplier);

public sealed record RecipeVersionRef(RecipeVersion Version, string RecipeName);

public sealed record RecipeCatalogRow(Recipe Recipe, RecipeVersion Latest);

public sealed record MenuDayRow(Menu Menu, MealDay MealDay);

namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>Same fields as PagedResult plus the instants used to pick each ingredient's effective nutrient version.</summary>
public sealed record IngredientListPage(
    IReadOnlyList<IngredientListItem> Items,
    int Page,
    int PageSize,
    int Total,
    DateTimeOffset ValidAt,
    DateTimeOffset KnownAt);

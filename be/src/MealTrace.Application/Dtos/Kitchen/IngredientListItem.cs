namespace MealTrace.Application.Dtos.Kitchen;

public sealed record IngredientListItem(Guid Id, string Name, string Unit, NutrientSnapshot? Nutrition);

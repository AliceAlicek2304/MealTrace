namespace MealTrace.Application.Dtos.Meals;

internal sealed record DecisionPage(List<MealDecision> Items, int Total, List<Room> Classes);

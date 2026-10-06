namespace MealTrace.Application.Dtos.Meals;

public sealed record DecisionPage(List<MealDecision> Items, int Total, List<Room> Classes);

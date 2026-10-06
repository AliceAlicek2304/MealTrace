namespace MealTrace.Application.Dtos.Workflow;

public sealed record CreateMealDay(DateOnly Date, string MealType, string SchoolYear);

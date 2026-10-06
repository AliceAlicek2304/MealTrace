namespace MealTrace.Application.Dtos.Calendar;

public sealed record PlanItem(DateOnly Date, string MealType, string Action, Guid? MealId = null);

namespace MealTrace.Application.Models.Persistence;

public sealed record MealDayListFilter(DateOnly? Date, string? Status, string? Search);

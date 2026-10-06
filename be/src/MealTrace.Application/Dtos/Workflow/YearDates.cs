namespace MealTrace.Application.Dtos.Workflow;

public sealed record YearDates(DateOnly StartDate, DateOnly EndDate, string? SourceYearCode = null);

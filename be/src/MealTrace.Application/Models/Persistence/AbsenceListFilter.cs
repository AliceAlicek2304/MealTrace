namespace MealTrace.Application.Models.Persistence;

public sealed record AbsenceListFilter(Guid UserId, Guid? StudentId, string? Status, string? Search, DateOnly Today);

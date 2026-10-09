namespace MealTrace.Application.Dtos.Workflow;

public sealed record CreateStudent(string FullName, Guid ClassId, string? StudentCode = null, DateOnly? StartDate = null,
    DateOnly? DateOfBirth = null, string? Gender = null);

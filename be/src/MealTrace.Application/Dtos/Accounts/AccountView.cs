namespace MealTrace.Application.Dtos.Accounts;

public sealed record AccountView(Guid Id, string FullName, string Email, string[] Roles, string Status,
    Guid[] ClassIds, Guid[] StudentIds, DateOnly? InspectorAccessUntil, string? PhoneNumber = null);

namespace MealTrace.Application.Dtos.Auth;

public sealed record CurrentUser(Guid Id, string FullName, string Email, string[] Roles, DateOnly? InspectorAccessUntil, string? PhoneNumber = null);

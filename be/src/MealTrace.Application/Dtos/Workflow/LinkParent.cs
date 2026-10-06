namespace MealTrace.Application.Dtos.Workflow;

public sealed record LinkParent(string? Email, string? FullName, string? PhoneNumber = null, bool SendRegistrationNotification = false);

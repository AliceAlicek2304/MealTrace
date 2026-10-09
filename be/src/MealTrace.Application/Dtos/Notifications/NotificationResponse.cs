namespace MealTrace.Application.Dtos.Notifications;

public sealed record NotificationResponse(string Status, string? ProviderMessageId, string Recipient, string Message);

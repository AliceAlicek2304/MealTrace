namespace MealTrace.Application.Dtos.Notifications;

public sealed record NotificationSettingsResponse(bool Enabled, string Channel, bool TemplateOnly);

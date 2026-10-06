namespace MealTrace.Application.Abstractions.Notifications;

public enum NotificationOutcome { Accepted, Failed, Unknown, Disabled }
public sealed record NotificationSendResult(NotificationOutcome Outcome, string? ProviderMessageId, string Message);
public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(string number, CancellationToken ct, string? text = null);
}

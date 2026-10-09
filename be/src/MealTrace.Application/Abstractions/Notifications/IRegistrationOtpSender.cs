namespace MealTrace.Application.Abstractions.Notifications;

// Must send the supplied code, never a generic demo notification.
public interface IRegistrationOtpSender
{
    Task<NotificationSendResult> SendAsync(string number, string code, CancellationToken ct);
}

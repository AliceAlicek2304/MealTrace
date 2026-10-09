using MealTrace.Application.Abstractions.Notifications;

namespace MealTrace.Infrastructure.Notifications;

public sealed class PriorityWhatsAppSender(INotificationSender primary, INotificationSender fallback) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(string number, CancellationToken ct, string? text = null)
    {
        var result = await primary.SendAsync(number, ct, text);
        // Accepted and uncertain outcomes must never be sent again through another provider.
        if (result.Outcome is not (NotificationOutcome.Failed or NotificationOutcome.Disabled)) return result;
        ct.ThrowIfCancellationRequested();
        var backup = await fallback.SendAsync(number, ct, text);
        return backup with { Message = "Vonage chưa gửi được; đã thử Twilio dự phòng. " + backup.Message };
    }
}

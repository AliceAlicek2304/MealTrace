using MealTrace.Application.Abstractions.Notifications;

namespace MealTrace.Application.Dtos.Notifications;

public sealed record NotificationResponse(string Status, string? ProviderMessageId, string Recipient, string Message)
{
    public static NotificationResponse FromSendResult(NotificationSendResult result, string recipient)
    {
        var message = result.Outcome switch
        {
            NotificationOutcome.Accepted => "Yêu cầu gửi tin đã được tiếp nhận. Vui lòng kiểm tra WhatsApp.",
            NotificationOutcome.Failed => "Chưa gửi được tin nhắn. Vui lòng thử lại sau.",
            NotificationOutcome.Disabled => "Chưa thể gửi thông báo. Vui lòng liên hệ nhà trường.",
            _ => "Chưa xác định được kết quả gửi tin. Vui lòng kiểm tra WhatsApp trước khi gửi lại."
        };
        return new(result.Outcome.ToString().ToUpperInvariant(), result.ProviderMessageId, recipient, message);
    }
}

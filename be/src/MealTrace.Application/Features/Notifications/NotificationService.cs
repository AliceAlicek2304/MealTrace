using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Dtos.Notifications;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Notifications;

public sealed class NotificationService(INotificationSender sender, NotificationPolicy policy, NotificationSendGate gate, ICurrentActor actor)
{
    public async Task<Result<NotificationResponse>> SendTestAsync(CancellationToken ct)
    {
        if (!actor.IsInRole(RoleNames.Admin)) return Result.Forbidden();
        var phone = PhoneNumbers.Normalize(policy.TestNumber);
        if (!policy.Enabled || phone is null)
            return Result.Success(new NotificationResponse("DISABLED", null, "", "Chưa bật thông báo hoặc chưa cấu hình số tester hợp lệ."));
        if (!gate.TryBegin()) return Result.Conflict("Đã có yêu cầu gửi thông báo thử. Đợi ít nhất 60 giây; kiểm tra Logs trước khi gửi lại.");
        try
        {
            var result = await sender.SendAsync("84" + phone[1..], ct,
                "MEALTRACE | TIN NHẮN THỬ NGHIỆM\n\nĐây là tin nhắn kiểm tra kết nối WhatsApp của MealTrace.\nQuý phụ huynh không cần thực hiện thao tác nào.\n\nTrân trọng,\nMealTrace");
            return Result.Success(new NotificationResponse(result.Outcome.ToString().ToUpperInvariant(), result.ProviderMessageId, "******" + phone[^4..], result.Message));
        }
        finally { gate.End(); }
    }
}

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
            return Result.Success(new NotificationResponse("DISABLED", null, "", "Tính năng gửi thông báo hiện chưa sẵn sàng."));
        if (!gate.TryBegin()) return Result.Conflict("Vui lòng đợi 60 giây trước khi gửi lại thông báo.");
        try
        {
            var result = await sender.SendAsync("84" + phone[1..], ct,
                "MEALTRACE | TIN NHẮN THỬ NGHIỆM\n\nĐây là tin nhắn kiểm tra kết nối WhatsApp của MealTrace.\nQuý phụ huynh không cần thực hiện thao tác nào.\n\nTrân trọng,\nMealTrace");
            return Result.Success(NotificationResponse.FromSendResult(result, "******" + phone[^4..]));
        }
        finally { gate.End(); }
    }
}

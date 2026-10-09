using MealTrace.Application.Abstractions.Notifications;

namespace MealTrace.Infrastructure.Notifications;

public sealed class RegistrationWhatsAppOtpSender(VonageWhatsAppSender vonage, TwilioWhatsAppSender twilio, bool preferVonage) : IRegistrationOtpSender
{
    public async Task<NotificationSendResult> SendAsync(string number, string code, CancellationToken ct)
    {
        var text = $"MEALTRACE | XÁC MINH SỐ ĐIỆN THOẠI\n\nMã OTP đăng ký phụ huynh: {code}\n\nMã có hiệu lực 5 phút. Không chia sẻ mã này với bất kỳ ai.\nNếu bạn không yêu cầu đăng ký, vui lòng bỏ qua tin nhắn.";
        if (!preferVonage) return await twilio.SendTextAsync(number, text, ct);
        var result = await vonage.SendAsync(number, ct, text);
        if (result.Outcome is not (NotificationOutcome.Failed or NotificationOutcome.Disabled)) return result;
        ct.ThrowIfCancellationRequested();
        return await twilio.SendTextAsync(number, text, ct);
    }
}

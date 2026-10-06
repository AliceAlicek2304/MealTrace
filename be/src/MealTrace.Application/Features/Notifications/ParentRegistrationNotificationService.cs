using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Dtos.Notifications;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Notifications;

public sealed class ParentRegistrationNotificationService(INotificationSender sender, NotificationPolicy policy, NotificationSendGate gate)
{
    public async Task<NotificationResponse> SendAsync(string? phoneNumber, string? childName = null, string? temporaryPassword = null)
    {
        var phone = PhoneNumbers.Normalize(phoneNumber);
        if (!policy.Enabled || phone is null)
            return new("DISABLED", null, "", "Hồ sơ đã lưu; thông báo chưa bật hoặc phụ huynh chưa có SĐT.");
        // Trial POC: never send credentials to an unapproved destination.
        if (phone != PhoneNumbers.Normalize(policy.TestNumber))
            return new("DISABLED", null, "******" + phone[^4..], "Hồ sơ đã lưu; giai đoạn thử chỉ gửi tới số tester cấu hình.");
        if (!gate.TryBegin())
            return new("RATE_LIMITED", null, "******" + phone[^4..], "Hồ sơ đã lưu; chưa gửi thông báo vì đang trong khoảng giới hạn 60 giây.");
        try
        {
            // The commit succeeded. Complete this bounded send even if the browser disconnects.
            var child = string.IsNullOrWhiteSpace(childName) ? "trẻ" : childName.Trim();
            var loginInstructions = temporaryPassword is null
                ? "Vui lòng đăng nhập bằng mật khẩu hiện tại của Quý phụ huynh.\nNếu quên mật khẩu, vui lòng liên hệ quản trị viên để được hỗ trợ."
                : $"Mật khẩu tạm: {temporaryPassword}\n\nVui lòng đổi mật khẩu sau khi đăng nhập lần đầu và không chia sẻ thông tin đăng nhập.";
            var content = $"MEALTRACE | THÔNG BÁO ĐĂNG KÝ\n\n"
                + $"Kính gửi Quý phụ huynh,\nHồ sơ của {child} đã được đăng ký trên MealTrace và liên kết với tài khoản của Quý phụ huynh.\n\n"
                + $"THÔNG TIN ĐĂNG NHẬP\nTài khoản (SĐT): {phone}\n{loginInstructions}\n\n"
                + "Trân trọng,\nMealTrace";
            var result = await sender.SendAsync("84" + phone[1..], CancellationToken.None, content);
            return new(result.Outcome.ToString().ToUpperInvariant(), result.ProviderMessageId, "******" + phone[^4..], result.Message);
        }
        catch (Exception)
        {
            // Keep registration successful and never expose credentials in error payloads.
            return new("UNKNOWN", null, "******" + phone[^4..], "Hồ sơ đã lưu; chưa xác định kết quả thông báo. Kiểm tra Logs của nhà cung cấp trước khi gửi lại.");
        }
        finally { gate.End(); }
    }
}

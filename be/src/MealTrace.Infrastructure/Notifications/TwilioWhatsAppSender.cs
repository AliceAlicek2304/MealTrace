using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MealTrace.Application.Abstractions.Notifications;

namespace MealTrace.Infrastructure.Notifications;

public sealed record TwilioWhatsAppSettings(string? AccountSid, string? AuthToken, string? From, string? ContentSid);

// Trial sends a fixed approved template; never substitute registration credentials into it.
public sealed class TwilioWhatsAppSender(HttpClient client, TwilioWhatsAppSettings settings) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(string number, CancellationToken ct, string? text = null)
    {
        if (!Matches(settings.AccountSid, @"^AC[0-9a-fA-F]{32}$") || string.IsNullOrWhiteSpace(settings.AuthToken)
            || !Matches(settings.From, @"^\+[1-9][0-9]{7,14}$") || !Matches(number, @"^[1-9][0-9]{7,14}$")
            || !Matches(settings.ContentSid, @"^HX[0-9a-fA-F]{32}$"))
            return new(NotificationOutcome.Disabled, null, "Chưa cấu hình Twilio WhatsApp trial hợp lệ.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{settings.AccountSid}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.AccountSid}:{settings.AuthToken}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = "whatsapp:+" + number,
            ["From"] = "whatsapp:" + settings.From,
            ["ContentSid"] = settings.ContentSid!
        });
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout
                    ? Unknown() : new(NotificationOutcome.Failed, null, "Twilio từ chối WhatsApp. Kiểm tra tester đã kết nối, mẫu và hạn mức trong Logs.");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("sid", out var sid) || sid.ValueKind != JsonValueKind.String
                || !Matches(sid.GetString(), @"^(SM|MM)[0-9a-fA-F]{32}$")
                || !body.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String) return Unknown();
            if (status.GetString() is "failed" or "undelivered" or "canceled")
                return new(NotificationOutcome.Failed, sid.GetString(), "Twilio báo WhatsApp thất bại; xem Logs trước khi gửi lại.");
            if (status.GetString() is not ("accepted" or "queued" or "sending" or "sent" or "delivered" or "read")) return Unknown();
            return new(NotificationOutcome.Accepted, sid.GetString(),
                "Twilio đã nhận WhatsApp mẫu thử. Mẫu cảnh báo số dư là nội dung demo, không phải số dư thật; chưa chứa tên trẻ/tài khoản/mật khẩu. Kiểm tra WhatsApp để xác nhận nhận tin.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unknown(); }
        catch (HttpRequestException) { return Unknown(); }
        catch (JsonException) { return Unknown(); }
    }

    private static bool Matches(string? value, string pattern) => value is not null && Regex.IsMatch(value, pattern);
    private static NotificationSendResult Unknown() => new(NotificationOutcome.Unknown, null,
        "Chưa xác định kết quả WhatsApp. Kiểm tra Twilio Logs trước khi gửi lại; không tự retry.");
}

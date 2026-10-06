using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MealTrace.Application.Abstractions.Notifications;

namespace MealTrace.Infrastructure.Notifications;

public sealed record VonageWhatsAppSettings(string? ApiKey, string? ApiSecret, string? From);

public sealed class VonageWhatsAppSender(HttpClient client, VonageWhatsAppSettings settings) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(string number, CancellationToken ct, string? text = null)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || settings.ApiKey.Contains(':') || string.IsNullOrWhiteSpace(settings.ApiSecret)
            || !ValidNumber(settings.From) || !ValidNumber(number))
            return new(NotificationOutcome.Disabled, null, "Chưa cấu hình Vonage WhatsApp sandbox hợp lệ.");
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4096)
            return new(NotificationOutcome.Failed, null, "Nội dung WhatsApp trống hoặc quá dài.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://messages-sandbox.nexmo.com/v1/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ApiKey}:{settings.ApiSecret}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new { from = settings.From, to = number, message_type = "text", text, channel = "whatsapp" });
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout
                    ? Unknown() : new(NotificationOutcome.Failed, null, "Vonage từ chối WhatsApp; kiểm tra sandbox, xác thực và cửa sổ hội thoại 24 giờ.");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("message_uuid", out var id)
                || id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var uuid)) return Unknown();
            return new(NotificationOutcome.Accepted, uuid.ToString(), "Vonage đã nhận tin WhatsApp tùy chỉnh; kiểm tra điện thoại để xác nhận nhận tin.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unknown(); }
        catch (HttpRequestException) { return Unknown(); }
        catch (JsonException) { return Unknown(); }
    }

    private static bool ValidNumber(string? number) => number is not null && Regex.IsMatch(number, @"\A[1-9][0-9]{7,14}\z");
    private static NotificationSendResult Unknown() => new(NotificationOutcome.Unknown, null,
        "Chưa rõ kết quả Vonage; không tự chuyển nhà cung cấp để tránh gửi trùng. Kiểm tra Logs trước khi gửi lại.");
}

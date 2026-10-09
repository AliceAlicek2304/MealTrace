using System.Net;
using System.Text.Json;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Infrastructure.Notifications;

namespace MealTrace.Api.Tests;

public sealed class RegistrationWhatsAppOtpSenderTests
{
    [Theory]
    [InlineData(HttpStatusCode.Accepted, false)]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task FallbackSendsActualOtpOnlyOnDefinitePrimaryRejection(HttpStatusCode primaryStatus, bool expectedFallback)
    {
        var backupCalls = 0;
        using var primaryHttp = new HttpClient(new Handler(async request =>
        {
            var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Contains("012345", payload.RootElement.GetProperty("text").GetString());
            return new(primaryStatus) { Content = new StringContent("{\"message_uuid\":\"11111111-1111-1111-1111-111111111111\"}") };
        }));
        using var backupHttp = new HttpClient(new Handler(async request =>
        {
            backupCalls++;
            var body = Uri.UnescapeDataString(await request.Content!.ReadAsStringAsync());
            Assert.Contains("012345", body);
            Assert.Contains("Body=", body);
            Assert.DoesNotContain("ContentSid=", body);
            return new(HttpStatusCode.Created) { Content = new StringContent("{\"sid\":\"SM11111111111111111111111111111111\",\"status\":\"queued\"}") };
        }));
        var sender = new RegistrationWhatsAppOtpSender(
            new(primaryHttp, new("key", "test-secret", "14157386102")),
            new(backupHttp, new("AC11111111111111111111111111111111", "test-secret", "+17372508034", null)), true);
        var result = await sender.SendAsync("84901234567", "012345", default);
        Assert.Equal(expectedFallback ? 1 : 0, backupCalls);
        Assert.Equal(primaryStatus == HttpStatusCode.InternalServerError ? NotificationOutcome.Unknown : NotificationOutcome.Accepted, result.Outcome);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}

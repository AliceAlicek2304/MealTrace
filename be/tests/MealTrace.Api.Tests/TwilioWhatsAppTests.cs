using System.Net;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Infrastructure.Notifications;

namespace MealTrace.Api.Tests;

public sealed class TwilioWhatsAppTests
{
    [Fact]
    public async Task TrialSendsApprovedTemplateWithoutRegistrationCredentials()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("api.twilio.com", request.RequestUri!.Host);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            var form = await request.Content!.ReadAsStringAsync();
            Assert.Contains("To=whatsapp%3A%2B84901234567", form);
            Assert.Contains("ContentSid=HX", form);
            Assert.DoesNotContain("Body=", form);
            Assert.DoesNotContain("private-password", form);
            return Response(201, "{\"sid\":\"MM" + new string('a', 32) + "\",\"status\":\"queued\"}");
        });
        var result = await Sender(handler).SendAsync("84901234567", default);
        Assert.Equal(NotificationOutcome.Accepted, result.Outcome);
        Assert.Contains("chưa chứa", result.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(500, "{}", NotificationOutcome.Unknown)]
    [InlineData(401, "private-provider-error", NotificationOutcome.Failed)]
    [InlineData(201, "[]", NotificationOutcome.Unknown)]
    [InlineData(201, "{\"sid\":\"MMaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"status\":\"failed\"}", NotificationOutcome.Failed)]
    public async Task ProviderErrorsAreNotDeliveryAndNeverRetried(int code, string body, NotificationOutcome expected)
    {
        var handler = new Handler(_ => Task.FromResult(Response(code, body)));
        var result = await Sender(handler).SendAsync("84901234567", default);
        Assert.Equal(expected, result.Outcome);
        Assert.DoesNotContain("private", result.Message);
        Assert.Equal(1, handler.Calls);
    }

    private static TwilioWhatsAppSender Sender(Handler handler) => new(new HttpClient(handler),
        new("AC" + new string('a', 32), "fake-token", "+17372508034", "HX" + new string('b', 32)));
    private static HttpResponseMessage Response(int status, string body) => new((HttpStatusCode)status)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return send(request); }
    }
}

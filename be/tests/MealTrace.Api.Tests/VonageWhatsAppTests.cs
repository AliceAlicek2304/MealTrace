using System.Net;
using System.Text.Json;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Infrastructure.Notifications;

namespace MealTrace.Api.Tests;

public sealed class VonageWhatsAppTests
{
    [Fact]
    public async Task SandboxUsesCustomTextAndReturnsAccepted()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("https://messages-sandbox.nexmo.com/v1/messages", request.RequestUri!.ToString());
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            var json = await request.Content!.ReadAsStringAsync();
            using var body = JsonDocument.Parse(json);
            Assert.Equal("whatsapp", body.RootElement.GetProperty("channel").GetString());
            Assert.Equal("84901234567", body.RootElement.GetProperty("to").GetString());
            Assert.Equal("Child A; password: Fake!9", body.RootElement.GetProperty("text").GetString());
            return Response(202, "{\"message_uuid\":\"6f0fbaaa-d839-4d11-9744-801ebfa2b82e\"}");
        });
        Assert.Equal(NotificationOutcome.Accepted, (await Sender(handler).SendAsync("84901234567", default, "Child A; password: Fake!9")).Outcome);
    }

    [Theory]
    [InlineData(401, NotificationOutcome.Failed)]
    [InlineData(429, NotificationOutcome.Failed)]
    [InlineData(500, NotificationOutcome.Unknown)]
    [InlineData(408, NotificationOutcome.Unknown)]
    [InlineData(202, NotificationOutcome.Unknown)]
    public async Task ErrorsAreSanitizedAndNotRetried(int code, NotificationOutcome outcome)
    {
        var handler = new Handler(_ => Task.FromResult(Response(code, "[\"private-payload\"]")));
        var result = await Sender(handler).SendAsync("84901234567", default, "test");
        Assert.Equal(outcome, result.Outcome);
        Assert.DoesNotContain("private", result.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task TimeoutIsUnknown()
    {
        var handler = new Handler(_ => throw new TaskCanceledException());
        Assert.Equal(NotificationOutcome.Unknown, (await Sender(handler).SendAsync("84901234567", default, "test")).Outcome);
    }

    [Theory]
    [InlineData(NotificationOutcome.Accepted, 0)]
    [InlineData(NotificationOutcome.Unknown, 0)]
    [InlineData(NotificationOutcome.Failed, 1)]
    [InlineData(NotificationOutcome.Disabled, 1)]
    public async Task FallbackOccursOnlyWhenPrimaryDefinitelyDidNotAccept(NotificationOutcome outcome, int expectedFallback)
    {
        var primary = new FakeSender(outcome);
        var backup = new FakeSender(NotificationOutcome.Accepted);
        var sender = new PriorityWhatsAppSender(primary, backup);
        var result = await sender.SendAsync("84901234567", default, "custom text");
        Assert.Equal(1, primary.Calls);
        Assert.Equal(expectedFallback, backup.Calls);
        Assert.Equal("custom text", primary.Text);
        if (expectedFallback == 1) Assert.Equal("custom text", backup.Text);
        Assert.Equal(expectedFallback == 1 ? NotificationOutcome.Accepted : outcome, result.Outcome);
    }

    private static VonageWhatsAppSender Sender(Handler handler) => new(new HttpClient(handler), new("fake-key", "fake-secret", "14157386102"));
    private static HttpResponseMessage Response(int status, string body) => new((HttpStatusCode)status)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return send(request); }
    }
    private sealed class FakeSender(NotificationOutcome outcome) : INotificationSender
    {
        public int Calls { get; private set; }
        public string? Text { get; private set; }
        public Task<NotificationSendResult> SendAsync(string number, CancellationToken ct, string? text = null)
        { Calls++; Text = text; return Task.FromResult(new NotificationSendResult(outcome, null, "test")); }
    }
}

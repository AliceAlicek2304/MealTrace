using System.Net;
using System.Text.Json;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Features.Notifications;
using MealTrace.Domain.Security;

namespace MealTrace.Api.Tests;

public sealed class NotificationTests
{
    [Fact]
    public async Task OnlyAdminCanSendAndOnlyConfiguredTesterReceivesMessage()
    {
        var fake = new FakeSender();
        var clock = new Clock();
        var gate = new NotificationSendGate(clock);
        var policy = new NotificationPolicy(true, "0901234567");
        var forbidden = await new NotificationService(fake, policy, gate, new Actor(RoleNames.Parent)).SendTestAsync(default);
        Assert.False(forbidden.IsSuccess);
        Assert.Equal(0, fake.Calls);
        var service = new NotificationService(fake, policy, gate, new Actor(RoleNames.Admin));
        var first = await service.SendTestAsync(default);
        Assert.True(first.IsSuccess);
        Assert.Equal("84901234567", fake.Number);
        Assert.Equal("******4567", first.Value!.Recipient);
        Assert.False((await service.SendTestAsync(default)).IsSuccess);
        Assert.Equal(1, fake.Calls);
        clock.Now = clock.Now.AddSeconds(60);
        Assert.True((await service.SendTestAsync(default)).IsSuccess);
        Assert.Equal(2, fake.Calls);
    }

    [Theory]
    [InlineData(NotificationOutcome.Accepted, "ACCEPTED")]
    [InlineData(NotificationOutcome.Failed, "FAILED")]
    [InlineData(NotificationOutcome.Unknown, "UNKNOWN")]
    [InlineData(NotificationOutcome.Disabled, "DISABLED")]
    public async Task PublicNotificationKeepsOutcomeWithoutProviderDiagnostics(NotificationOutcome outcome, string expectedStatus)
    {
        var sender = new FakeSender(outcome);
        var result = await new NotificationService(sender, new NotificationPolicy(true, "0901234567"),
            new NotificationSendGate(new Clock()), new Actor(RoleNames.Admin)).SendTestAsync(default);
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Value!.Status);
        Assert.Equal("******4567", result.Value.Recipient);
        Assert.DoesNotContain("provider-internal", result.Value.Message);
        Assert.DoesNotContain("sandbox", result.Value.Message);
        Assert.Equal(1, sender.Calls);
    }

    [Fact]
    public void GateBlocksConcurrentRequestsEvenAfterCooldown()
    {
        var clock = new Clock();
        var gate = new NotificationSendGate(clock);
        Assert.True(gate.TryBegin());
        clock.Now = clock.Now.AddMinutes(2);
        Assert.False(gate.TryBegin());
        gate.End();
        Assert.True(gate.TryBegin());
    }

    private sealed class FakeSender(NotificationOutcome outcome = NotificationOutcome.Accepted) : INotificationSender
    {
        public int Calls { get; private set; }
        public string? Number { get; private set; }
        public Task<NotificationSendResult> SendAsync(string number, CancellationToken ct, string? text = null) { Calls++; Number = number; return Task.FromResult(new NotificationSendResult(outcome, Guid.NewGuid().ToString(), "provider-internal: Vonage ưu tiên, Twilio dự phòng, sandbox")); }
    }
    private sealed class Actor(string role) : ICurrentActor
    {
        public Guid? UserId => Guid.Empty;
        public bool IsInRole(string value) => value == role;
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-06T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

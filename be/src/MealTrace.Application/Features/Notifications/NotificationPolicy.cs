namespace MealTrace.Application.Features.Notifications;

public sealed record NotificationPolicy(bool Enabled, string? TestNumber, string Channel = "WhatsApp", bool TemplateOnly = true);

// POC protection per application instance; production batching will use a persistent outbox.
public sealed class NotificationSendGate(TimeProvider clock)
{
    private readonly object gate = new();
    private DateTimeOffset? lastAttempt;
    private bool running;
    public bool TryBegin()
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (running || (lastAttempt is not null && now - lastAttempt.Value < TimeSpan.FromMinutes(1))) return false;
            running = true;
            lastAttempt = now;
            return true;
        }
    }
    public void End() { lock (gate) running = false; }
}

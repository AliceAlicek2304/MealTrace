namespace MealTrace.Api.Data;

public sealed class AccountPasswordResetAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PerformedByUserId { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
}

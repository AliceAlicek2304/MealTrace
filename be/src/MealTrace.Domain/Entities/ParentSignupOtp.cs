namespace MealTrace.Domain.Entities;

public sealed class ParentSignupOtp
{
    public string PhoneNumber { get; set; } = "";
    public Guid ChallengeId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public int SendsInWindow { get; set; }
    public int FailedAttempts { get; set; }
    public bool Used { get; set; }
}

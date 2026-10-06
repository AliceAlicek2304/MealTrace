namespace MealTrace.Domain.Entities;

public sealed class InspectorGrant
{
    public Guid UserId { get; set; }

    public DateOnly ExpiresOn { get; set; }
    public Guid GrantedById { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}

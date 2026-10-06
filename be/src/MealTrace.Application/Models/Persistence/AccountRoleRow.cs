namespace MealTrace.Application.Models.Persistence;

public sealed record AccountRoleRow
{
    public Guid UserId { get; init; }
    public required string Role { get; init; }
}

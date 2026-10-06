namespace MealTrace.Application.Dtos.Identity;

// Account data used by use cases. Password hashes, stamps and Identity internals never cross this port.
public sealed class IdentityAccount
{
    public Guid Id { get; set; }
    public required string FullName { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AccountRole
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
}
public sealed class AccountRoleLink
{
    public Guid UserId { get; init; }
    public Guid RoleId { get; init; }
}
public sealed record IdentityFailure(string Description);
public sealed record IdentityOperation(bool Succeeded, IReadOnlyList<IdentityFailure> Errors);

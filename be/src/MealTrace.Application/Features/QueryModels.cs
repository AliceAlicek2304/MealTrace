namespace MealTrace.Application.Features;

internal sealed record AccountRoleRow
{
    public Guid UserId { get; init; }
    public required string Role { get; init; }
}

internal sealed record AmendmentCandidateRow
{
    public Guid Id { get; init; }
    public required string FullName { get; init; }
    public required string StudentCode { get; init; }
}

internal sealed record StudentSummaryRow
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public int Revision { get; init; }
    public bool IsActive { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
}

internal sealed record ParentStudentRow
{
    public Guid StudentId { get; init; }
    public Guid Id { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
}

internal sealed record ClassStudentRow
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
}

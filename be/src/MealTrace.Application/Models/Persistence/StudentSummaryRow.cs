namespace MealTrace.Application.Models.Persistence;

public sealed record StudentSummaryRow
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public int Revision { get; init; }
    public bool IsActive { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
}

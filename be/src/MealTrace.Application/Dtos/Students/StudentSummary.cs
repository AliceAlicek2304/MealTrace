
namespace MealTrace.Application.Dtos.Students;

public sealed record StudentSummary
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
    public required IEnumerable<ParentSummary> Parents { get; init; }
}


namespace MealTrace.Application.Dtos.Students;

public sealed record ParentSummary
{
    public Guid Id { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
}

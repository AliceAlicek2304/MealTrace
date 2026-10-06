namespace MealTrace.Application.Models.Persistence;

public sealed record ParentStudentRow
{
    public Guid StudentId { get; init; }
    public Guid Id { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
}

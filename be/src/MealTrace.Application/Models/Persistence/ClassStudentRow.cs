namespace MealTrace.Application.Models.Persistence;

public sealed record ClassStudentRow
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
}

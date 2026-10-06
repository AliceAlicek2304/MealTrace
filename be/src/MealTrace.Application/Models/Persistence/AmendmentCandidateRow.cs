namespace MealTrace.Application.Models.Persistence;

public sealed record AmendmentCandidateRow
{
    public Guid Id { get; init; }
    public required string FullName { get; init; }
    public required string StudentCode { get; init; }
}

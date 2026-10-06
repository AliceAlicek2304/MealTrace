
namespace MealTrace.Application.Dtos.Students;
public sealed record ClassSummary
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string SchoolYear { get; init; }
    public int StudentCount { get; init; }
}

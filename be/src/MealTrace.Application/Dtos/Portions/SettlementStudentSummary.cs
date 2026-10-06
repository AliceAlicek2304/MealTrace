
namespace MealTrace.Application.Dtos.Portions;

public sealed record SettlementStudentSummary
{
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
}

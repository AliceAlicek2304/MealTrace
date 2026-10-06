
namespace MealTrace.Application.Dtos.Portions;

public sealed record SettlementSnapshot
{
    public Guid Id { get; init; }
    public int Version { get; init; }
    public int Count { get; init; }
    public DateTimeOffset SettledAt { get; init; }
    public required string SettledBy { get; init; }
    public string? Reason { get; init; }
    public Guid? SupersedesId { get; init; }
    public required IEnumerable<SettlementStudentSummary> Students { get; init; }
    public required IEnumerable<SettlementDecisionSummary> Decisions { get; init; }
}

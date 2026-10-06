
namespace MealTrace.Application.Dtos.Portions;

public sealed record AmendmentComparisonResponse
{
    public required SettlementSnapshot Before { get; init; }
    public SettlementSnapshot? After { get; init; }
}

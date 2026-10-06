
namespace MealTrace.Application.Dtos.Meals;

public sealed record MealDayDetail
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public int? SettledPortions { get; init; }
    public required IEnumerable<MealDishSummary> Dishes { get; init; }
    public required IEnumerable<SettlementSummary> Settlements { get; init; }
    public required IEnumerable<MealEvidenceSummary> Evidence { get; init; }
}

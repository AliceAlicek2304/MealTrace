
namespace MealTrace.Application.Dtos.Meals;
public sealed record MealDaySummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public required List<MealDishSummary> Dishes { get; init; }
    public int? SettledPortions { get; init; }
}

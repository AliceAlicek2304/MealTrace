
namespace MealTrace.Application.Dtos.Workflow;

public sealed record WorkflowMealDaySummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public string? SchoolYear { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public bool IsSettled { get; init; }
}

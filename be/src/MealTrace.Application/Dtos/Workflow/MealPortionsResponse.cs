using MealTrace.Application.Dtos.Portions;

namespace MealTrace.Application.Dtos.Workflow;

public sealed record MealPortionsResponse
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isSettled")]
    public bool IsSettled { get; init; }
    public required List<ClassPortions> Classes { get; init; }
}

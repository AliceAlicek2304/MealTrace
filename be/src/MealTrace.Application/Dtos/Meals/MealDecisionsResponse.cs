
namespace MealTrace.Application.Dtos.Meals;
public sealed record MealDecisionsResponse
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public DateTimeOffset CutoffAt { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isSettled")]
    public bool IsSettled { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("canEdit")]
    public bool CanEdit { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("asOf")]
    public DateTimeOffset AsOf { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("classes")]
    public required List<Room> Classes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required List<MealDecision> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

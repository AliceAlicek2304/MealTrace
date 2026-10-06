using MealTrace.Application.Dtos.Meals;

namespace MealTrace.Application.Dtos.Responses;
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

public sealed record MealExceptionHistoryResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required IEnumerable<MealExceptionHistoryItem> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

public sealed record MealExceptionHistoryItem
{
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("action")]
    public required string Action { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid? RecordedByUserId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("actorName")]
    public string? ActorName { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isLegacy")]
    public bool IsLegacy { get; init; }
    public Guid? SupersedesId { get; init; }
    public int Sequence { get; init; }
}

public sealed record MealExceptionCreatedResponse
{
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("action")]
    public required string Action { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid? SupersedesId { get; init; }
    public int Sequence { get; init; }
}

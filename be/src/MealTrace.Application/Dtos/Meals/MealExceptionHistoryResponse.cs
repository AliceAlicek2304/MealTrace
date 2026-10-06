
namespace MealTrace.Application.Dtos.Meals;

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

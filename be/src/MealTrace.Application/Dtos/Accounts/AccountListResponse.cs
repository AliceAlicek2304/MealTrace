
namespace MealTrace.Application.Dtos.Accounts;

public sealed record AccountListResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required AccountView[] Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

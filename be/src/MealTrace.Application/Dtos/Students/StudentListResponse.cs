
namespace MealTrace.Application.Dtos.Students;

public sealed record StudentListResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required IEnumerable<StudentSummary> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("earliestChangeDate")]
    public DateOnly EarliestChangeDate { get; init; }
}

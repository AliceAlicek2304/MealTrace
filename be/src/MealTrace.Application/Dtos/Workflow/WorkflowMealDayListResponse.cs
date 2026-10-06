
namespace MealTrace.Application.Dtos.Workflow;

public sealed record WorkflowMealDayListResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required List<WorkflowMealDaySummary> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

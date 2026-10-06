
namespace MealTrace.Application.Dtos.Calendar;

public sealed record CalendarGenerationPreview
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required List<PlanItem> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("previewToken")]
    public required string PreviewToken { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("createCount")]
    public int CreateCount { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("restoreCount")]
    public int RestoreCount { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("existingCount")]
    public int ExistingCount { get; init; }
}

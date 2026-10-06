
namespace MealTrace.Application.Dtos.Workflow;
public sealed record AcademicYearConfiguration
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public required string Code { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("startDate")]
    public DateOnly? StartDate { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("endDate")]
    public DateOnly? EndDate { get; init; }
}

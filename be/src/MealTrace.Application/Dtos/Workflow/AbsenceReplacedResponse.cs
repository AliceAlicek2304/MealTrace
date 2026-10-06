
namespace MealTrace.Application.Dtos.Workflow;

public sealed record AbsenceReplacedResponse
{
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("replacedId")]
    public Guid ReplacedId { get; init; }
}

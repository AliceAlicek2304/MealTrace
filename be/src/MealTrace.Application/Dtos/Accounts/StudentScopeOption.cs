
namespace MealTrace.Application.Dtos.Accounts;

public sealed record StudentScopeOption
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public required string Name { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("classId")]
    public Guid ClassId { get; init; }
}

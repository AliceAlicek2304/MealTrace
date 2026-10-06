
namespace MealTrace.Application.Dtos.Accounts;

public sealed record ClassScopeOption
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public required string Name { get; init; }
}

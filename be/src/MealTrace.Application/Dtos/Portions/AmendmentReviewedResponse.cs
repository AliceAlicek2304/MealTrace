
namespace MealTrace.Application.Dtos.Portions;

public sealed record AmendmentReviewedResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public required string Status { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("settlementId")]
    public Guid? SettlementId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public int? Version { get; init; }
}

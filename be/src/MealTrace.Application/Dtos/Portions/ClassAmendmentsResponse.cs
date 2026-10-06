
namespace MealTrace.Application.Dtos.Portions;

public sealed record ClassAmendmentsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("classId")]
    public Guid ClassId { get; init; }
    public string? ClassName { get; init; }
    public bool HasCompleteRoster { get; init; }
    public bool CanRequest { get; init; }
    public required SettlementSnapshot Original { get; init; }
    public required SettlementSnapshot Current { get; init; }
    public bool HasOriginalSources { get; init; }
    public required IEnumerable<SettlementStudentSummary> Added { get; init; }
    public required IEnumerable<SettlementStudentSummary> Removed { get; init; }
    public required IEnumerable<AmendmentCandidate> Candidates { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("candidateTotal")]
    public int CandidateTotal { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("candidatePage")]
    public int CandidatePage { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required List<PortionAmendmentSummary> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}

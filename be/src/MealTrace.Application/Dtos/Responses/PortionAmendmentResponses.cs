
namespace MealTrace.Application.Dtos.Responses;

public sealed record PortionAmendmentSummary
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string StudentCode { get; init; }
    public Guid BaseSettlementId { get; init; }
    public int BaseVersion { get; init; }
    public Guid? EnrollmentId { get; init; }
    public bool WasEating { get; init; }
    public bool WillEat { get; init; }
    public required string Reason { get; init; }
    public required string RequestedByName { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public required string Status { get; init; }
    public string? ReviewReason { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public Guid? AppliedSettlementId { get; init; }
}

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

public sealed record SettlementStudentSummary
{
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
}

public sealed record AmendmentCandidate
{
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string StudentCode { get; init; }
    public bool WillEat { get; init; }
    public required string Source { get; init; }
    public Guid? EnrollmentId { get; init; }
    public Guid? AbsenceId { get; init; }
    public Guid? ExceptionId { get; init; }
}

public sealed record AmendmentCreatedResponse
{
    public Guid Id { get; init; }
}

public sealed record AmendmentComparisonResponse
{
    public required SettlementSnapshot Before { get; init; }
    public SettlementSnapshot? After { get; init; }
}

public sealed record AmendmentReviewedResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public required string Status { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("settlementId")]
    public Guid? SettlementId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public int? Version { get; init; }
}

public sealed record SettlementSnapshot
{
    public Guid Id { get; init; }
    public int Version { get; init; }
    public int Count { get; init; }
    public DateTimeOffset SettledAt { get; init; }
    public required string SettledBy { get; init; }
    public string? Reason { get; init; }
    public Guid? SupersedesId { get; init; }
    public required IEnumerable<SettlementStudentSummary> Students { get; init; }
    public required IEnumerable<SettlementDecisionSummary> Decisions { get; init; }
}

public sealed record SettlementDecisionSummary
{
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string StudentCode { get; init; }
    public bool WillEat { get; init; }
    public required string Source { get; init; }
    public Guid? EnrollmentId { get; init; }
    public Guid? AbsenceId { get; init; }
    public Guid? ExceptionId { get; init; }
    public Guid? AmendmentId { get; init; }
}

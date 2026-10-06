
namespace MealTrace.Application.Dtos.Responses;
public sealed record ClassSummary
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string SchoolYear { get; init; }
    public int StudentCount { get; init; }
}

public sealed record ClassListResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required List<ClassSummary> Items { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public int Total { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("page")]
    public int Page { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}



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

public sealed record StudentSummary
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public int Revision { get; init; }
    public bool IsActive { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
    public required IEnumerable<ParentSummary> Parents { get; init; }
}

public sealed record ParentSummary
{
    public Guid Id { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed record EnrollmentHistoryItem
{
    public Guid Id { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
    public required string SchoolYear { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public required string Reason { get; init; }
    public string? EndReason { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public DateTimeOffset? EndRecordedAt { get; init; }
    public Guid? EndedByUserId { get; init; }
}

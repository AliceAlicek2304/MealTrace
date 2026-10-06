using MealTrace.Application.Dtos.Portions;

namespace MealTrace.Application.Dtos.Responses;
public sealed record AcademicYearConfiguration
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public required string Code { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("startDate")]
    public DateOnly? StartDate { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("endDate")]
    public DateOnly? EndDate { get; init; }
}

public sealed record NextAcademicYearResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public required string Code { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("startDate")]
    public DateOnly StartDate { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("endDate")]
    public DateOnly EndDate { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("sourceYearCode")]
    public required string SourceYearCode { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isConfigured")]
    public bool IsConfigured { get; init; }
}

public sealed record ClassCreatedResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string SchoolYear { get; init; }
}


public sealed record ClassStudentSummary
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
    public required ParentSummary[] Parents { get; init; }
}

public sealed record StudentCreatedResponse
{
    public Guid Id { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
    public int Revision { get; init; }
}

public sealed record ParentLinkedResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("parentId")]
    public Guid ParentId { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("studentId")]
    public Guid StudentId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public bool Created { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("temporaryPassword")]
    public string? TemporaryPassword { get; init; }
}

public sealed record ParentChildSummary
{
    public Guid StudentId { get; init; }
    public required string StudentCode { get; init; }
    public required string FullName { get; init; }
    public Guid ClassId { get; init; }
    public required string ClassName { get; init; }
    public required string SchoolYear { get; init; }
    public DateOnly? YearStartDate { get; init; }
    public DateOnly? YearEndDate { get; init; }
}

public sealed record AbsenceCreatedResponse
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
}

public sealed record AbsenceReplacedResponse
{
    public Guid Id { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("replacedId")]
    public Guid ReplacedId { get; init; }
}

public sealed record AbsenceSummary
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public required string StudentName { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset ReportedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public string? SchoolYear { get; init; }
}

public sealed record MealDayCreatedResponse
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public required string SchoolYear { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
}

public sealed record WorkflowMealDaySummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public string? SchoolYear { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public bool IsSettled { get; init; }
}

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

public sealed record MealPortionsResponse
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isSettled")]
    public bool IsSettled { get; init; }
    public required List<ClassPortions> Classes { get; init; }
}

public sealed record PortionsSettledResponse
{
    public Guid Id { get; init; }
    public int Classes { get; init; }
    public int Total { get; init; }
}

public sealed record AcademicYearResponse(string Code, DateOnly StartDate, DateOnly EndDate);

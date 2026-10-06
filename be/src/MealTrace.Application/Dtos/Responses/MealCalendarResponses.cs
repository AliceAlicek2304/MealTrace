using MealTrace.Application.Dtos.Calendar;

namespace MealTrace.Application.Dtos.Responses;
public sealed record CalendarDaySummary
{
    [System.Text.Json.Serialization.JsonPropertyName("date")]
    public DateOnly Date { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isOpen")]
    public bool IsOpen { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("mealTypes")]
    public required string[] MealTypes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isException")]
    public bool IsException { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("locked")]
    public bool Locked { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("sessions")]
    public required IEnumerable<CalendarSessionSummary> Sessions { get; init; }
}

public sealed record CalendarSessionSummary
{
    public Guid Id { get; init; }
    public required string MealType { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("isSettled")]
    public bool IsSettled { get; init; }
}

public sealed record MealCalendarResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("schoolYear")]
    public required string SchoolYear { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("from")]
    public DateOnly From { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("to")]
    public DateOnly To { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("revision")]
    public int Revision { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("weekdays")]
    public required IEnumerable<int> Weekdays { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("mealTypes")]
    public required string[] MealTypes { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("days")]
    public required List<CalendarDaySummary> Days { get; init; }
}

public sealed record CalendarRevisionResponse
{
    public int Revision { get; init; }
}

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

public sealed record GeneratedSessionSummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
}

public sealed record CalendarGenerationResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("created")]
    public int Created { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("restored")]
    public int Restored { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("existing")]
    public int Existing { get; init; }
}

public sealed record CalendarHistoryItem
{
    public Guid Id { get; init; }
    public DateOnly? Date { get; init; }
    public required string Kind { get; init; }
    public required string Reason { get; init; }
    public Guid ActorId { get; init; }
    public required string ActorName { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

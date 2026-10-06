using MealTrace.Application.Dtos.Calendar;

namespace MealTrace.Application.Features.Calendar;
internal sealed record ScheduleAuditState
{
    [System.Text.Json.Serialization.JsonPropertyName("schedule")]
    public required MealTrace.Domain.Entities.MealSchedule Schedule { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("sessions")]
    public required IEnumerable<SessionAuditState> Sessions { get; init; }
}

internal sealed record DayAuditState
{
    [System.Text.Json.Serialization.JsonPropertyName("exception")]
    public MealTrace.Domain.Entities.MealCalendarException? Exception { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("sessions")]
    public required IEnumerable<SessionAuditState> Sessions { get; init; }
}

internal sealed record SessionAuditState
{
    public Guid Id { get; init; }
    public bool IsCancelled { get; init; }
}

internal sealed record CalendarPreviewFingerprint
{
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public required string Code { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("revision")]
    public int Revision { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("items")]
    public required List<PlanItem> Items { get; init; }
}

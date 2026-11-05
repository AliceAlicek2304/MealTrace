using System.Text.Json;

namespace MealTrace.Application.Features.Audit;

public sealed record AuditActor(Guid Id, string? Name);

// Compact merge key. Payload/roster JSON is loaded only for events selected for
// the final page/export; deep paging never materializes discarded snapshots.
public sealed record AuditKey(Guid RecordId, string Source, DateTimeOffset OccurredAt)
{
    public string Id => $"{Source}:{RecordId:D}";
}

public sealed record AuditEntry(string Id, string Source, DateTimeOffset OccurredAt,
    AuditActor? Actor, string Action, string EntityType, string EntityId, string? Reason,
    JsonElement? Before, JsonElement? After, string? CorrelationId);

// Bounds are inclusive instants, normalized to UTC before reaching persistence.
public sealed record AuditFilter(DateTimeOffset? From = null, DateTimeOffset? To = null,
    Guid? ActorId = null, string? EntityType = null, string? EntityId = null, string? Action = null);

public sealed record AuditPage(IReadOnlyList<AuditEntry> Items, long Total, int Page, int PageSize);
public sealed record AuditCsv(byte[] Content, string FileName);

public sealed class AuditRequestException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

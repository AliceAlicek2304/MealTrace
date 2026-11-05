using System.Text.Json;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features.Audit;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Audit;

// Projection contains only the business-history row, never Identity entities.
public sealed class AuditProjection<T>
{
    public required T Event { get; init; }
    public Guid Id { get; init; }
    public DateTimeOffset At { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public string? EntityText { get; init; }
    public string? Reason { get; init; }
}

public abstract class EfAuditSource<T> : IAuditSource
{
    public abstract string Name { get; }
    protected abstract IQueryable<AuditProjection<T>> Events { get; }
    protected abstract JsonElement? After(T row);
    protected virtual bool TextEntityIdentity => false;
    protected virtual JsonElement? Before(T row) => null;
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);
    protected static JsonElement Json<TValue>(TValue value) => JsonSerializer.SerializeToElement(value, SnapshotJson);
    protected virtual AuditActor? Actor(AuditProjection<T> row) => row.ActorId is { } id && id != Guid.Empty
        ? new AuditActor(id, row.ActorName) : null;
    protected virtual IQueryable<AuditProjection<T>> FilterActor(IQueryable<AuditProjection<T>> query, Guid actor)
        => query.Where(x => x.ActorId == actor);

    protected virtual IQueryable<AuditProjection<T>> FilterEntity(IQueryable<AuditProjection<T>> query, AuditFilter filter)
    {
        if (filter.EntityType is { } type) query = query.Where(x => x.EntityType == type);
        if (filter.EntityId is { } text)
        {
            if (TextEntityIdentity) query = query.Where(x => x.EntityText == text);
            else query = Guid.TryParse(text, out var id) ? query.Where(x => x.EntityId == id) : query.Where(x => false);
        }
        return query;
    }

    private IQueryable<AuditProjection<T>> Filter(AuditFilter filter)
    {
        var query = FilterEntity(Events, filter);
        if (filter.From is { } from) query = query.Where(x => x.At >= from);
        if (filter.To is { } to) query = query.Where(x => x.At <= to);
        if (filter.ActorId is { } actor) query = FilterActor(query, actor);
        if (filter.Action is { } action) query = query.Where(x => x.Action == action);
        return query;
    }

    public Task<long> CountAsync(AuditFilter filter, CancellationToken ct) => Filter(filter).LongCountAsync(ct);
    public async Task<IReadOnlyList<AuditKey>> ReadKeysAsync(AuditFilter filter, int offset, int take, CancellationToken ct)
    {
        if (offset < 0 || take is < 1 or > AuditService.BatchSize) throw new ArgumentOutOfRangeException(nameof(take));
        return await Filter(filter).OrderByDescending(x => x.At).ThenBy(x => x.Id).Skip(offset).Take(take)
            .Select(x => new AuditKey(x.Id, Name, x.At)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<AuditEntry>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count is < 1 or > AuditService.BatchSize) throw new ArgumentOutOfRangeException(nameof(ids));
        var keys = ids.ToArray();
        var rows = await Events.Where(x => keys.Contains(x.Id)).ToListAsync(ct);
        return rows.Select(Map).ToArray();
    }
    public async Task<AuditEntry?> FindAsync(Guid id, CancellationToken ct)
    {
        var row = await Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }
    private AuditEntry Map(AuditProjection<T> row) => new($"{Name}:{row.Id:D}", Name, row.At.ToUniversalTime(),
        Actor(row),
        row.Action, row.EntityType, row.EntityText ?? row.EntityId.ToString("D"), row.Reason,
        Before(row.Event), After(row.Event), null);
}

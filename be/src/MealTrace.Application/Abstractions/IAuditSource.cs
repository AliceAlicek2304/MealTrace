using MealTrace.Application.Features.Audit;

namespace MealTrace.Application.Abstractions;

// One stable source:GUID per event kind. Missing historical actor/name/reason,
// before/after/correlation stay null; never join a current profile as a snapshot.
// Inclusive UTC filters and ordering occur in persistence BEFORE Skip/Take.
// ReadKeys returns <=128 keys ordered occurredAt DESC, canonical GUID ASC.
// FindMany loads only selected keys, in batches <=128; no per-event queries.
// Student relations must be explicit FKs/frozen membership, never timestamps.
// BE1 integration requires immutable version/entity IDs, recorded actor/time,
// correction reason, version/predecessor, validity/source and pinned recipe lines.
// BE2 ack integration requires ack/meal IDs, actor/time/note and frozen
// {settlementId, version} membership. A snapshot-token hash is not membership.
// Legacy gaps remain null; neither owner schema nor business history is patched
// by these read adapters. Enrollment kind and absence replacement need owner data.
public interface IAuditSource
{
    string Name { get; }
    Task<long> CountAsync(AuditFilter filter, CancellationToken ct);
    Task<IReadOnlyList<AuditKey>> ReadKeysAsync(AuditFilter filter, int offset, int take, CancellationToken ct);
    Task<IReadOnlyList<AuditEntry>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<AuditEntry?> FindAsync(Guid id, CancellationToken ct);
}

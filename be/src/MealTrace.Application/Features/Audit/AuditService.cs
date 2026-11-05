using System.Globalization;
using System.Text;
using System.Text.Json;
using MealTrace.Application.Abstractions;

namespace MealTrace.Application.Features.Audit;

public sealed class AuditService
{
    private readonly IAuditSource[] _sources;
    private readonly Dictionary<string, IAuditSource> _byName;
    public AuditService(IEnumerable<IAuditSource> sources)
    {
        _sources = sources.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        _byName = _sources.ToDictionary(x => x.Name, StringComparer.Ordinal);
    }
    public const int BatchSize = 128;
    public const int ExportLimit = 10_000;

    public async Task<AuditPage> ListAsync(AuditFilter filter, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        filter = Validate(filter);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue - pageSize)
            throw Invalid("AUDIT_INVALID_PAGING", "page phải >= 1, pageSize từ 1 đến 100 và offset hợp lệ.");
        var total = await CountAsync(filter, ct);
        var skip = (page - 1) * pageSize;
        var keys = skip >= total ? [] : await SelectKeysAsync(filter, skip, pageSize, ct);
        var items = await HydrateAsync(keys, ct);
        return new AuditPage(items, total, page, pageSize);
    }

    public async Task<AuditEntry> DetailAsync(string auditId, CancellationToken ct = default)
    {
        var parts = auditId.Split(':');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[1], "D", out var id))
            throw Invalid("AUDIT_INVALID_ID", "auditId phải có dạng source:guid.");
        if (!_byName.TryGetValue(parts[0], out var source)) throw Invalid("AUDIT_UNKNOWN_SOURCE", "Nguồn audit không hợp lệ.");
        var entry = await source.FindAsync(id, ct);
        return entry is null ? throw new AuditRequestException(404, "AUDIT_NOT_FOUND", "Không tìm thấy sự kiện.") : AuditPrivacy.Protect(entry);
    }

    public async Task<AuditCsv> ExportAsync(AuditFilter filter, string format = "csv", CancellationToken ct = default)
    {
        filter = Validate(filter);
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            throw Invalid("AUDIT_INVALID_FORMAT", "Chỉ hỗ trợ CSV.");
        if (filter.From is null || filter.To is null) throw Invalid("AUDIT_RANGE_REQUIRED", "Cần from và to khi xuất CSV.");
        if (filter.To - filter.From > TimeSpan.FromDays(31))
            throw new AuditRequestException(422, "AUDIT_EXPORT_RANGE_EXCEEDED", "Khoảng xuất tối đa 31 ngày.");
        if (await CountAsync(filter, ct) > ExportLimit)
            throw new AuditRequestException(422, "AUDIT_EXPORT_ROWS_EXCEEDED", "CSV tối đa 10.000 dòng; hãy thu hẹp bộ lọc.");
        // The extra row also detects growth between counting and reading.
        var keys = await SelectKeysAsync(filter, 0, ExportLimit + 1, ct);
        if (keys.Count > ExportLimit)
            throw new AuditRequestException(422, "AUDIT_EXPORT_ROWS_EXCEEDED", "CSV tối đa 10.000 dòng.");
        var items = await HydrateAsync(keys, ct);
        var csv = new StringBuilder("id,source,occurredAt,actorId,actorName,action,entityType,entityId,reason,before,after,correlationId\r\n");
        foreach (var item in items)
        {
            var entry = AuditPrivacy.ForExport(item);
            string?[] fields = [entry.Id, entry.Source, entry.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
                entry.Actor?.Id.ToString(), entry.Actor?.Name, entry.Action, entry.EntityType, entry.EntityId,
                entry.Reason, entry.Before?.GetRawText(), entry.After?.GetRawText(), entry.CorrelationId];
            csv.AppendJoin(',', fields.Select(Cell)).Append("\r\n");
        }
        var encoding = new UTF8Encoding(true);
        return new AuditCsv(encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray(), "audit-logs.csv");
    }

    private static string Cell(string? value)
    {
        var text = value ?? "";
        var start = 0;
        while (start < text.Length && (char.IsWhiteSpace(text[start]) || char.IsControl(text[start]) ||
            char.GetUnicodeCategory(text[start]) == UnicodeCategory.Format)) start++;
        if (start < text.Length && ("=+-@".Contains(text[start]) || char.IsControl(text[0]))) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private async Task<long> CountAsync(AuditFilter filter, CancellationToken ct)
    {
        long total = 0;
        foreach (var source in _sources) total += await source.CountAsync(filter, ct);
        return total;
    }

    // Bounded k-way merge: each database stream has its OWN offset and buffer.
    // Never truncate each source to pageSize, or load/filter all history in memory.
    private async Task<List<AuditKey>> SelectKeysAsync(AuditFilter filter, int skip, int take, CancellationToken ct)
    {
        var streams = _sources.Select(x => new StreamState(x)).ToArray();
        var heap = new PriorityQueue<StreamState, AuditKey>(Comparer<AuditKey>.Create(Compare));
        foreach (var stream in streams)
            if (await stream.MoveAsync(filter, ct)) heap.Enqueue(stream, stream.Current);
        var selected = new List<AuditKey>();
        long seen = 0;
        while (heap.TryDequeue(out var stream, out var entry))
        {
            ct.ThrowIfCancellationRequested();
            if (seen++ >= skip) selected.Add(entry);
            if (selected.Count == take) break;
            if (await stream.MoveAsync(filter, ct)) heap.Enqueue(stream, stream.Current);
        }
        return selected;
    }

    private async Task<List<AuditEntry>> HydrateAsync(IReadOnlyList<AuditKey> selected, CancellationToken ct)
    {
        var entries = new Dictionary<string, AuditEntry>(StringComparer.Ordinal);
        foreach (var group in selected.GroupBy(x => x.Source))
            foreach (var batch in group.Chunk(BatchSize))
                foreach (var entry in await _byName[group.Key].FindManyAsync(batch.Select(x => x.RecordId).ToArray(), ct))
                    entries.Add(entry.Id, entry);
        return selected.Select(key => entries.TryGetValue(key.Id, out var entry) && entry.OccurredAt == key.OccurredAt
            ? AuditPrivacy.Protect(entry)
            : throw new AuditRequestException(409, "AUDIT_SOURCE_CHANGED", "Nguồn audit thay đổi trong lúc đọc; hãy thử lại.")).ToList();
    }

    private static int Compare(AuditKey a, AuditKey b)
    {
        var result = b.OccurredAt.CompareTo(a.OccurredAt);
        if (result == 0) result = StringComparer.Ordinal.Compare(a.Source, b.Source);
        return result == 0 ? StringComparer.Ordinal.Compare(a.Id, b.Id) : result;
    }

    private sealed class StreamState(IAuditSource source)
    {
        private IReadOnlyList<AuditKey> _buffer = [];
        private int _position = -1;
        private int _offset;
        private bool _last;
        public AuditKey Current => _buffer[_position];
        public async Task<bool> MoveAsync(AuditFilter filter, CancellationToken ct)
        {
            if (++_position < _buffer.Count) return true;
            if (_last) return false;
            _buffer = await source.ReadKeysAsync(filter, _offset, BatchSize, ct);
            _offset = checked(_offset + _buffer.Count);
            _last = _buffer.Count < BatchSize;
            _position = 0;
            return _buffer.Count > 0;
        }
    }

    private static AuditFilter Validate(AuditFilter filter)
    {
        if (filter.From > filter.To) throw Invalid("AUDIT_INVALID_RANGE", "from phải <= to.");
        if (filter.EntityType?.Length > 100 || filter.EntityId?.Length > 200 || filter.Action?.Length > 100)
            throw Invalid("AUDIT_INVALID_FILTER", "Bộ lọc quá dài.");
        if (filter.ActorId == Guid.Empty || new[] { filter.EntityType, filter.EntityId, filter.Action }
            .Any(x => x is not null && (string.IsNullOrWhiteSpace(x) || x.Any(char.IsControl))))
            throw Invalid("AUDIT_INVALID_FILTER", "Bộ lọc không hợp lệ.");
        return filter with { From = filter.From?.ToUniversalTime(), To = filter.To?.ToUniversalTime() };
    }
    private static AuditRequestException Invalid(string code, string message) => new(400, code, message);
}

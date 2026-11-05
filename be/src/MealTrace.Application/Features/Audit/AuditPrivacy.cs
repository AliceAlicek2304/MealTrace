using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MealTrace.Application.Features.Audit;

public static partial class AuditPrivacy
{
    // Calendar JSON is legacy, untrusted input. Only these scalar fields and
    // containers are accepted, at every level; never serialize an EF entity.
    private static readonly HashSet<string> Scalars = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "schoolYear", "date", "weekdayMask", "mealTypesJson", "revision", "isOpen",
        "reason", "isCancelled", "mealType", "studentId", "classId", "enrollmentId",
        "mealDayId", "willEat", "sequence", "supersedesId", "fromDate", "toDate",
        "startDate", "endDate", "baseSettlementId", "quantity", "isQuantityOnly",
        "approved", "appliedSettlementId", "version", "count", "kitchenAdjustment",
        "cutoffAt", "created", "batchId", "status", "parentId", "sourceRow", "amendmentId",
        "absenceId", "exceptionId", "source"
    };
    private static readonly HashSet<string> Containers = new(StringComparer.OrdinalIgnoreCase)
        { "schedule", "exception", "sessions", "students", "decisions", "items" };

    public static JsonElement? Snapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            return Snapshot(document.RootElement);
        }
        catch (JsonException) { return null; }
    }

    public static JsonElement? Snapshot(JsonElement? value) => Transform(value, false);

    private static JsonElement? Transform(JsonElement? value, bool maskPhones)
    {
        if (value is null || value.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value.Value, 0, maskPhones);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value, int depth, bool maskPhones)
    {
        if (depth > 16) { writer.WriteNullValue(); return; }
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in value.EnumerateObject())
            {
                var composite = p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
                if (!(composite ? Containers.Contains(p.Name) : Scalars.Contains(p.Name))) continue;
                writer.WritePropertyName(p.Name);
                Write(writer, p.Value, depth + 1, maskPhones);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray())
                if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array) Write(writer, item, depth + 1, maskPhones);
            writer.WriteEndArray();
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = CleanText(value.GetString());
            writer.WriteStringValue(maskPhones && text is not null ? MaskPhones(text) : text);
        }
        else value.WriteTo(writer);
    }

    public static AuditEntry Protect(AuditEntry entry) => entry with
    {
        Actor = entry.Actor is { } actor ? actor with { Name = CleanText(actor.Name) } : null,
        Reason = CleanText(entry.Reason), Before = Snapshot(entry.Before), After = Snapshot(entry.After),
        CorrelationId = CleanText(entry.CorrelationId)
    };

    // Mask text values, never numeric JSON, IDs, or the occurredAt CSV column.
    public static AuditEntry ForExport(AuditEntry entry) => entry with
    {
        Actor = entry.Actor is { } actor ? actor with { Name = actor.Name is null ? null : MaskPhones(actor.Name) } : null,
        Reason = entry.Reason is null ? null : MaskPhones(entry.Reason),
        CorrelationId = entry.CorrelationId is null ? null : MaskPhones(entry.CorrelationId),
        Before = Transform(entry.Before, true), After = Transform(entry.After, true)
    };

    public static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // Also sanitize JSON embedded inside a legacy string column.
        if (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
                // MealTypesJson is a real legacy string[] column. Preserve its
                // values while filtering labelled secrets, rather than turning
                // legitimate meal selections into an empty array.
                if (document.RootElement.ValueKind == JsonValueKind.Array &&
                    document.RootElement.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String))
                    return JsonSerializer.Serialize(document.RootElement.EnumerateArray().Select(x => RedactText(x.GetString()!)));
                return Snapshot(document.RootElement)?.GetRawText();
            }
            catch (JsonException) { /* Free text, not a valid serialized snapshot. */ }
        }
        // Labelled credentials redact the whole field, including multiline or
        // ambiguous trailing content. Unlabelled text still is not general DLP.
        return RedactText(text);
    }

    private static string RedactText(string text) => Credentials().IsMatch(text) ? "[REDACTED]" :
        Bearer().Replace(Jwt().Replace(text, "[REDACTED]"), "$1[REDACTED]");

    public static string MaskPhones(string text) => Phone().Replace(text, match =>
    {
        if (!match.Groups["phone"].Success && (Guid.TryParse(match.Value, out _) ||
            DateOnly.TryParseExact(match.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            (match.Value.Contains('T', StringComparison.OrdinalIgnoreCase) && DateTimeOffset.TryParse(match.Value,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))) return match.Value;
        var digits = new string(match.Value.Where(char.IsDigit).ToArray());
        return "***" + digits[^3..];
    });

    [GeneratedRegex(@"((?<![a-z0-9])(?:[a-z0-9_-]*password(?:[_ -]?hash)?|temporary[_ -]?password|[a-z0-9_-]*(?:token|secret)|security[_ -]?stamp|[a-z0-9_-]*api[_ -]?key|private[_ -]?key|jwt[: _-]*(?:key|secret)|authorization|connection[_ -]?strings?)[""']?\s*[=:]\s*)(?:""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|[^\r\n;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Credentials();
    [GeneratedRegex(@"(bearer\s+)[a-z0-9._~+/-]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Bearer();
    // Compact JWT shape also covers headers serialized with leading whitespace
    // (not necessarily eyJ...) and unsecured tokens with an empty signature.
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{3,}\.[A-Za-z0-9_-]*(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    private static partial Regex Jwt();
    [GeneratedRegex(@"\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b|\b\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2}))?(?![\p{L}\d])|(?<phone>(?<![\p{L}\d])\+?\d(?:[ .()-]*\d){6,14}(?![\p{L}\d]))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Phone();
}

using System.Globalization;
using System.Text.RegularExpressions;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Security;

namespace MealTrace.Api.Features;

internal static partial class AuditEndpoints
{
    private const string Root = "/api/admin/audit-logs";

    public static void UseAuditErrors(this WebApplication app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments(Root)) { await next(context); return; }
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await next(context);
            if (!context.Response.HasStarted && context.Response.StatusCode is 401 or 403)
            {
                var status = context.Response.StatusCode;
                await Problem(status, status == 401 ? "AUDIT_UNAUTHENTICATED" : "AUDIT_FORBIDDEN",
                    status == 401 ? "Cần đăng nhập." : "Chỉ ADMIN được đọc audit.").ExecuteAsync(context);
            }
        }
        catch (AuditRequestException error) when (!context.Response.HasStarted)
        {
            await Problem(error.Status, error.Code, error.Message).ExecuteAsync(context);
        }
        catch (Exception error) when (!context.Response.HasStarted && error is not OperationCanceledException)
        {
            // Unexpected provider errors must not expose SQL, snapshots or secrets.
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Audit")
                .LogError("Audit read failed ({ExceptionType}); trace {TraceId}", error.GetType().Name, context.TraceIdentifier);
            await Problem(500, "AUDIT_READ_FAILED", "Không thể đọc audit lúc này.").ExecuteAsync(context);
        }
    });

    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Root).WithTags("Admin - Audit").RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        group.MapGet("", async (HttpRequest request, AuditService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(Filter(request), Integer(request, "page", 1), Integer(request, "pageSize", 25), ct)))
            .Produces<AuditPage>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(500);
        group.MapGet("/export", async (HttpRequest request, AuditService service, CancellationToken ct) =>
        {
            var csv = await service.ExportAsync(Filter(request), Text(request, "format") ?? "csv", ct);
            return Results.File(csv.Content, "text/csv; charset=utf-8", csv.FileName);
        }).Produces<Stream>(200, "text/csv").ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(422).ProducesProblem(500);
        group.MapGet("/entities/{entityType}/{entityId}", async (string entityType, string entityId, HttpRequest request, AuditService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(Filter(request) with { EntityType = entityType, EntityId = entityId },
                Integer(request, "page", 1), Integer(request, "pageSize", 25), ct)))
            .Produces<AuditPage>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(500);
        group.MapGet("/{auditId}", async (string auditId, AuditService service, CancellationToken ct) =>
            Results.Ok(await service.DetailAsync(auditId, ct)))
            .Produces<AuditEntry>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(500);
    }

    private static IResult Problem(int status, string code, string message) => Results.Problem(
        statusCode: status, title: message, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static AuditFilter Filter(HttpRequest request)
    {
        Guid? actorId = null;
        if (Text(request, "actorId") is { } actor)
        {
            if (!Guid.TryParseExact(actor, "D", out var id)) throw Invalid("actorId không hợp lệ.");
            actorId = id;
        }
        return new AuditFilter(Instant(request, "from"), Instant(request, "to"), actorId,
            Text(request, "entityType"), Text(request, "entityId"), Text(request, "action"));
    }
    private static string? Text(HttpRequest request, string key)
    {
        if (!request.Query.TryGetValue(key, out var values)) return null;
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) throw Invalid($"{key} không hợp lệ.");
        return values[0];
    }
    private static int Integer(HttpRequest request, string key, int fallback) => Text(request, key) is not { } text ? fallback :
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : throw Invalid($"{key} phải là số nguyên.");
    private static DateTimeOffset? Instant(HttpRequest request, string key)
    {
        if (Text(request, key) is not { } text) return null;
        if (!IsoInstant().IsMatch(text) || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw Invalid($"{key} phải là timestamp có Z hoặc UTC offset.");
        return value.ToUniversalTime();
    }
    private static AuditRequestException Invalid(string message) => new(400, "AUDIT_INVALID_QUERY", message);
    // Time-only strings otherwise acquire the host's current date during parsing.
    [GeneratedRegex(@"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsoInstant();
}

using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class AuditReviewTests
{
    private const string Root = "/api/admin/audit-logs";
    private static readonly DateTimeOffset At = new(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SwaggerOffersFiltersPagingAndRequiredExportBounds()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        var swagger = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var list = swagger.Paths[Root].Operations[OperationType.Get];
        Assert.Equal(new[] { "action", "actorId", "entityId", "entityType", "from", "page", "pageSize", "to" },
            list.Parameters.Where(p => p.In == ParameterLocation.Query).Select(p => p.Name).Order());
        var size = Assert.Single(list.Parameters, p => p.Name == "pageSize").Schema;
        Assert.Equal(100, size.Maximum); Assert.Equal(25, ((Microsoft.OpenApi.Any.OpenApiInteger)size.Default).Value);
        var export = swagger.Paths[Root + "/export"].Operations[OperationType.Get];
        Assert.True(Assert.Single(export.Parameters, p => p.Name == "from").Required);
        Assert.True(Assert.Single(export.Parameters, p => p.Name == "to").Required);
        Assert.Equal("binary", export.Responses["200"].Content["text/csv"].Schema.Format);
        Assert.DoesNotContain(export.Parameters, p => p.Name is "page" or "pageSize");
    }

    [Fact]
    public async Task DevelopmentSwaggerUiAndDocumentAreServedWithoutSeedingAnyDatabase()
    {
        using var factory = new AuthTestFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Seed:Enabled"] = "false" }));
        });
        using var client = configured.CreateClient();
        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var operation = document.GetProperty("paths").GetProperty(Root).GetProperty("get");
        Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "actorId");
        // Seeder must stay off; the shared SQLite connection has no schema yet.
        using var scope = configured.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table'";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task CsvPreservesTimestampsIdentifiersAndNumericSnapshotValues()
    {
        var entry = Entry(1) with { OccurredAt = At.AddTicks(1234567), Reason = "SĐT-0901234567",
            After = JsonSerializer.SerializeToElement(new { count = 12345678, cutoffAt = At, reason = "+84 901 234 567" }) };
        var service = new AuditService([new RowsSource([entry])]);
        var csv = Encoding.UTF8.GetString((await service.ExportAsync(new AuditFilter(At, At.AddDays(1)))).Content);
        Assert.Contains('"' + entry.OccurredAt.ToString("O", CultureInfo.InvariantCulture) + '"', csv);
        Assert.Contains("12345678", csv); Assert.Contains(entry.Id, csv);
        Assert.DoesNotContain("0901234567", csv); Assert.Contains("***567", csv);
    }

    [Theory]
    [InlineData("0901-23-4567", "***567")]
    [InlineData("1234-56-7890", "***890")]
    [InlineData("0901-23-45", "***345")]
    [InlineData("1234-56-78", "***678")]
    public void PhoneNumbersCannotEscapeMaskingByResemblingADatePrefix(string phone, string masked)
        => Assert.Equal(masked, AuditPrivacy.MaskPhones(phone));

    [Theory]
    [InlineData("api_key=LEAK_VALUE")]
    [InlineData("ApiKey: LEAK_VALUE")]
    [InlineData("client_secret=LEAK_VALUE")]
    [InlineData("AuthToken=LEAK_VALUE")]
    [InlineData("VonageApiSecret=LEAK_VALUE")]
    [InlineData("TempPassword=LEAK_VALUE")]
    [InlineData("Jwt:Key=LEAK_VALUE")]
    [InlineData("password=\"safe LEAK_VALUE\"")]
    [InlineData("password_hash: LEAK_VALUE")]
    [InlineData("temporary password='safe LEAK_VALUE'")]
    [InlineData("Reason password=first line\nLEAK_VALUE")]
    [InlineData("password=\"first line\"\nLEAK_VALUE")]
    [InlineData("[ { \"access_token\": \"LEAK_VALUE\" } ]")]
    public void CredentialLabelsCannotLeakAcrossFreeText(string text)
        => Assert.DoesNotContain("LEAK_VALUE", AuditPrivacy.CleanText(text) ?? "");

    [Fact]
    public void LegacyMealSelectionAndOrdinaryReasonsSurvivePrivacyFiltering()
    {
        var snapshot = AuditPrivacy.Snapshot("{\"schedule\":{\"MealTypesJson\":\"[\\\"Breakfast\\\",\\\"Lunch\\\"]\"}}");
        var types = snapshot!.Value.GetProperty("schedule").GetProperty("MealTypesJson").GetString();
        Assert.Equal(new[] { "Breakfast", "Lunch" }, JsonSerializer.Deserialize<string[]>(types!));
        Assert.Equal("[Ghi chú] Không ăn", AuditPrivacy.CleanText("[Ghi chú] Không ăn"));
        Assert.DoesNotContain("LEAK_VALUE", AuditPrivacy.CleanText("[\"api_key=LEAK_VALUE\"]")!);
    }

    [Fact]
    public void CompactJwtWithWhitespaceHeaderDoesNotRequireEyJPrefixToBeRedacted()
    {
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(" {\"alg\":\"HS256\"}") + "." + Encode("{\"sub\":\"private\"}") + ".signature";
        Assert.DoesNotContain(token, AuditPrivacy.CleanText("Thông tin " + token)!);
        var unsigned = Encode("{\"alg\":\"none\"}") + "." + Encode("{\"sub\":\"private\"}") + ".";
        Assert.DoesNotContain(unsigned, AuditPrivacy.CleanText(unsigned)!);
    }

    [Fact]
    public async Task EveryHttpOutputRedactsActorReasonAndNestedSnapshots()
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        Guid studentId, calendarId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { ClassId = seed.ClassId, FullName = "Tên riêng LEAK_PII", IsActive = false };
            db.Students.Add(child); studentId = child.Id;
            db.Enrollments.Add(new Enrollment { StudentId = child.Id, ClassId = seed.ClassId, StartDate = new DateOnly(2026, 10, 1),
                RecordedAt = At, RecordedByUserId = seed.TeacherId, Reason = "api_key=LEAK_VALUE" });
            var calendar = new MealCalendarAudit { SchoolYear = "2026-2027", Kind = "GENERATE", ActorId = seed.TeacherId,
                ActorName = "password=\"value LEAK_VALUE\"", Reason = "client_secret=LEAK_VALUE", RecordedAt = At,
                BeforeJson = "bad JSON LEAK_VALUE", AfterJson = "{\"sessions\":[{\"apiKey\":\"LEAK_VALUE\",\"reason\":\"access_token=LEAK_VALUE\",\"students\":[{\"passwordHash\":\"LEAK_VALUE\"}]}]}" };
            db.MealCalendarAudits.Add(calendar); calendarId = calendar.Id;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        foreach (var url in new[] { Root, $"{Root}/calendar:{calendarId}", $"{Root}/entities/Student/{studentId}",
            Root + "/export?from=2026-10-20T00:00:00Z&to=2026-10-21T00:00:00Z" })
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            Assert.DoesNotContain("LEAK_", await response.Content.ReadAsStringAsync());
        }
        foreach (var query in new[] { "page=2147483647&pageSize=100", "pageSize=0", "actorId=00000000-0000-0000-0000-000000000000",
            "entityType=%09Student", "from=03:00:00Z", "from=2026-10-21T00:00:00Z&to=2026-10-20T00:00:00Z" })
        {
            var response = await client.GetAsync(Root + "?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
        foreach (var query in new[] { "from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00.0000001Z", "format=xlsx", "from=2026-10-01T00:00:00Z" })
        {
            var response = await client.GetAsync(Root + "/export?" + query);
            Assert.Equal(query.StartsWith("from=2026-10-01T00:00:00Z&to=") ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
    }

    [Fact]
    public async Task UnexpectedSourceFailureReturnsSafeProblemDetails()
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuditSource>(); services.AddScoped<IAuditSource, BrokenSource>();
        }));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var response = await client.GetAsync(Root);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("LEAK_VALUE", await response.Content.ReadAsStringAsync());
        Assert.Equal("AUDIT_READ_FAILED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovedOrRetimestampedEventsAreReadConflictsRatherThanPartialPages(bool retimestamped)
    {
        var error = await Assert.ThrowsAsync<AuditRequestException>(() => new AuditService([new ChangedSource(retimestamped)]).ListAsync(new AuditFilter()));
        Assert.Equal(409, error.Status); Assert.Equal("AUDIT_SOURCE_CHANGED", error.Code);
    }

    [Fact]
    public async Task AuditSnapshotsStayStableWhenProfilesAndEnrollmentEndChange()
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        Guid childId, enrollmentId, calendarId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { ClassId = seed.ClassId, FullName = "Tên đầu", IsActive = false };
            db.Students.Add(child); childId = child.Id;
            var enrollment = new Enrollment { StudentId = child.Id, ClassId = seed.ClassId, StartDate = new DateOnly(2026, 9, 1),
                RecordedAt = At, RecordedByUserId = seed.TeacherId, Reason = "Ghi danh" };
            db.Enrollments.Add(enrollment); enrollmentId = enrollment.Id;
            var calendar = new MealCalendarAudit { SchoolYear = "2026-2027", Kind = "LEGACY_UNKNOWN", RecordedAt = At,
                ActorId = seed.TeacherId, ActorName = "Tên đã ghi", Reason = "Lịch", BeforeJson = "null", AfterJson = "{}" };
            db.MealCalendarAudits.Add(calendar); calendarId = calendar.Id;
            await db.SaveChangesAsync();
        }
        AuditEntry original;
        using (var scope = factory.Services.CreateScope())
            original = await scope.ServiceProvider.GetRequiredService<AuditService>().DetailAsync($"enrollment:{enrollmentId}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MealTrace.Infrastructure.Identity.ApplicationUser>>();
            var teacher = (await users.FindByIdAsync(seed.TeacherId.ToString()))!;
            teacher.FullName = "Tên hiện tại"; Assert.True((await users.UpdateAsync(teacher)).Succeeded);
            (await db.Students.FindAsync(childId))!.FullName = "Tên trẻ hiện tại";
            var enrollment = (await db.Enrollments.FindAsync(enrollmentId))!;
            enrollment.EndDate = new DateOnly(2026, 10, 21); enrollment.EndRecordedAt = At.AddDays(1);
            enrollment.EndReason = "Kết thúc"; enrollment.EndedByUserId = seed.TeacherId;
            await db.SaveChangesAsync();
        }
        using var check = factory.Services.CreateScope();
        var service = check.ServiceProvider.GetRequiredService<AuditService>();
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await service.DetailAsync(original.Id)));
        var calendarEvent = await service.DetailAsync($"calendar:{calendarId}");
        Assert.Equal("Tên đã ghi", calendarEvent.Actor!.Name);
        Assert.Equal("calendar.unknown", calendarEvent.Action);
        Assert.Equal(At.AddDays(1), (await service.DetailAsync($"enrollment-end:{enrollmentId}")).OccurredAt);
        Assert.Empty(check.ServiceProvider.GetRequiredService<MealTraceDbContext>().ChangeTracker.Entries());
    }

    private sealed class ChangedSource(bool retimestamped) : IAuditSource
    {
        public string Name => "test";
        public Task<long> CountAsync(AuditFilter filter, CancellationToken ct) => Task.FromResult(1L);
        public Task<IReadOnlyList<AuditKey>> ReadKeysAsync(AuditFilter filter, int offset, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AuditKey>>(offset == 0 ? [new AuditKey(Guid.Parse(Entry(1).Id[5..]), Name, At)] : []);
        public Task<IReadOnlyList<AuditEntry>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AuditEntry>>(retimestamped ? [Entry(1) with { OccurredAt = At.AddSeconds(1) }] : []);
        public Task<AuditEntry?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<AuditEntry?>(null);
    }

    private sealed class BrokenSource : IAuditSource
    {
        public string Name => "broken";
        public Task<long> CountAsync(AuditFilter filter, CancellationToken ct) => throw new InvalidOperationException("api_key=LEAK_VALUE");
        public Task<IReadOnlyList<AuditKey>> ReadKeysAsync(AuditFilter filter, int offset, int take, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditEntry>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<AuditEntry?> FindAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task DeepPagingReadsCompactKeysBeforeHydratingOnlyTheSelectedPage()
    {
        var probe = new QueryProbe();
        using var factory = new AuthTestFactory(commands: probe);
        var seed = await factory.SeedUsersAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.MealCalendarAudits.AddRange(Enumerable.Range(0, 260).Select(i => new MealCalendarAudit
            {
                SchoolYear = "2026-2027", Kind = "GENERATE", RecordedAt = At.AddSeconds(i), ActorId = seed.TeacherId,
                ActorName = "Tên đã ghi", Reason = "Lịch", BeforeJson = "null", AfterJson = "{}"
            }));
            await db.SaveChangesAsync();
        }
        probe.Queries.Clear();
        using var check = factory.Services.CreateScope();
        var page = await check.ServiceProvider.GetRequiredService<AuditService>().ListAsync(new AuditFilter(), 11, 25);
        Assert.Equal(260, page.Total); Assert.Equal(10, page.Items.Count);
        var indexReads = probe.Queries.Where(q => q.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.NotEmpty(indexReads);
        Assert.All(indexReads, q => Assert.DoesNotContain("BeforeJson", q));
        Assert.All(indexReads, q => Assert.DoesNotContain("AfterJson", q));
    }

    internal static AuditEntry Entry(int i) => new($"test:{new Guid(i, 0, 0, new byte[8]):D}", "test", At,
        null, "test.action", "Student", Guid.Empty.ToString(), null, null, null, null);

    internal sealed class RowsSource(AuditEntry[] entries) : IAuditSource
    {
        public string Name => "test";
        public Task<long> CountAsync(AuditFilter filter, CancellationToken ct) => Task.FromResult(entries.LongLength);
        public Task<AuditEntry?> FindAsync(Guid id, CancellationToken ct)
            => Task.FromResult(entries.SingleOrDefault(e => e.Id == $"test:{id:D}"));
        public Task<IReadOnlyList<AuditEntry>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            var keys = ids.Select(id => $"test:{id:D}").ToHashSet();
            return Task.FromResult<IReadOnlyList<AuditEntry>>(entries.Where(e => keys.Contains(e.Id)).ToArray());
        }
        public Task<IReadOnlyList<AuditKey>> ReadKeysAsync(AuditFilter filter, int offset, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AuditKey>>(entries.OrderByDescending(e => e.OccurredAt)
                .ThenBy(e => e.Id, StringComparer.Ordinal).Skip(offset).Take(take)
                .Select(e => new AuditKey(Guid.Parse(e.Id[5..]), "test", e.OccurredAt)).ToArray());
    }

    internal sealed class QueryProbe : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class AuditTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);
    private const string Root = "/api/admin/audit-logs";

    [Fact]
    public async Task MergeCrossesSourceBuffersAndPagesWithoutLossOrDuplicates()
    {
        var actor = Guid.NewGuid();
        var rows = Enumerable.Range(1, 550).Select(i => Entry(i % 2 == 0 ? "a" : "b", i, actor)).ToArray();
        var a = new MemorySource("a", rows.Where(x => x.Source == "a").ToArray());
        var b = new MemorySource("b", rows.Where(x => x.Source == "b").ToArray());
        var service = new AuditService([b, a]);
        var expected = rows.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Source, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var actual = new List<AuditEntry>();
        for (var page = 1; page <= 6; page++)
        {
            var result = await service.ListAsync(new AuditFilter(), page, 100);
            Assert.Equal(550, result.Total);
            actual.AddRange(result.Items);
        }
        Assert.Equal(expected.Select(x => x.Id), actual.Select(x => x.Id));
        Assert.Equal(550, actual.Select(x => x.Id).Distinct().Count());
        Assert.True(a.Reads > 6);
        var filtered = await service.ListAsync(new AuditFilter(At, At, actor, "Student", actor.ToString(), "test"));
        Assert.Equal(rows.Count(x => x.OccurredAt == At), filtered.Total);
    }

    [Fact]
    public async Task DetailRejectsMalformedUnknownAndMissingIds()
    {
        var service = new AuditService([new MemorySource("a", [])]);
        foreach (var id in new[] { "bad", "a:bad", "a:" + Guid.NewGuid().ToString("N"), "a:x:y" })
            Assert.Equal("AUDIT_INVALID_ID", (await Assert.ThrowsAsync<AuditRequestException>(() => service.DetailAsync(id))).Code);
        Assert.Equal("AUDIT_UNKNOWN_SOURCE", (await Assert.ThrowsAsync<AuditRequestException>(() => service.DetailAsync($"unknown:{Guid.NewGuid()}"))).Code);
        Assert.Equal(404, (await Assert.ThrowsAsync<AuditRequestException>(() => service.DetailAsync($"a:{Guid.NewGuid()}"))).Status);
    }

    [Theory]
    [InlineData(10000, false)]
    [InlineData(10001, true)]
    public async Task ExportEnforcesExactRowBoundary(int count, bool rejected)
    {
        var rows = Enumerable.Range(1, count).Select(i => Entry("a", i, Guid.Empty) with { OccurredAt = At }).ToArray();
        var source = new MemorySource("a", rows);
        var service = new AuditService([source]);
        if (rejected)
        {
            var error = await Assert.ThrowsAsync<AuditRequestException>(() => service.ExportAsync(new AuditFilter(At, At.AddDays(31))));
            Assert.Equal(422, error.Status); Assert.Equal("AUDIT_EXPORT_ROWS_EXCEEDED", error.Code);
            Assert.Equal(0, source.Reads);
        }
        else
        {
            var csv = await service.ExportAsync(new AuditFilter(At, At.AddDays(31)));
            Assert.Equal(10002, Encoding.UTF8.GetString(csv.Content).Split("\r\n").Length);
        }
    }

    [Fact]
    public async Task ExportRangeEncodingInjectionMaskingAndNestedRedaction()
    {
        var id = Guid.Parse("09012345-1234-1234-1234-123456789012");
        var entry = Entry("a", 1, id) with
        {
            Reason = "  =SUM(1,2)\r\nĐiện thoại +84 901 234 567, \"ghi chú\"",
            Actor = new AuditActor(id, "\t@bad"),
            After = JsonSerializer.SerializeToElement(new { startDate = "2026-10-20", students = new[] {
                new { studentId = id, password = "NEVER_PASSWORD", token = "NEVER_TOKEN", securityStamp = "NEVER_STAMP" } },
                temporaryPassword = "NEVER_TEMP", refreshToken = "NEVER_REFRESH" })
        };
        var service = new AuditService([new MemorySource("a", [entry])]);
        var csv = await service.ExportAsync(new AuditFilter(At, At.AddDays(31)));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, csv.Content.Take(3));
        var text = Encoding.UTF8.GetString(csv.Content);
        Assert.Contains("'  =SUM(1,2)", text); Assert.Contains("'\t@bad", text);
        Assert.Contains("***567", text); Assert.DoesNotContain("901 234", text);
        Assert.Contains("Điện thoại", text); Assert.Contains("\"\"ghi chú\"\"", text);
        Assert.Contains(id.ToString(), text); Assert.Contains("2026-10-20", text);
        Assert.DoesNotContain("NEVER_", text);
        var range = await Assert.ThrowsAsync<AuditRequestException>(() => service.ExportAsync(new AuditFilter(At, At.AddDays(31).AddTicks(1))));
        Assert.Equal("AUDIT_EXPORT_RANGE_EXCEEDED", range.Code);
        Assert.Equal(422, range.Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<AuditRequestException>(() => service.ExportAsync(new AuditFilter(At.AddDays(1), At)))).Status);
        Assert.Equal("AUDIT_RANGE_REQUIRED", (await Assert.ThrowsAsync<AuditRequestException>(() => service.ExportAsync(new AuditFilter()))).Code);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+SUM(A1)")]
    [InlineData("-1+2")]
    [InlineData("@SUM(A1)")]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    [InlineData("\n=1+1")]
    [InlineData("\0=1+1")]
    [InlineData("\uFEFF=1+1")]
    [InlineData("\u200B=1+1")]
    public async Task CsvNeutralizesFormulaPrefixes(string value)
    {
        var entry = Entry("a", 1, Guid.Empty) with { Reason = value };
        var csv = await new AuditService([new MemorySource("a", [entry])]).ExportAsync(new AuditFilter(At, At.AddDays(1)));
        Assert.Contains("\"'" + value + "\"", Encoding.UTF8.GetString(csv.Content));
    }

    [Fact]
    public void PrivacyRejectsUnknownFieldsAndMalformedLegacyJson()
    {
        Assert.Null(AuditPrivacy.Snapshot("invalid JSON"));
        Assert.Null(AuditPrivacy.Snapshot("\"arbitrary secret\""));
        var value = AuditPrivacy.Snapshot("{\"sessions\":[\"NEVER_ARRAY_SECRET\",{\"id\":\"ok\",\"passwordHash\":\"NEVER_HASH\"}],\"reason\":\"{\\\"refreshToken\\\":\\\"NEVER_ENCODED\\\"}\"}");
        var json = value!.Value.GetRawText();
        Assert.Contains("ok", json); Assert.DoesNotContain("NEVER_", json);
        Assert.DoesNotContain("credential", AuditPrivacy.CleanText("password=credential refresh_token=credential SecurityStamp=credential"));
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Teacher)]
    [InlineData(RoleNames.KitchenStaff)]
    [InlineData(RoleNames.Parent)]
    [InlineData(null)]
    public async Task AllFourAuditRoutesEnforceAdminAndProblemDetails(string? role)
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        if (role is not null)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(seed.TeacherId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(user, RoleNames.Teacher)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.TeacherEmail, seed.Password));
        }
        foreach (var route in new[] { Root, $"{Root}/calendar:{Guid.NewGuid()}", $"{Root}/entities/Student/{Guid.NewGuid()}",
            Root + "/export?from=2026-10-01T00:00:00Z&to=2026-10-31T00:00:00Z&format=csv" })
        {
            var response = await client.GetAsync(route);
            var expected = role is null ? HttpStatusCode.Unauthorized : role != RoleNames.Admin ? HttpStatusCode.Forbidden :
                route.Contains("calendar:") ? HttpStatusCode.NotFound : HttpStatusCode.OK;
            Assert.Equal(expected, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            if (expected != HttpStatusCode.OK)
            {
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
                Assert.StartsWith("AUDIT_", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            }
        }
    }

    [Fact]
    public async Task SqliteAdaptersMapOnlyRecordedFactsAndProvenStudentTimeline() => await VerifyAdapters(false);
    [PostgresFact]
    public async Task PostgreSqlAdaptersTranslateFiltersAndProvenStudentTimeline() => await VerifyAdapters(true);

    private static async Task VerifyAdapters(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres: postgres);
        var seed = await factory.SeedUsersAsync();
        Guid studentId, calendarId, exceptionId, amendmentId, resetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var student = new Student { FullName = "Trẻ audit", ClassId = seed.ClassId, IsActive = false };
            db.Students.Add(student); studentId = student.Id;
            var day = new MealDay { Date = new DateOnly(2026, 10, 20), MealType = "Lunch", SchoolYear = "2026-2027", CutoffAt = At };
            db.MealDays.Add(day);
            db.Enrollments.Add(new Enrollment { StudentId = studentId, ClassId = seed.ClassId, StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 10, 21), RecordedAt = At, EndRecordedAt = At.AddHours(5), RecordedByUserId = seed.TeacherId,
                EndedByUserId = seed.TeacherId, Reason = "Ghi danh", EndReason = "Kết thúc" });
            var calendar = new MealCalendarAudit { SchoolYear = "2026-2027", Kind = "GENERATE", ActorId = seed.TeacherId, ActorName = "Tên đã lưu",
                RecordedAt = At, Reason = "Tạo lịch", BeforeJson = "null", AfterJson = "{\"sessions\":[{\"id\":\"safe\",\"password\":\"NEVER_SECRET\",\"students\":[{\"token\":\"NEVER_NESTED\",\"studentId\":\"safe\"}]}]}" };
            db.MealCalendarAudits.Add(calendar); calendarId = calendar.Id;
            var reset = new AccountPasswordResetAudit { UserId = seed.TeacherId, PerformedByUserId = seed.TeacherId, PerformedAt = At, Reason = "Đối chiếu hồ sơ" };
            db.AccountPasswordResetAudits.Add(reset); resetId = reset.Id;
            var exception = new MealRegistration { StudentId = studentId, MealDayId = day.Id, RecordedAt = At.AddHours(1), WillEat = null, Sequence = 1 };
            db.MealRegistrations.Add(exception); exceptionId = exception.Id;
            db.MealAbsences.Add(new MealAbsence { StudentId = studentId, ReportedByUserId = seed.TeacherId, ReportedAt = At,
                CancelledAt = At.AddHours(1), FromDate = day.Date, ToDate = day.Date, Reason = "Nghỉ" });
            var settlement = new PortionSettlement { MealDayId = day.Id, ClassId = seed.ClassId, Count = 1, SettledAt = At.AddHours(2),
                CutoffAt = At, SettledBy = seed.TeacherId.ToString(), Decisions = [new SettlementDecision { StudentId = studentId,
                    StudentName = "Trẻ audit", StudentCode = "A", WillEat = true, Source = "Enrollment" }] };
            db.PortionSettlements.Add(settlement);
            var amendment = new PortionAmendment { BaseSettlement = settlement, StudentId = Guid.Empty, StudentName = "", StudentCode = "",
                Reason = "Điều chỉnh", RequestedBy = seed.TeacherId, RequestedByName = "Tên yêu cầu", RequestedAt = At.AddHours(3),
                Students = [new PortionAmendmentStudent { StudentId = studentId, StudentName = "Trẻ audit", StudentCode = "A" }] };
            db.PortionAmendments.Add(amendment); amendmentId = amendment.Id;
            db.PortionAmendmentResolutions.Add(new PortionAmendmentResolution { Amendment = amendment, Approved = false,
                Reason = "Giữ nguyên", ReviewedAt = At.AddHours(4), ReviewedBy = seed.TeacherId, ReviewedByName = "Tên duyệt" });
            db.ParentLinkRequests.Add(new ParentLinkRequest { StudentId = studentId, ParentId = seed.TeacherId, RequestedAt = At,
                ReviewedAt = At.AddHours(1), ReviewedByUserId = seed.TeacherId, Status = "REVOKED", RevokedAt = At.AddHours(2),
                RevokedByUserId = seed.TeacherId, ReviewReason = "Duyệt", RevocationReason = "Thu hồi" });
            db.StudentImportBatches.Add(new StudentImportBatch { ClassId = seed.ClassId, ClassName = "Lớp test", SchoolYear = "2026-2027",
                FileName = "unused.xlsx", SheetName = "Sheet1", ImportedByUserId = seed.TeacherId, ImportedAt = At, StartDate = day.Date, Created = 1,
                Items = [new StudentImportBatchItem { StudentId = studentId, SourceRow = 2, FullName = "Trẻ audit" }] });
            await db.SaveChangesAsync();
        }
        using var check = factory.Services.CreateScope();
        var service = check.ServiceProvider.GetRequiredService<AuditService>();
        var sources = check.ServiceProvider.GetServices<IAuditSource>().ToArray();
        var all = await service.ListAsync(new AuditFilter(At, At.AddDays(1)), pageSize: 100);
        Assert.Equal(14, all.Total);
        var paged = new List<AuditEntry>();
        for (var page = 1; page <= 4; page++) paged.AddRange((await service.ListAsync(new AuditFilter(At, At.AddDays(1)), page, 4)).Items);
        Assert.Equal(all.Items.Select(x => x.Id), paged.Select(x => x.Id));
        Assert.Equal(sources.Select(x => x.Name).Order(), all.Items.Select(x => x.Source).Order());
        foreach (var source in sources)
        {
            var entry = Assert.Single(all.Items, x => x.Source == source.Name);
            Assert.Equal(entry.Id, (await service.DetailAsync(entry.Id)).Id);
            var filtered = await service.ListAsync(new AuditFilter(At, At.AddDays(1), entry.Actor?.Id, Action: entry.Action));
            Assert.Contains(filtered.Items, x => x.Id == entry.Id);
        }
        var timeline = await service.ListAsync(new AuditFilter(EntityType: "Student", EntityId: studentId.ToString()), pageSize: 100);
        Assert.Equal(11, timeline.Total);
        Assert.Contains(timeline.Items, x => x.Source == "enrollment");
        Assert.Contains(timeline.Items, x => x.Id == $"meal-exception:{exceptionId}");
        Assert.Contains(timeline.Items, x => x.Id == $"amendment:{amendmentId}");
        Assert.DoesNotContain(timeline.Items, x => x.Source is "calendar" or "password-reset" or "parent-link");
        Assert.Empty((await service.ListAsync(new AuditFilter(EntityType: "Student", EntityId: Guid.NewGuid().ToString()))).Items);
        var legacy = await service.DetailAsync($"meal-exception:{exceptionId}");
        Assert.Null(legacy.Actor); Assert.Null(legacy.Reason); Assert.Null(legacy.Before); Assert.Null(legacy.CorrelationId);
        Assert.Equal("meal.exception.restore", legacy.Action);
        var password = await service.DetailAsync($"password-reset:{resetId}");
        Assert.Null(password.Before); Assert.Null(password.After); Assert.Null(password.Actor!.Name);
        Assert.Equal("Tên đã lưu", (await service.DetailAsync($"calendar:{calendarId}")).Actor!.Name);
        var membership = Assert.Single(all.Items, x => x.Source == "settlement").After!.Value.GetProperty("decisions");
        Assert.Equal(studentId, membership[0].GetProperty("studentId").GetGuid());
        Assert.Equal("Enrollment", membership[0].GetProperty("source").GetString());
        Assert.DoesNotContain("NEVER_", JsonSerializer.Serialize(all));
        var csv = await service.ExportAsync(new AuditFilter(At, At.AddDays(1)));
        Assert.DoesNotContain("NEVER_", Encoding.UTF8.GetString(csv.Content));

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        foreach (var route in new[] { Root, $"{Root}/calendar:{calendarId}", $"{Root}/entities/Student/{studentId}" })
        {
            var response = await client.GetAsync(route); response.EnsureSuccessStatusCode();
            Assert.DoesNotContain("NEVER_", await response.Content.ReadAsStringAsync());
        }
        foreach (var query in new[] { "page=bad", "page=0", "pageSize=101", "actorId=bad", "from=yesterday", "from=2026-10-20", "page=1&page=2" })
        {
            var response = await client.GetAsync(Root + "?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        }
    }

    private static AuditEntry Entry(string source, int i, Guid actor) => new($"{source}:{new Guid(i, 0, 0, new byte[8]):D}", source,
        At.AddMinutes(i % 4), new AuditActor(actor, "Tên"), "test", "Student", actor.ToString(), null, null, null, null);

    private sealed class MemorySource(string name, AuditEntry[] rows) : IAuditSource
    {
        public string Name => name;
        public int Reads { get; private set; }
        private IEnumerable<AuditEntry> Filter(AuditFilter f) => rows.Where(x =>
            (f.From is null || x.OccurredAt >= f.From) && (f.To is null || x.OccurredAt <= f.To) &&
            (f.ActorId is null || x.Actor?.Id == f.ActorId) && (f.EntityType is null || x.EntityType == f.EntityType) &&
            (f.EntityId is null || x.EntityId == f.EntityId) && (f.Action is null || x.Action == f.Action));
        public Task<long> CountAsync(AuditFilter f, CancellationToken ct) => Task.FromResult(Filter(f).LongCount());
        public Task<AuditEntry?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(rows.SingleOrDefault(x => x.Id == $"{name}:{id:D}"));
        public Task<IReadOnlyList<AuditEntry>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            var keys = ids.Select(id => $"{name}:{id:D}").ToHashSet();
            return Task.FromResult<IReadOnlyList<AuditEntry>>(rows.Where(e => keys.Contains(e.Id)).ToArray());
        }
        public Task<IReadOnlyList<AuditKey>> ReadKeysAsync(AuditFilter f, int offset, int take, CancellationToken ct)
        {
            Assert.InRange(take, 1, 128); Reads++;
            return Task.FromResult<IReadOnlyList<AuditKey>>(Filter(f).OrderByDescending(x => x.OccurredAt)
                .ThenBy(x => x.Id, StringComparer.Ordinal).Skip(offset).Take(take)
                .Select(x => new AuditKey(Guid.Parse(x.Id[(name.Length + 1)..]), name, x.OccurredAt)).ToArray());
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class MealQueryOptimizationTests
{
    [Fact]
    public Task SqlitePaginationKeepsDropdownScopeAndLiteralSearch() => VerifyPagination(false);
    [PostgresFact]
    public Task PostgresPaginationKeepsDropdownScopeAndLiteralSearch() => VerifyPagination(true);

    private static async Task VerifyPagination(bool postgres)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        var date = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var expected = new HashSet<Guid>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            for (var i = 0; i < 35; i++)
            {
                var child = new Student { FullName = i == 34 ? "Kid %_ literal" : $"Kid {i:D3}", ClassId = seed.ClassId,
                    StudentCode = $"PAGE-{i:D3}", Enrollments = [new() { ClassId = seed.ClassId,
                        StartDate = date.AddDays(-1), RecordedAt = clock.GetUtcNow().AddDays(-1) }] };
                db.Students.Add(child); expected.Add(child.Id);
            }
            await db.SaveChangesAsync();
        }
        await Authorize(client, seed.AdminEmail, seed.Password);
        var seen = new HashSet<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions?classId={seed.ClassId}&search=KiD&page={page}&pageSize=7");
            Assert.Equal(35, body.GetProperty("total").GetInt32());
            Assert.Equal(2, body.GetProperty("classes").GetArrayLength());
            Assert.Equal(7, body.GetProperty("items").GetArrayLength());
            foreach (var child in body.GetProperty("items").EnumerateArray())
            { Assert.Equal(seed.ClassId, child.GetProperty("classId").GetGuid()); Assert.True(seen.Add(child.GetProperty("studentId").GetGuid())); }
        }
        Assert.True(expected.SetEquals(seen));
        var literal = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions?search={Uri.EscapeDataString("%_")}");
        Assert.Equal(1, literal.GetProperty("total").GetInt32());
        var empty = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions?search=missing&page=500");
        Assert.Equal(0, empty.GetProperty("total").GetInt32()); Assert.Equal(2, empty.GetProperty("classes").GetArrayLength());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        await Authorize(client, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/meal-days/{seed.DayId}/decisions?classId={seed.ForeignClassId}")).StatusCode);
        var teacher = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions?pageSize=7");
        Assert.Equal(37, teacher.GetProperty("total").GetInt32()); Assert.Single(teacher.GetProperty("classes").EnumerateArray());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.TeacherAssignments.RemoveRange(await db.TeacherAssignments.ToListAsync()); await db.SaveChangesAsync();
        }
        var noClasses = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/portions");
        Assert.Empty(noClasses.GetProperty("classes").EnumerateArray());
        var noDecisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions");
        Assert.Equal(0, noDecisions.GetProperty("total").GetInt32()); Assert.Empty(noDecisions.GetProperty("classes").EnumerateArray());
    }

    [Fact]
    public Task SqliteLatestSnapshotPreservesFrozenNamesOriginalCountAndLegacyCount() => VerifySnapshot(false);
    [PostgresFact]
    public Task PostgresLatestSnapshotPreservesFrozenNamesOriginalCountAndLegacyCount() => VerifySnapshot(true);

    private static async Task VerifySnapshot(bool postgres)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password); clock.Set(seed.Cutoff);
        (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();
        Guid latestId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var root = await db.PortionSettlements.SingleAsync(x => x.MealDayId == seed.DayId && x.ClassId == seed.ClassId);
            Guid previous = root.Id;
            for (var version = 2; version <= 3; version++)
            {
                var row = new PortionSettlement { MealDayId = seed.DayId, ClassId = seed.ClassId, ClassName = "Frozen class",
                    Version = version, SupersedesId = previous, Count = version == 3 ? 7 : 2, CutoffAt = seed.Cutoff,
                    SettledAt = clock.GetUtcNow(), SettledBy = seed.TeacherId.ToString(),
                    Students = [new() { StudentId = seed.PresentId, StudentName = "Frozen child" }, new() { StudentId = seed.AbsentId, StudentName = "Frozen second" }] };
                db.PortionSettlements.Add(row); previous = row.Id;
            }
            latestId = previous; await db.SaveChangesAsync();
        }
        await Authorize(client, seed.TeacherEmail, seed.Password);
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/portions");
        var room = Assert.Single(body.GetProperty("classes").EnumerateArray());
        Assert.Equal(seed.ClassId, room.GetProperty("classId").GetGuid()); Assert.Equal("Frozen class", room.GetProperty("className").GetString());
        Assert.Equal(latestId, room.GetProperty("settlementId").GetGuid()); Assert.Equal(3, room.GetProperty("version").GetInt32());
        Assert.Equal(7, room.GetProperty("count").GetInt32()); Assert.Equal(1, room.GetProperty("originalCount").GetInt32());
        var names = room.GetProperty("studentIds").EnumerateArray().Select(x => x.GetGuid())
            .Zip(room.GetProperty("studentNames").EnumerateArray().Select(x => x.GetString())).ToDictionary(x => x.First, x => x.Second);
        Assert.Equal("Frozen child", names[seed.PresentId]); Assert.Equal("Frozen second", names[seed.AbsentId]);
        await Authorize(client, seed.AdminEmail, seed.Password);
        var all = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/portions");
        Assert.Equal(2, all.GetProperty("classes").GetArrayLength());
        using var final = factory.Services.CreateScope(); var check = final.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(1, (await check.PortionSettlements.SingleAsync(x => x.MealDayId == seed.DayId && x.ClassId == seed.ClassId && x.Version == 1)).Count);
    }

    [Fact]
    public Task SqliteResolverKeepsLegacyYearBoundsAndLatestValidDefault() => VerifySources(false);
    [PostgresFact]
    public Task PostgresResolverKeepsLegacyYearBoundsAndLatestValidDefault() => VerifySources(true);

    private static async Task VerifySources(bool postgres)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        var date = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var expected = new Dictionary<Guid, (bool Eat, string Source)> { [seed.PresentId] = (true, "DEFAULT"),
            [seed.ForeignId] = (true, "DEFAULT"), [seed.AbsentId] = (false, "PARENT_ABSENCE") };
        Guid restoreId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.SchoolYear = null;
            var parentId = (await db.MealAbsences.SingleAsync()).ReportedByUserId;
            var year = (await db.AcademicYears.FindAsync("2026-2027"))!; year.StartDate = date.AddDays(-30); year.EndDate = date.AddDays(30);
            var unknown = new SchoolClass { Name = "Missing year", SchoolYear = "2010-2011" };
            var old = new SchoolClass { Name = "Old year", SchoolYear = "2025-2026" };
            db.Classes.AddRange(unknown, old); db.AcademicYears.Add(new() { Code = old.SchoolYear, StartDate = date.AddDays(-1000), EndDate = date.AddDays(-500) });
            foreach (var kind in new[] { "legacy", "mismatch", "missing", "outside" })
            {
                var classId = kind == "missing" ? unknown.Id : kind == "outside" ? old.Id : seed.ClassId;
                var child = new Student { FullName = kind, ClassId = classId,
                    Enrollments = [new() { ClassId = classId, StartDate = date.AddDays(-1), RecordedAt = clock.GetUtcNow().AddDays(-1) }] };
                db.Students.Add(child);
                db.MealAbsences.Add(new() { StudentId = child.Id, ReportedByUserId = parentId, FromDate = kind is "legacy" or "missing" ? date.AddDays(-365) : date,
                    ToDate = date.AddDays(1), SchoolYear = kind is "legacy" or "missing" ? null : "2025-2026",
                    ReportedAt = clock.GetUtcNow().AddMinutes(-1), Reason = "Year rule" });
                expected.Add(child.Id, kind == "missing" ? (false, "PARENT_ABSENCE") : (true, "DEFAULT"));
            }
            var restore = new MealRegistration { MealDayId = seed.DayId, StudentId = seed.AbsentId, Sequence = 2,
                WillEat = null, RecordedAt = clock.GetUtcNow().AddMinutes(-1) }; restoreId = restore.Id;
            db.MealRegistrations.AddRange(new() { MealDayId = seed.DayId, StudentId = seed.AbsentId, Sequence = 1,
                WillEat = true, RecordedAt = clock.GetUtcNow().AddMinutes(-2) }, restore,
                new() { MealDayId = seed.DayId, StudentId = seed.AbsentId, Sequence = 3, WillEat = true, RecordedAt = seed.Cutoff.AddMinutes(1) });
            await db.SaveChangesAsync();
        }
        clock.Set(seed.Cutoff.AddMinutes(2)); await Authorize(client, seed.AdminEmail, seed.Password);
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions?pageSize=100");
        Assert.Equal(expected.Count, body.GetProperty("total").GetInt32());
        foreach (var row in body.GetProperty("items").EnumerateArray())
        {
            var answer = expected[row.GetProperty("studentId").GetGuid()];
            Assert.Equal(answer.Eat, row.GetProperty("willEat").GetBoolean()); Assert.Equal(answer.Source, row.GetProperty("source").GetString());
            if (row.GetProperty("studentId").GetGuid() == seed.AbsentId)
            { Assert.Equal(restoreId, row.GetProperty("latestEventId").GetGuid()); Assert.Equal("DEFAULT", row.GetProperty("latestAction").GetString()); }
        }
        var settled = await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null); settled.EnsureSuccessStatusCode();
        Assert.Equal(5, (await settled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        using var final = factory.Services.CreateScope(); var database = final.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(7, await database.SettlementDecisions.CountAsync()); Assert.Equal(5, await database.SettlementStudents.CountAsync());
        var absentDecision = await database.SettlementDecisions.SingleAsync(x => x.StudentId == seed.AbsentId);
        Assert.False(absentDecision.WillEat); Assert.Equal(restoreId, absentDecision.ExceptionId); Assert.NotNull(absentDecision.AbsenceId);
    }
}

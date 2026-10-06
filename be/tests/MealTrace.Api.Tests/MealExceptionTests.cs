using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class MealExceptionTests
{
    internal sealed class TestClock : TimeProvider
    {
        private long _ticks = DateTimeOffset.UtcNow.UtcTicks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        public void Set(DateTimeOffset value) => Interlocked.Exchange(ref _ticks, value.UtcTicks);
    }

    internal sealed record Scenario(Guid DayId, Guid PresentId, Guid AbsentId, Guid ForeignId, Guid ClassId,
        Guid ForeignClassId, Guid TeacherId, string AdminEmail, string TeacherEmail, string Password, DateTimeOffset Cutoff);

    internal static async Task<Scenario> SeedScenario(AuthTestFactory factory, TestClock clock)
    {
        var seed = await factory.SeedUsersAsync();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var parent = new ApplicationUser { FullName = "Parent", UserName = "parent@test.local", Email = "parent@test.local" };
        Assert.True((await manager.CreateAsync(parent, seed.Password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
        var other = new SchoolClass { Name = "Other class", SchoolYear = "2026-2027" }; db.Classes.Add(other);
        var date = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        Student Child(string name, Guid classId) => new() { FullName = name, ClassId = classId,
            Enrollments = [new Enrollment { ClassId = classId, StartDate = date.AddDays(-1), RecordedAt = clock.GetUtcNow().AddHours(-1) }] };
        var present = Child("Present child", seed.ClassId); var absent = Child("Absent child", seed.ClassId); var foreign = Child("Foreign child", other.Id);
        db.Students.AddRange(present, absent, foreign);
        db.TeacherAssignments.Add(new TeacherAssignment { UserId = seed.TeacherId, ClassId = seed.ClassId });
        var cutoff = clock.GetUtcNow().AddMinutes(20);
        var day = new MealDay { Date = date, SchoolYear = "2026-2027", MealType = "Lunch", CutoffAt = cutoff }; db.MealDays.Add(day);
        db.ParentStudents.AddRange(new ParentStudent { UserId = parent.Id, StudentId = present.Id }, new ParentStudent { UserId = parent.Id, StudentId = absent.Id });
        db.MealAbsences.Add(new MealAbsence { StudentId = absent.Id, ReportedByUserId = parent.Id, FromDate = date, ToDate = date,
            Reason = "Parent report", ReportedAt = clock.GetUtcNow().AddMinutes(-1) });
        await db.SaveChangesAsync();
        return new(day.Id, present.Id, absent.Id, foreign.Id, seed.ClassId, other.Id, seed.TeacherId, seed.AdminEmail, seed.TeacherEmail, seed.Password, cutoff);
    }

    internal static async Task Authorize(HttpClient client, string email, string password) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, email, password));

    internal static Task<HttpResponseMessage> Record(HttpClient client, Guid day, Guid student, string action, Guid? expected = null, string reason = "Reason") =>
        client.PostAsJsonAsync($"/api/meal-days/{day}/exceptions", new { studentId = student, action, reason, expectedEventId = expected });
    internal static async Task<Guid> EventId(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); var json = await response.Content.ReadFromJsonAsync<JsonElement>(); return json.GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Decision(HttpClient client, Scenario seed, Guid student)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions");
        return page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == student);
    }

    [Fact]
    public async Task RestoringDefaultRespectsParentAbsenceAndKeepsActorAndEventHistory()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.TeacherEmail, seed.Password);
        var initial = await Decision(client, seed, seed.AbsentId); Assert.False(initial.GetProperty("willEat").GetBoolean());
        Assert.Equal("PARENT_ABSENCE", initial.GetProperty("source").GetString());
        var eat = await EventId(await Record(client, seed.DayId, seed.AbsentId, "EAT"));
        Assert.True((await Decision(client, seed, seed.AbsentId)).GetProperty("willEat").GetBoolean());
        var restored = await EventId(await Record(client, seed.DayId, seed.AbsentId, "DEFAULT", eat));
        var after = await Decision(client, seed, seed.AbsentId); Assert.False(after.GetProperty("willEat").GetBoolean());
        Assert.Equal("PARENT_ABSENCE", after.GetProperty("source").GetString()); Assert.Equal(restored, after.GetProperty("latestEventId").GetGuid());
        var absent = await EventId(await Record(client, seed.DayId, seed.PresentId, "ABSENT"));
        Assert.False((await Decision(client, seed, seed.PresentId)).GetProperty("willEat").GetBoolean());
        await EventId(await Record(client, seed.DayId, seed.PresentId, "DEFAULT", absent));
        Assert.True((await Decision(client, seed, seed.PresentId)).GetProperty("willEat").GetBoolean());
        var portions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/portions");
        var room = Assert.Single(portions.GetProperty("classes").EnumerateArray());
        Assert.Single(room.GetProperty("studentIds").EnumerateArray());
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/students/{seed.AbsentId}/exceptions");
        var events = history.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(2, events.Length);
        Assert.Equal("DEFAULT", events[0].GetProperty("action").GetString()); Assert.Equal(eat, events[0].GetProperty("supersedesId").GetGuid());
        Assert.Equal(seed.TeacherId, events[0].GetProperty("recordedByUserId").GetGuid()); Assert.False(events[0].GetProperty("isLegacy").GetBoolean());
        Assert.Equal("Giáo viên thử nghiệm", events[0].GetProperty("actorName").GetString());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(4, await db.MealRegistrations.CountAsync());
        Assert.True(await db.MealAbsences.AnyAsync(x => x.StudentId == seed.AbsentId && x.CancelledAt == null));
    }

    [Fact]
    public async Task StaleVersionAndInvalidInputsDoNotAppendEvents()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        Assert.Equal(HttpStatusCode.BadRequest, (await Record(client, seed.DayId, seed.PresentId, "INVALID")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Record(client, seed.DayId, seed.PresentId, "EAT", reason: " ")).StatusCode);
        var first = await EventId(await Record(client, seed.DayId, seed.PresentId, "ABSENT"));
        Assert.Equal(HttpStatusCode.Conflict, (await Record(client, seed.DayId, seed.PresentId, "EAT")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Record(client, seed.DayId, seed.PresentId, "DEFAULT", Guid.NewGuid())).StatusCode);
        var decision = await Decision(client, seed, seed.PresentId); Assert.Equal(first, decision.GetProperty("latestEventId").GetGuid());
        using var scope = factory.Services.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().MealRegistrations.CountAsync());
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.OK)]
    [InlineData(0, HttpStatusCode.Conflict)]
    [InlineData(1, HttpStatusCode.Conflict)]
    public async Task CutoffIsCheckedBeforeAtAndAfterExactBoundary(int ticks, HttpStatusCode expected)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password); clock.Set(seed.Cutoff.AddTicks(ticks));
        Assert.Equal(expected, (await Record(client, seed.DayId, seed.PresentId, "ABSENT")).StatusCode);
    }

    [Fact]
    public async Task TeacherScopeUsesClassOnMealDateAndProtectsHistory()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        var date = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/admin/students/{seed.PresentId}/enrollments", new
            { classId = seed.ForeignClassId, effectiveDate = date, reason = "Transfer", revision = 0 })).StatusCode);
        await Authorize(client, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.OK, (await Record(client, seed.DayId, seed.PresentId, "ABSENT")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Record(client, seed.DayId, seed.ForeignId, "ABSENT")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/meal-days/{seed.DayId}/students/{seed.ForeignId}/exceptions")).StatusCode);
        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions"); Assert.Equal(2, decisions.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/meal-days/{seed.DayId}/decisions?classId={seed.ForeignClassId}")).StatusCode);
    }

    [Fact]
    public async Task LegacyHistoryStaysUnknownWhileNewEventsHaveActorAndSourceVersion()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock); Guid legacyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var legacy = new MealRegistration { MealDayId = seed.DayId, StudentId = seed.PresentId, WillEat = false, Sequence = 1,
                Reason = "Legacy reason", RecordedAt = clock.GetUtcNow().AddMinutes(-1) }; db.MealRegistrations.Add(legacy); legacyId = legacy.Id; await db.SaveChangesAsync();
        }
        await Authorize(client, seed.AdminEmail, seed.Password);
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/students/{seed.PresentId}/exceptions");
        var old = Assert.Single(before.GetProperty("items").EnumerateArray()); Assert.True(old.GetProperty("isLegacy").GetBoolean()); Assert.Equal(JsonValueKind.Null, old.GetProperty("actorName").ValueKind);
        var newId = await EventId(await Record(client, seed.DayId, seed.PresentId, "DEFAULT", legacyId));
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/students/{seed.PresentId}/exceptions?pageSize=1");
        Assert.Equal(2, history.GetProperty("total").GetInt32()); var latest = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal(newId, latest.GetProperty("id").GetGuid()); Assert.False(latest.GetProperty("isLegacy").GetBoolean());
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/students/{seed.PresentId}/exceptions?page=2&pageSize=1");
        Assert.Equal(legacyId, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions?search=Present&pageSize=1");
        Assert.Equal(1, decisions.GetProperty("total").GetInt32()); Assert.Equal(seed.PresentId, Assert.Single(decisions.GetProperty("items").EnumerateArray()).GetProperty("studentId").GetGuid());
    }

    [Theory]
    [InlineData(RoleNames.Parent)]
    [InlineData(RoleNames.KitchenStaff)]
    public async Task ParentAndKitchenCannotReadOrWriteExceptions(string role)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { FullName = "Restricted", Email = "restricted@test.local", UserName = "restricted@test.local" };
            Assert.True((await manager.CreateAsync(user, seed.Password)).Succeeded); Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        await Authorize(client, "restricted@test.local", seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await Record(client, seed.DayId, seed.PresentId, "ABSENT")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/meal-days/{seed.DayId}/decisions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/meal-days/{seed.DayId}/students/{seed.PresentId}/exceptions")).StatusCode);
    }

    [Fact]
    public async Task SettlementAndPastCutoffBlockChangesWhileHistoryRemainsReadable()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password); var first = await EventId(await Record(client, seed.DayId, seed.PresentId, "ABSENT"));
        clock.Set(seed.Cutoff); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).StatusCode);
        var before = await client.GetStringAsync($"/api/meal-days/{seed.DayId}/portions");
        Assert.Equal(HttpStatusCode.Conflict, (await Record(client, seed.DayId, seed.PresentId, "EAT", first)).StatusCode);
        clock.Set(seed.Cutoff.AddMinutes(-1)); // Even if the clock moves backwards, an already settled day stays closed.
        Assert.Equal(HttpStatusCode.Conflict, (await Record(client, seed.DayId, seed.PresentId, "DEFAULT", first)).StatusCode);
        Assert.Equal(before, await client.GetStringAsync($"/api/meal-days/{seed.DayId}/portions"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/meal-days/{seed.DayId}/students/{seed.PresentId}/exceptions")).StatusCode);
    }
}

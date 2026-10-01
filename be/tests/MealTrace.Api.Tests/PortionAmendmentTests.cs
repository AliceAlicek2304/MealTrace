using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class PortionAmendmentTests
{
    private static string Route(Scenario seed) => $"/api/meal-days/{seed.DayId}/amendments";
    private static async Task<Guid> Base(HttpClient client, Scenario seed) =>
        (await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId)).GetProperty("current").GetProperty("id").GetGuid();
    private static Task<HttpResponseMessage> Submit(HttpClient client, Scenario seed, Guid source, Guid child, bool willEat, string reason = "Meal correction") =>
        client.PostAsJsonAsync(Route(seed), new { classId = seed.ClassId, studentId = child, baseSettlementId = source, willEat, reason });
    private static Task<HttpResponseMessage> Review(HttpClient client, Scenario seed, Guid requestId, bool approve, string reason = "Reviewed school records") =>
        client.PostAsJsonAsync(Route(seed) + $"/{requestId}/review", new { approve, reason });
    private static async Task<Scenario> Setup(AuthTestFactory factory, TestClock clock, HttpClient client)
    {
        var seed = await SeedScenario(factory, clock); await Authorize(client, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff); (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();
        return seed;
    }

    [Fact]
    public async Task ApprovalCreatesNewVersionPreservesOriginalAndKitchenReadsAppliedTotal()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var originalId = await Base(client, seed);
        await Authorize(client, seed.TeacherEmail, seed.Password);
        var requestId = await EventId(await Submit(client, seed, originalId, seed.AbsentId, true));
        Assert.Equal(originalId, await Base(client, seed)); // pending never changes portions
        Assert.Equal(HttpStatusCode.Forbidden, (await Review(client, seed, requestId, true)).StatusCode);
        await Authorize(client, seed.AdminEmail, seed.Password); (await Review(client, seed, requestId, true)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Review(client, seed, requestId, true)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var snapshots = await db.PortionSettlements.Include(x => x.Students).Include(x => x.Decisions).Where(x => x.ClassId == seed.ClassId).OrderBy(x => x.Version).ToListAsync();
        Assert.Equal(2, snapshots.Count); Assert.Equal(1, snapshots[0].Count); Assert.Equal(originalId, snapshots[0].Id);
        Assert.Single(snapshots[0].Students); Assert.Equal(2, snapshots[0].Decisions.Count);
        var absent = snapshots[0].Decisions.Single(x => x.StudentId == seed.AbsentId);
        Assert.False(absent.WillEat); Assert.Equal("PARENT_ABSENCE", absent.Source); Assert.NotNull(absent.AbsenceId); Assert.NotNull(absent.EnrollmentId);
        Assert.Equal(2, snapshots[1].Version); Assert.Equal(2, snapshots[1].Count); Assert.Equal(originalId, snapshots[1].SupersedesId);
        Assert.Equal(requestId, snapshots[1].Decisions.Single(x => x.StudentId == seed.AbsentId).AmendmentId);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var kitchen = new ApplicationUser { UserName = "kitchen@test.local", Email = "kitchen@test.local", FullName = "Kitchen" };
        Assert.True((await users.CreateAsync(kitchen, seed.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(kitchen, RoleNames.KitchenStaff)).Succeeded);
        await Authorize(client, kitchen.Email!, seed.Password);
        var state = await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId);
        Assert.False(state.GetProperty("canRequest").GetBoolean()); Assert.Single(state.GetProperty("added").EnumerateArray());
        Assert.Equal("APPROVED", state.GetProperty("items")[0].GetProperty("status").GetString());
        var portions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/portions");
        var room = portions.GetProperty("classes").EnumerateArray().Single(x => x.GetProperty("classId").GetGuid() == seed.ClassId);
        Assert.Equal(2, room.GetProperty("studentIds").GetArrayLength()); Assert.Equal(1, room.GetProperty("originalCount").GetInt32());
        var days = await client.GetFromJsonAsync<JsonElement>("/api/meal-days");
        Assert.Equal(3, days[0].GetProperty("settledPortions").GetInt32()); // other class retains one; no double-counting versions
        var dayDetail = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}");
        Assert.Equal(3, dayDetail.GetProperty("settledPortions").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await Submit(client, seed, snapshots[1].Id, seed.PresentId, false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Review(client, seed, requestId, false)).StatusCode);
    }

    [Fact]
    public async Task RejectionKeepsCountAndStalePendingRequestCannotBeApproved()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var source = await Base(client, seed);
        var add = await EventId(await Submit(client, seed, source, seed.AbsentId, true));
        var remove = await EventId(await Submit(client, seed, source, seed.PresentId, false));
        (await Review(client, seed, add, true)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Review(client, seed, remove, true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(client, seed, source, seed.PresentId, false)).StatusCode);
        (await Review(client, seed, remove, false)).EnsureSuccessStatusCode();
        var current = await Base(client, seed); var again = await EventId(await Submit(client, seed, current, seed.PresentId, false));
        (await Review(client, seed, again, true)).EnsureSuccessStatusCode();
        var state = await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId);
        Assert.Equal(3, state.GetProperty("current").GetProperty("version").GetInt32()); Assert.Equal(1, state.GetProperty("current").GetProperty("count").GetInt32());
        Assert.Single(state.GetProperty("removed").EnumerateArray());
        var detail = await client.GetFromJsonAsync<JsonElement>(Route(seed) + $"/{again}");
        Assert.Equal(2, detail.GetProperty("before").GetProperty("version").GetInt32()); Assert.Equal(3, detail.GetProperty("after").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task ScopeInvalidStudentDuplicateNoOpAndEmptyReasonAreRejected()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var source = await Base(client, seed);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(client, seed, source, seed.ForeignId, false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(client, seed, source, seed.PresentId, true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(client, seed, source, seed.PresentId, false, " ")).StatusCode);
        var requestId = await EventId(await Submit(client, seed, source, seed.AbsentId, true));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(client, seed, source, seed.AbsentId, true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Review(client, seed, requestId, true, " ")).StatusCode);
        await Authorize(client, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route(seed) + "?classId=" + seed.ForeignClassId)).StatusCode);
        var foreignSource = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Route(seed), new { classId = seed.ForeignClassId,
            studentId = seed.ForeignId, baseSettlementId = foreignSource, willEat = false, reason = "Reason" })).StatusCode);
        await Authorize(client, "parent@test.local", seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route(seed) + "?classId=" + seed.ClassId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Submit(client, seed, source, seed.AbsentId, true)).StatusCode);
    }

    [Fact]
    public async Task BeforeSettlementIsRejectedAndLateParentChangesNeverRewriteFrozenSources()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); await Authorize(client, seed.AdminEmail, seed.Password);
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(client, seed, Guid.NewGuid(), seed.PresentId, false)).StatusCode);
        clock.Set(seed.Cutoff); (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();
        var original = await Base(client, seed);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealAbsences.SingleAsync()).CancelledAt = clock.GetUtcNow().AddMinutes(1);
            (await db.Students.FindAsync(seed.AbsentId))!.FullName = "Renamed after cutoff"; await db.SaveChangesAsync();
        }
        var state = await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId);
        var candidate = state.GetProperty("candidates").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.AbsentId);
        Assert.False(candidate.GetProperty("willEat").GetBoolean()); Assert.Equal("Absent child", candidate.GetProperty("studentName").GetString());
        Assert.Equal("PARENT_ABSENCE", candidate.GetProperty("source").GetString());
        Assert.Equal(original, await Base(client, seed));
    }

    [Fact]
    public async Task LegacySnapshotIsMarkedAndCanBeAmendedWithoutInventingSources()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); await Authorize(client, seed.AdminEmail, seed.Password); clock.Set(seed.Cutoff);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.SettledAt = clock.GetUtcNow();
            db.PortionSettlements.Add(new PortionSettlement { MealDayId = seed.DayId, ClassId = seed.ClassId, ClassName = "Historical class",
                Count = 1, SettledBy = "legacy", CutoffAt = seed.Cutoff, Students = [new SettlementStudent { StudentId = seed.PresentId, StudentName = "Historical child" }] });
            await db.SaveChangesAsync();
        }
        var state = await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId);
        Assert.False(state.GetProperty("hasOriginalSources").GetBoolean()); Assert.Empty(state.GetProperty("original").GetProperty("decisions").EnumerateArray());
        var source = await Base(client, seed); var requestId = await EventId(await Submit(client, seed, source, seed.PresentId, false));
        (await Review(client, seed, requestId, true)).EnsureSuccessStatusCode();
        using var finalScope = factory.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Empty(await finalDb.SettlementDecisions.Where(x => x.PortionSettlementId == source).ToListAsync());
        var root = (await finalDb.PortionSettlements.Include(x => x.Students).SingleAsync(x => x.Id == source));
        Assert.Equal("Historical child", Assert.Single(root.Students).StudentName);
        root.Count = 0; await Assert.ThrowsAsync<InvalidOperationException>(() => finalDb.SaveChangesAsync());
    }

    [Fact]
    public async Task NewEnrollmentAfterCutoffCanBeRequestedAndCandidatesArePaginated()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var source = await Base(client, seed); Guid lateId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var date = (await db.MealDays.FindAsync(seed.DayId))!.Date;
            var late = new Student { FullName = "Late child", ClassId = seed.ClassId,
                Enrollments = [new Enrollment { ClassId = seed.ClassId, StartDate = date, RecordedAt = seed.Cutoff.AddMinutes(1) }] };
            lateId = late.Id; db.Students.Add(late);
            for (var i = 0; i < 27; i++) db.Students.Add(new Student { FullName = "Extra " + i, ClassId = seed.ClassId,
                Enrollments = [new Enrollment { ClassId = seed.ClassId, StartDate = date, RecordedAt = seed.Cutoff.AddMinutes(1) }] });
            await db.SaveChangesAsync();
        }
        var state = await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId + "&candidatePage=2");
        Assert.Equal(30, state.GetProperty("candidateTotal").GetInt32()); Assert.Equal(5, state.GetProperty("candidates").GetArrayLength());
        var requestId = await EventId(await Submit(client, seed, source, lateId, true)); (await Review(client, seed, requestId, true)).EnsureSuccessStatusCode();
        using var finalScope = factory.Services.CreateScope(); var dbCheck = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.DoesNotContain(await dbCheck.SettlementStudents.Where(x => x.PortionSettlementId == source).ToListAsync(), x => x.StudentId == lateId);
        Assert.NotNull((await dbCheck.PortionAmendments.SingleAsync()).EnrollmentId);
    }

    [Fact]
    public async Task LegacyCountWithoutCompleteRosterCannotBeReplacedByAnIncorrectTotal()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); await Authorize(client, seed.AdminEmail, seed.Password); clock.Set(seed.Cutoff);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.SettledAt = clock.GetUtcNow();
            db.PortionSettlements.Add(new PortionSettlement { MealDayId = seed.DayId, ClassId = seed.ClassId, ClassName = "Legacy",
                Count = 10, SettledBy = "legacy", CutoffAt = seed.Cutoff,
                Students = [new SettlementStudent { StudentId = seed.PresentId, StudentName = "Historical child" }] });
            await db.SaveChangesAsync();
        }
        var state = await client.GetFromJsonAsync<JsonElement>(Route(seed) + "?classId=" + seed.ClassId);
        Assert.False(state.GetProperty("hasCompleteRoster").GetBoolean()); Assert.False(state.GetProperty("canRequest").GetBoolean());
        var portions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/portions");
        Assert.Equal(10, portions.GetProperty("classes")[0].GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(client, seed, await Base(client, seed), seed.AbsentId, true)).StatusCode);
        using var finalScope = factory.Services.CreateScope(); var dbCheck = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(10, (await dbCheck.PortionSettlements.SingleAsync()).Count); Assert.Empty(await dbCheck.PortionAmendments.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentApprovalsOfDifferentRequestsOnSameSourceHaveOneWinner()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: true, clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var source = await Base(client, seed);
        var add = await EventId(await Submit(client, seed, source, seed.AbsentId, true)); var remove = await EventId(await Submit(client, seed, source, seed.PresentId, false));
        var results = await Task.WhenAll(Review(client, seed, add, true), Review(client, seed, remove, true));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(2, await db.PortionSettlements.CountAsync(x => x.ClassId == seed.ClassId)); Assert.Single(await db.PortionAmendmentResolutions.ToListAsync());
    }

    [Fact]
    public async Task ChildCannotHaveTwoPortionsAcrossClassesEvenIfOtherClassChangesAfterRequest()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var source = await Base(client, seed);
        var pending = await EventId(await Submit(client, seed, source, seed.AbsentId, true));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var other = await db.PortionSettlements.SingleAsync(x => x.ClassId == seed.ForeignClassId);
            // Simulate an independently approved correction for the other class.
            db.PortionSettlements.Add(new PortionSettlement { MealDayId = seed.DayId, ClassId = seed.ForeignClassId,
                ClassName = other.ClassName, Count = 2, Version = 2, SupersedesId = other.Id, SettledBy = "test", CutoffAt = seed.Cutoff,
                Students = [new SettlementStudent { StudentId = seed.ForeignId, StudentName = "Foreign child" },
                    new SettlementStudent { StudentId = seed.AbsentId, StudentName = "Absent child" }] });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Review(client, seed, pending, true)).StatusCode);
        (await Review(client, seed, pending, false)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(client, seed, source, seed.AbsentId, true)).StatusCode);
        Assert.Equal(source, await Base(client, seed));
    }

    [PostgresFact]
    public async Task ConcurrentReviewOfSameRequestAndConcurrentSubmissionsNeverDuplicateHistory()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: true, clock: clock); using var client = factory.CreateClient();
        var seed = await Setup(factory, clock, client); var source = await Base(client, seed);
        var submitted = await Task.WhenAll(Submit(client, seed, source, seed.AbsentId, true), Submit(client, seed, source, seed.AbsentId, true));
        Assert.Single(submitted, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(submitted, x => x.StatusCode == HttpStatusCode.Conflict);
        var requestId = await EventId(submitted.Single(x => x.StatusCode == HttpStatusCode.OK));
        var reviewed = await Task.WhenAll(Review(client, seed, requestId, true), Review(client, seed, requestId, false));
        Assert.Single(reviewed, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(reviewed, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Single(await db.PortionAmendments.ToListAsync()); Assert.Single(await db.PortionAmendmentResolutions.ToListAsync());
    }
}

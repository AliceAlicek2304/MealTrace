using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class ListPaginationTests
{
    [Fact]
    public Task AbsencesBeyondOneHundredRemainSearchableAndRestrictedToGuardian() => CheckAbsences(false);

    [PostgresFact]
    public Task PostgresAbsencePagingFiltersAndGuardianScope() => CheckAbsences(true);

    private static async Task CheckAbsences(bool postgres)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: postgres, clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        var today = SchoolTime.Today(clock.GetUtcNow());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var parent = await db.Users.SingleAsync(x => x.Email == "parent@test.local");
            for (var i = 0; i < 106; i++)
                db.MealAbsences.Add(new MealAbsence { StudentId = seed.PresentId, ReportedByUserId = parent.Id,
                    FromDate = i == 1 ? today.AddDays(1) : today.AddDays(-300 - i), ToDate = i == 1 ? today.AddDays(1) : today.AddDays(-299 - i),
                    Reason = i == 105 ? "Oldest needle %_" : "Historical absence",
                    ReportedAt = clock.GetUtcNow().AddDays(-i - 1), CancelledAt = i == 0 ? clock.GetUtcNow() : null });
            // Neither another reporter nor an unlinked child may enter the returned page/total/options.
            db.MealAbsences.Add(new MealAbsence { StudentId = seed.PresentId, ReportedByUserId = seed.TeacherId, FromDate = today, ToDate = today, Reason = "Secret reporter" });
            db.MealAbsences.Add(new MealAbsence { StudentId = seed.ForeignId, ReportedByUserId = parent.Id, FromDate = today, ToDate = today, Reason = "Secret child" });
            // Historic registrations remain filterable even after an enrollment ends.
            (await db.Enrollments.SingleAsync(x => x.StudentId == seed.PresentId)).EndDate = today;
            await db.SaveChangesAsync();
        }
        await Authorize(client, "parent@test.local", seed.Password);
        var ids = new HashSet<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var result = await client.GetFromJsonAsync<JsonElement>($"/api/parent/absences/search?page={page}");
            Assert.Equal(107, result.GetProperty("total").GetInt32());
            Assert.Equal(page == 5 ? 7 : 25, result.GetProperty("items").GetArrayLength());
            foreach (var item in result.GetProperty("items").EnumerateArray()) Assert.True(ids.Add(item.GetProperty("id").GetGuid()));
            Assert.Equal(2, result.GetProperty("students").GetArrayLength());
        }
        Assert.Equal(107, ids.Count);
        var old = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences/search?search=OLDEST%20NEEDLE%20%25_&status=EXPIRED&studentId=" + seed.PresentId);
        Assert.Equal(1, old.GetProperty("total").GetInt32());
        Assert.Equal("Oldest needle %_", old.GetProperty("items")[0].GetProperty("reason").GetString());
        var cancelled = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences/search?status=CANCELLED");
        Assert.Equal(1, cancelled.GetProperty("total").GetInt32());
        var active = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences/search?status=ACTIVE");
        Assert.Equal(1, active.GetProperty("total").GetInt32());
        var upcoming = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences/search?status=UPCOMING");
        Assert.Equal(1, upcoming.GetProperty("total").GetInt32());
        var foreign = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences/search?studentId=" + seed.ForeignId);
        Assert.Equal(0, foreign.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/parent/absences/search?status=UNKNOWN")).StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences/search?page=100000");
        Assert.Empty(empty.GetProperty("items").EnumerateArray()); Assert.Equal(107, empty.GetProperty("total").GetInt32());
        await Authorize(client, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/parent/absences/search")).StatusCode);
    }

    [Fact]
    public Task MealDaysBeyondOneHundredArePagedAndUseLatestSettledTotal() => CheckMealDays(false);

    [PostgresFact]
    public Task PostgresMealDayPagingSearchAndCurrentSnapshotTotal() => CheckMealDays(true);

    private static async Task CheckMealDays(bool postgres)
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: postgres, clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); var today = SchoolTime.Today(clock.GetUtcNow()); Guid oldId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            for (var i = 0; i < 105; i++) db.MealDays.Add(new MealDay { Date = today.AddDays(-i - 1), MealType = "Lunch", CutoffAt = seed.Cutoff.AddDays(-i - 1), IsCancelled = i == 0 });
            var oldest = new MealDay { Date = today.AddDays(-200), MealType = "Old needle", CutoffAt = seed.Cutoff.AddDays(-200), SettledAt = clock.GetUtcNow() };
            oldId = oldest.Id; db.MealDays.Add(oldest);
            var original = new PortionSettlement { MealDayId = oldId, ClassId = seed.ClassId, ClassName = "Class", Count = 4, Version = 1, SettledBy = "test", CutoffAt = oldest.CutoffAt };
            db.PortionSettlements.Add(original);
            db.PortionSettlements.Add(new PortionSettlement { MealDayId = oldId, ClassId = seed.ClassId, ClassName = "Class", Count = 2, Version = 2, SupersedesId = original.Id, SettledBy = "test", CutoffAt = oldest.CutoffAt });
            await db.SaveChangesAsync();
        }
        await Authorize(client, seed.AdminEmail, seed.Password);
        var ids = new HashSet<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var result = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/search?page={page}");
            Assert.Equal(107, result.GetProperty("total").GetInt32());
            foreach (var item in result.GetProperty("items").EnumerateArray()) Assert.True(ids.Add(item.GetProperty("id").GetGuid()));
        }
        Assert.Equal(107, ids.Count);
        var old = await client.GetFromJsonAsync<JsonElement>("/api/meal-days/search?search=OLD%20NEEDLE&status=SETTLED&date=" + today.AddDays(-200));
        Assert.Equal(1, old.GetProperty("total").GetInt32()); Assert.Equal(oldId, old.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(2, old.GetProperty("items")[0].GetProperty("settledPortions").GetInt32());
        var cancelled = await client.GetFromJsonAsync<JsonElement>("/api/meal-days/search?status=CANCELLED");
        Assert.Equal(1, cancelled.GetProperty("total").GetInt32());
        var pending = await client.GetFromJsonAsync<JsonElement>("/api/meal-days/search?status=PENDING");
        Assert.Equal(105, pending.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/meal-days/search?status=UNKNOWN")).StatusCode);
        await Authorize(client, "parent@test.local", seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/meal-days/search")).StatusCode);
    }

    [Fact]
    public Task AccountSearchAndRoleFiltersApplyBeforePaging() => CheckAccounts(false);

    [PostgresFact]
    public Task PostgresAccountSearchRoleScopeAndPaging() => CheckAccounts(true);

    private static async Task CheckAccounts(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres: postgres); using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync(); Guid target;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var teacherRole = await db.Roles.SingleAsync(x => x.Name == RoleNames.Teacher);
            var parentRole = await db.Roles.SingleAsync(x => x.Name == RoleNames.Parent);
            for (var i = 0; i < 35; i++) db.Users.Add(new ApplicationUser { Id = Guid.NewGuid(), FullName = "Filler " + i, UserName = $"a{i:D2}@test.local", Email = $"a{i:D2}@test.local" });
            var account = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Needle %_", UserName = "z-needle@test.local", Email = "z-needle@test.local", PhoneNumber = "0987654321" };
            target = account.Id; db.Users.Add(account);
            db.UserRoles.AddRange(new IdentityUserRole<Guid> { UserId = target, RoleId = teacherRole.Id }, new IdentityUserRole<Guid> { UserId = target, RoleId = parentRole.Id });
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = target, ClassId = seed.ClassId }); await db.SaveChangesAsync();
        }
        await Authorize(client, seed.AdminEmail, seed.Password);
        var all = await client.GetFromJsonAsync<JsonElement>("/api/admin/users?pageSize=100"); var total = all.GetProperty("total").GetInt32();
        var first = await client.GetFromJsonAsync<JsonElement>("/api/admin/users?pageSize=25");
        Assert.DoesNotContain(first.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == target);
        var found = await client.GetFromJsonAsync<JsonElement>("/api/admin/users?search=NEEDLE%20%25_&role=teacher&classId=" + seed.ClassId);
        Assert.Equal(1, found.GetProperty("total").GetInt32()); Assert.Equal(target, found.GetProperty("items")[0].GetProperty("id").GetGuid());
        var phone = await client.GetFromJsonAsync<JsonElement>("/api/admin/users?search=0987654321&role=PARENT"); Assert.Equal(1, phone.GetProperty("total").GetInt32());
        var excluded = await client.GetFromJsonAsync<JsonElement>("/api/admin/users?search=needle&role=ADMIN"); Assert.Equal(0, excluded.GetProperty("total").GetInt32());
        var ids = new HashSet<Guid>();
        for (var page = 1; page <= (total + 9) / 10; page++)
        {
            var result = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users?page={page}&pageSize=10");
            Assert.Equal(total, result.GetProperty("total").GetInt32());
            foreach (var item in result.GetProperty("items").EnumerateArray()) Assert.True(ids.Add(item.GetProperty("id").GetGuid()));
        }
        Assert.Equal(total, ids.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/users?role=UNKNOWN")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/users?search=" + new string('x', 201))).StatusCode);
        await Authorize(client, seed.TeacherEmail, seed.Password); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users?search=needle")).StatusCode);
    }
}

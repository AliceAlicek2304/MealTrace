using System.Net;
using System.Net.Http.Headers;
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

public sealed class PhaseOneHttpScopeTests
{
    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Teacher)]
    [InlineData(RoleNames.KitchenStaff)]
    [InlineData(RoleNames.Parent)]
    [InlineData(null)]
    public async Task ClassesPortionsDecisionsAndExceptionWritesEnforceHttpRolesAndScope(string? role)
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);
        using var client = factory.CreateClient();
        if (role is not null)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var teacher = (await users.FindByIdAsync(seed.TeacherId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(teacher, RoleNames.Teacher)).Succeeded);
            Assert.True((await users.AddToRoleAsync(teacher, role)).Succeeded);
            await Authorize(client, seed.TeacherEmail, seed.Password);
        }
        var denied = role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        var canEdit = role is RoleNames.Admin or RoleNames.Teacher;
        foreach (var classId in new[] { seed.ClassId, seed.ForeignClassId })
        {
            var response = await client.GetAsync($"/api/classes/{classId}/students");
            var allowed = role == RoleNames.Admin || (role == RoleNames.Teacher && classId == seed.ClassId);
            Assert.Equal(allowed ? HttpStatusCode.OK : denied, response.StatusCode);
            if (role == RoleNames.Teacher && allowed) Assert.DoesNotContain(seed.ForeignId.ToString(), await response.Content.ReadAsStringAsync());
        }
        var portions = await client.GetAsync($"/api/meal-days/{seed.DayId}/portions");
        Assert.Equal(role is RoleNames.Admin or RoleNames.Teacher or RoleNames.KitchenStaff ? HttpStatusCode.OK : denied, portions.StatusCode);
        var decisions = await client.GetAsync($"/api/meal-days/{seed.DayId}/decisions");
        Assert.Equal(canEdit ? HttpStatusCode.OK : denied, decisions.StatusCode);
        if (role == RoleNames.Teacher) Assert.DoesNotContain(seed.ForeignId.ToString(), await decisions.Content.ReadAsStringAsync());
        Assert.Equal(canEdit ? HttpStatusCode.OK : denied, (await Record(client, seed.DayId, seed.PresentId, "EAT")).StatusCode);
        Assert.Equal(role == RoleNames.Admin ? HttpStatusCode.OK : denied, (await Record(client, seed.DayId, seed.ForeignId, "EAT")).StatusCode);
        if (canEdit)
        {
            // Repeating without expectedEventId is an application stale conflict.
            Assert.Equal(HttpStatusCode.Conflict, (await Record(client, seed.DayId, seed.PresentId, "EAT")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/meal-days/{seed.DayId}/students/{seed.PresentId}/exceptions")).StatusCode);
        }
    }

    [Fact]
    public async Task TeacherPermissionFollowsEnrollmentAtMealDateAcrossScheduledTransfer()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);
        Guid futureDay;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var today = SchoolTime.Today(clock.GetUtcNow());
            var enrollment = await db.Enrollments.SingleAsync(x => x.StudentId == seed.PresentId);
            enrollment.EndDate = today.AddDays(1); enrollment.EndRecordedAt = clock.GetUtcNow();
            var child = (await db.Students.FindAsync(seed.PresentId))!;
            child.ClassId = seed.ForeignClassId; // Legacy cache already points to the scheduled next class.
            await db.SaveChangesAsync();
            db.Enrollments.Add(new Enrollment { StudentId = child.Id, ClassId = seed.ForeignClassId,
                StartDate = today.AddDays(1), RecordedAt = clock.GetUtcNow() });
            var day = new MealDay { Date = today.AddDays(1), MealType = "Lunch", SchoolYear = "2026-2027", CutoffAt = clock.GetUtcNow().AddDays(1) };
            db.MealDays.Add(day); futureDay = day.Id;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        await Authorize(client, seed.TeacherEmail, seed.Password);
        var roster = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{seed.ClassId}/students");
        Assert.Contains(roster.EnumerateArray(), x => x.GetProperty("id").GetGuid() == seed.PresentId);
        Assert.Equal(HttpStatusCode.OK, (await Record(client, seed.DayId, seed.PresentId, "EAT")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Record(client, futureDay, seed.PresentId, "EAT")).StatusCode);
        var next = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{futureDay}/decisions");
        Assert.DoesNotContain(next.GetProperty("items").EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == seed.PresentId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            // Move this teacher's assignment, not the historical meal membership.
            db.TeacherAssignments.RemoveRange(await db.TeacherAssignments.Where(x => x.UserId == seed.TeacherId).ToListAsync());
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seed.TeacherId, ClassId = seed.ForeignClassId });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await Record(client, futureDay, seed.PresentId, "EAT")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Record(client, seed.DayId, seed.PresentId, "EAT")).StatusCode);
    }

    [Fact]
    public async Task RevokedParentLosesOldSessionAndRecordAccessAfterRelogin()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);
        using var parent = factory.CreateClient();
        await Authorize(parent, "parent@test.local", seed.Password);
        var today = SchoolTime.Today(clock.GetUtcNow());
        var children = await parent.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Contains(children.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == seed.PresentId);
        Assert.DoesNotContain(children.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == seed.ForeignId);
        var report = new { studentId = seed.PresentId, fromDate = today, toDate = today, reason = "Nghỉ" };
        var created = await parent.PostAsJsonAsync("/api/parent/absences", report);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var absenceId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsJsonAsync("/api/parent/absences",
            new { studentId = seed.ForeignId, fromDate = today, toDate = today, reason = "Không thuộc quyền" })).StatusCode);
        Guid requestId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync("parent@test.local"))!;
            var request = new ParentLinkRequest { StudentId = seed.PresentId, ParentId = user.Id, Status = "APPROVED",
                RequestedAt = clock.GetUtcNow().AddMinutes(-2), ReviewedAt = clock.GetUtcNow().AddMinutes(-1),
                ReviewedByUserId = seed.TeacherId, ReviewReason = "Đã đối chiếu" };
            db.ParentLinkRequests.Add(request); requestId = request.Id;
            await db.SaveChangesAsync();
        }
        using var staff = factory.CreateClient();
        await Authorize(staff, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync($"/api/student-link-requests/{requestId}/revoke", new { revision = 0, reason = "Liên kết sai" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/parent/students")).StatusCode);
        await Authorize(parent, "parent@test.local", seed.Password);
        children = await parent.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.DoesNotContain(children.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == seed.PresentId);
        Assert.Contains(children.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == seed.AbsentId);
        Assert.DoesNotContain(absenceId.ToString(), await parent.GetStringAsync("/api/parent/absences"));
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsync($"/api/parent/absences/{absenceId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsJsonAsync($"/api/parent/absences/{absenceId}/replace", report)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsJsonAsync("/api/parent/absences", report)).StatusCode);
    }
}

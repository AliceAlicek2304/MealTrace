using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class ParentLinkTests
{
    [Fact] public Task ApprovalRequiresSchoolReviewBeforeChildAccess() => Flow(false);
    [PostgresFact] public Task PostgreSqlApprovalRequiresSchoolReviewBeforeChildAccess() => Flow(true);

    [Theory]
    [InlineData("cancel")]
    [InlineData("reject")]
    [InlineData("inactive-child")]
    [InlineData("inactive-parent")]
    [InlineData("self-review")]
    [InlineData("manual-link")]
    [InlineData("other-parent")]
    public async Task DecisionsPreserveOwnershipAndEligibility(string scenario)
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        Guid parentId;
        var student = new Student { FullName = "Trần Bình", ClassId = seed.ClassId };
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var parent = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Phụ huynh Bình", UserName = "parent@test.local", Email = "parent@test.local" };
            Assert.True((await users.CreateAsync(parent, seed.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
            if (scenario == "self-review") Assert.True((await users.AddToRoleAsync(parent, RoleNames.Admin)).Succeeded);
            parentId = parent.Id;
            if (scenario == "other-parent")
            {
                var other = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Phụ huynh khác", UserName = "other@test.local", Email = "other@test.local" };
                Assert.True((await users.CreateAsync(other, seed.Password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(other, RoleNames.Parent)).Succeeded);
            }
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.Students.Add(student);
            await db.SaveChangesAsync();
        }
        using var parentClient = factory.CreateClient();
        parentClient.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(parentClient, "parent@test.local", seed.Password));
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(admin, seed.AdminEmail, seed.Password));
        var input = new { studentCode = student.StudentCode, studentName = student.FullName, relationship = "GUARDIAN", note = "" };
        Assert.Equal(HttpStatusCode.NoContent, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", input)).StatusCode);
        var list = await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/link-requests");
        var id = Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        if (scenario == "other-parent")
        {
            using var other = factory.CreateClient();
            other.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(other, "other@test.local", seed.Password));
            Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/parent/link-requests")).GetProperty("items").EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/parent/link-requests/{id}/cancel", new { revision = 0 })).StatusCode);
        }
        else if (scenario == "cancel")
        {
            Assert.Equal(HttpStatusCode.Conflict, (await parentClient.PostAsJsonAsync($"/api/parent/link-requests/{id}/cancel", new { revision = 4 })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await parentClient.PostAsJsonAsync($"/api/parent/link-requests/{id}/cancel", new { revision = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = true, reason = "Đối chiếu", revision = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", input)).StatusCode);
        }
        else
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
                if (scenario == "inactive-child") (await db.Students.FindAsync(student.Id))!.IsActive = false;
                if (scenario == "inactive-parent") (await db.Users.FindAsync(parentId))!.IsActive = false;
                if (scenario == "manual-link") db.ParentStudents.Add(new ParentStudent { UserId = parentId, StudentId = student.Id });
                await db.SaveChangesAsync();
            }
            var reviewer = scenario == "self-review" ? parentClient : admin;
            var expected = scenario == "self-review" ? HttpStatusCode.Forbidden : scenario.StartsWith("inactive-") ? HttpStatusCode.Conflict : HttpStatusCode.NoContent;
            Assert.Equal(expected, (await reviewer.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = scenario != "reject", reason = "Đã đối chiếu hồ sơ", revision = 0 })).StatusCode);
            if (scenario == "reject")
            {
                var history = await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/link-requests?status=REJECTED");
                Assert.Equal("Đã đối chiếu hồ sơ", Assert.Single(history.GetProperty("items").EnumerateArray()).GetProperty("reviewReason").GetString());
            }
        }
        using var final = factory.Services.CreateScope();
        Assert.Equal(scenario == "manual-link" ? 1 : 0, await final.ServiceProvider.GetRequiredService<MealTraceDbContext>().ParentStudents.CountAsync());
    }

    [PostgresFact]
    public async Task ConcurrentSchoolDecisionsProduceOneDecisionAndAtMostOneLink()
    {
        using var factory = new AuthTestFactory(postgres: true);
        var seed = await factory.SeedUsersAsync();
        var request = new ParentLinkRequest { StudentCode = "HS-CONCURRENT", StudentName = "Nguyễn An", Relationship = "MOTHER", RequestedAt = DateTimeOffset.UtcNow };
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var parent = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Mẹ An", UserName = "parent@test.local" };
            Assert.True((await users.CreateAsync(parent, seed.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { StudentCode = request.StudentCode, FullName = request.StudentName, ClassId = seed.ClassId };
            db.Students.Add(child); request.StudentId = child.Id; request.ParentId = parent.Id;
            db.ParentLinkRequests.Add(request); await db.SaveChangesAsync();
        }
        using var first = factory.CreateClient(); using var second = factory.CreateClient();
        var token = await LoginAsync(first, seed.AdminEmail, seed.Password);
        first.DefaultRequestHeaders.Authorization = new("Bearer", token); second.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var responses = await Task.WhenAll(first.PostAsJsonAsync($"/api/student-link-requests/{request.Id}/review", new { approve = true, reason = "Đã đối chiếu", revision = 0 }), second.PostAsJsonAsync($"/api/student-link-requests/{request.Id}/review", new { approve = false, reason = "Chưa khớp hồ sơ", revision = 0 }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var final = factory.Services.CreateScope();
        var finalDb = final.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var decision = await finalDb.ParentLinkRequests.SingleAsync();
        Assert.Equal(1, decision.Revision);
        Assert.Equal(decision.Status == "APPROVED" ? 1 : 0, await finalDb.ParentStudents.CountAsync());
        foreach (var response in responses) response.Dispose();
    }

    [Theory]
    [InlineData(false, "year-only")]
    [InlineData(false, "wrong-year")]
    [InlineData(false, "approve")]
    [InlineData(false, "reject")]
    [InlineData(false, "stale")]
    [InlineData(false, "wrong-class")]
    [InlineData(false, "unassigned")]
    [InlineData(false, "duplicate")]
    public Task BulkDecisionsAreScopedAndAtomic(bool postgres, string scenario) => BulkFlow(postgres, scenario);

    [PostgresFact] public Task PostgreSqlBulkApprovalAndClassChoice() => BulkFlow(true, "approve");
    [PostgresFact] public Task PostgreSqlBulkStaleRevisionRollsBackAllItems() => BulkFlow(true, "stale");

    private static async Task BulkFlow(bool postgres, string scenario)
    {
        using var factory = new AuthTestFactory(postgres: postgres);
        var seed = await factory.SeedUsersAsync();
        var first = new Student { FullName = "Nguyễn An", ClassId = seed.ClassId };
        var second = new Student { FullName = "Trần Bình", ClassId = seed.ClassId };
        var otherClass = new SchoolClass { Name = "A3", SchoolYear = "2025-2026" };
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var parent = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Phụ huynh", UserName = "parent@test.local", Email = "parent@test.local" };
            Assert.True((await users.CreateAsync(parent, seed.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.Classes.Add(otherClass); db.Students.AddRange(first, second);
            if (scenario != "unassigned") db.TeacherAssignments.Add(new TeacherAssignment { UserId = seed.TeacherId, ClassId = seed.ClassId });
            await db.SaveChangesAsync();
        }
        using var parentClient = factory.CreateClient();
        parentClient.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(parentClient, "parent@test.local", seed.Password));
        var choices = await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/link-requests/classes");
        Assert.Equal(2, choices.GetArrayLength());
        Assert.All(choices.EnumerateArray(), x => Assert.Equal(new[] { "id", "name", "schoolYear" }, x.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(HttpStatusCode.BadRequest, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", new { classId = otherClass.Id, studentName = first.FullName, relationship = "MOTHER" })).StatusCode);
        foreach (var child in new[] { first, second })
            Assert.Equal(HttpStatusCode.NoContent, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", new { classId = seed.ClassId, studentName = child.FullName.ToLowerInvariant(), relationship = "MOTHER" })).StatusCode);
        var own = await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/link-requests");
        var rows = own.GetProperty("items").EnumerateArray().ToArray();
        var items = rows.Select(x => new { id = x.GetProperty("id").GetGuid(), revision = 0 }).ToArray();
        using var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(teacher, seed.TeacherEmail, seed.Password));
        var teacherClasses = await teacher.GetFromJsonAsync<JsonElement>("/api/student-link-requests/classes");
        Assert.Equal(scenario == "unassigned" ? 0 : 1, teacherClasses.GetArrayLength());
        var yearFiltered = await teacher.GetFromJsonAsync<JsonElement>("/api/student-link-requests?schoolYear=2025-2026");
        Assert.Equal(0, yearFiltered.GetProperty("total").GetInt32());
        if (scenario == "stale") items[1] = new { items[1].id, revision = 99 };
        if (scenario == "duplicate") items[1] = items[0];
        var response = await teacher.PostAsJsonAsync("/api/student-link-requests/bulk-review", new { classId = scenario is "year-only" or "wrong-year" ? (Guid?)null : scenario == "wrong-class" ? otherClass.Id : seed.ClassId, schoolYear = scenario == "year-only" ? "2026-2027" : scenario == "wrong-year" ? "2025-2026" : null, items, approve = scenario != "reject", reason = "Đã rà danh sách lớp và hồ sơ phụ huynh" });
        var expected = scenario is "approve" or "year-only" or "reject" ? HttpStatusCode.NoContent : scenario == "unassigned" ? HttpStatusCode.NotFound : scenario == "duplicate" ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict;
        Assert.Equal(expected, response.StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(scenario is "approve" or "year-only" ? 2 : 0, await finalDb.ParentStudents.CountAsync());
        var statuses = await finalDb.ParentLinkRequests.Select(x => x.Status).ToListAsync();
        Assert.All(statuses, value => Assert.Equal(scenario is "approve" or "year-only" ? "APPROVED" : scenario == "reject" ? "REJECTED" : "PENDING", value));
    }

    private static async Task Flow(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres: postgres);
        var seed = await factory.SeedUsersAsync();
        Guid parentId;
        var student = new Student { FullName = "Nguyễn An", ClassId = seed.ClassId };
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var parent = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Mẹ An", UserName = "parent@test.local", Email = "parent@test.local" };
            Assert.True((await users.CreateAsync(parent, seed.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
            parentId = parent.Id;
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.Students.Add(student);
            db.Enrollments.Add(new Enrollment { StudentId = student.Id, ClassId = seed.ClassId, StartDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1) });
            await db.SaveChangesAsync();
        }
        using var parentClient = factory.CreateClient();
        parentClient.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(parentClient, "parent@test.local", seed.Password));
        using var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(teacher, seed.TeacherEmail, seed.Password));
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(admin, seed.AdminEmail, seed.Password));
        var input = new { studentCode = student.StudentCode, studentName = "  nguyễn   an ", relationship = "MOTHER", note = "Liên hệ đối chiếu" };
        Assert.Equal(HttpStatusCode.BadRequest, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", new { studentCode = student.StudentCode, studentName = "Sai tên", relationship = "MOTHER" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", new { input.studentCode, input.studentName, input.relationship, parentId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", input)).StatusCode);
        Assert.Empty((await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/students")).EnumerateArray());
        var own = await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/link-requests");
        var item = Assert.Single(own.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("className").ValueKind);
        var id = item.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await parentClient.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = true, reason = "Đối chiếu", revision = 0 })).StatusCode);
        Assert.Empty((await teacher.GetFromJsonAsync<JsonElement>("/api/student-link-requests")).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = true, reason = "Đối chiếu", revision = 0 })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seed.TeacherId, ClassId = seed.ClassId });
            await db.SaveChangesAsync();
        }
        var scoped = await teacher.GetFromJsonAsync<JsonElement>("/api/student-link-requests?status=PENDING");
        Assert.Equal("Lớp test", Assert.Single(scoped.GetProperty("items").EnumerateArray()).GetProperty("className").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = true, reason = " ", revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = true, reason = "Đã đối chiếu với hồ sơ nhập học", revision = 0 })).StatusCode);
        Assert.Single((await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/students")).EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/student-link-requests/{id}/review", new { approve = false, reason = "Quyết định cũ", revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await parentClient.PostAsJsonAsync($"/api/parent/link-requests/{id}/cancel", new { revision = 0 })).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(1, await finalDb.ParentStudents.CountAsync(x => x.UserId == parentId && x.StudentId == student.Id));
        Assert.Equal("APPROVED", (await finalDb.ParentLinkRequests.SingleAsync()).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/student-link-requests/{id}/revoke", new { revision = 1, reason = "Liên kết nhầm, đã đối chiếu lại" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parentClient.GetAsync("/api/parent/students")).StatusCode);
        parentClient.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(parentClient, "parent@test.local", seed.Password));
        Assert.Empty((await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/students")).EnumerateArray());
        var revoked = await parentClient.GetFromJsonAsync<JsonElement>("/api/parent/link-requests?status=REVOKED");
        var revokedItem = Assert.Single(revoked.GetProperty("items").EnumerateArray());
        Assert.Equal("Đã đối chiếu với hồ sơ nhập học", revokedItem.GetProperty("reviewReason").GetString());
        Assert.Equal("Liên kết nhầm, đã đối chiếu lại", revokedItem.GetProperty("revocationReason").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await parentClient.PostAsJsonAsync("/api/parent/link-requests", new { classId = seed.ClassId, studentName = student.FullName, relationship = "MOTHER" })).StatusCode);

    }
}

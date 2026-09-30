using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Data;
using MealTrace.Api.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class EnrollmentTests
{
    [Fact]
    public async Task StudentCodeIsUniqueAndSurvivesProfileEditWhileStaleEditsAreRejected()
    {
        using var factory = new AuthTestFactory(); using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var create = await client.PostAsJsonAsync("/api/students", new { fullName = "Child", classId = seed.ClassId, studentCode = "hs-2026-001" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var child = await create.Content.ReadFromJsonAsync<JsonElement>(); var id = child.GetProperty("id").GetGuid();
        Assert.Equal("HS-2026-001", child.GetProperty("studentCode").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/students", new { fullName = "Other", classId = seed.ClassId, studentCode = "HS-2026-001" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "", revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Renamed", revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Stale", revision = 0 })).StatusCode);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/students?search=HS-2026-001");
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("Renamed", row.GetProperty("fullName").GetString()); Assert.Equal("HS-2026-001", row.GetProperty("studentCode").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.TeacherEmail, seed.Password));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/students/{id}/enrollments")).StatusCode);
    }

    [Fact]
    public async Task FutureTransferWithdrawalAndReEnrollmentRespectDatesAndKeepGuardians()
    {
        using var factory = new AuthTestFactory(); using var client = factory.CreateClient(); var seed = await factory.SeedUsersAsync();
        var today = StudentAdministrationEndpoints.Today; Guid childId; Guid nextClass; Guid todayMeal; Guid futureMeal;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var room = new SchoolClass { Name = "Next class", SchoolYear = "2027-2028" }; db.Classes.Add(room); nextClass = room.Id;
            var child = new Student { FullName = "History child", ClassId = seed.ClassId,
                Enrollments = [new Enrollment { ClassId = seed.ClassId, StartDate = today.AddDays(-2), RecordedAt = DateTimeOffset.UtcNow.AddDays(-3) }] };
            db.Students.Add(child); childId = child.Id;
            var old = new MealDay { Date = today, SchoolYear = "2026-2027", MealType = "Lunch", CutoffAt = DateTimeOffset.UtcNow.AddHours(1) };
            var future = new MealDay { Date = today.AddDays(1), SchoolYear = "2027-2028", MealType = "Lunch", CutoffAt = DateTimeOffset.UtcNow.AddDays(1) };
            db.MealDays.AddRange(old, future); todayMeal = old.Id; futureMeal = future.Id; await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var parent = await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0901234567", fullName = "Guardian" });
        Assert.Equal(HttpStatusCode.OK, parent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments", new { classId = nextClass, effectiveDate = today.AddDays(-1), reason = "Past", revision = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments", new { classId = nextClass, effectiveDate = today.AddDays(1), reason = "Transfer", revision = 0 })).StatusCode);
        var current = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{todayMeal}/portions");
        Assert.Equal(seed.ClassId, Assert.Single(current.GetProperty("classes").EnumerateArray()).GetProperty("classId").GetGuid());
        var futureResult = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{futureMeal}/portions");
        Assert.Equal(nextClass, Assert.Single(futureResult.GetProperty("classes").EnumerateArray()).GetProperty("classId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments", new { classId = (Guid?)null, effectiveDate = today.AddDays(2), reason = "Withdrawal", revision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments", new { classId = nextClass, effectiveDate = today.AddDays(3), reason = "Return", revision = 2 })).StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{childId}/enrollments");
        Assert.Equal(3, history.GetArrayLength());
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Single(await database.ParentStudents.Where(x => x.StudentId == childId).ToListAsync());
        Assert.False(await StudentAdministrationEndpoints.OnDate(database, today.AddDays(2)).AnyAsync(x => x.StudentId == childId));
        Assert.True(await StudentAdministrationEndpoints.OnDate(database, today.AddDays(3)).AnyAsync(x => x.StudentId == childId));
    }

    [Fact]
    public async Task ScopeSearchAndPaginationReachBeyondTwoHundredAndReturnSelectedItemsOutsidePage()
    {
        using var factory = new AuthTestFactory(); using var client = factory.CreateClient(); var seed = await factory.SeedUsersAsync(); Guid lastId = default;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            for (var i = 0; i < 225; i++) { var child = new Student { FullName = $"Child {i:D3}", ClassId = seed.ClassId }; db.Students.Add(child); lastId = child.Id; }
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var scopes = await client.GetFromJsonAsync<JsonElement>($"/api/admin/scope-options?studentPage=9&selectedStudentIds={lastId}");
        Assert.Equal(225, scopes.GetProperty("studentTotal").GetInt32()); Assert.Equal(25, scopes.GetProperty("students").GetArrayLength());
        Assert.Equal(lastId, Assert.Single(scopes.GetProperty("selectedStudents").EnumerateArray()).GetProperty("id").GetGuid());
        var result = await client.GetFromJsonAsync<JsonElement>("/api/admin/students?search=Child%20224");
        Assert.Equal(lastId, Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
    }
}

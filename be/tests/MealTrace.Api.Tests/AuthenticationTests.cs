using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MealTrace.Api.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task MealEndpointsRejectAnonymousRequests()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/meal-days");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeacherCannotManageAccountsAndSuspensionRevokesExistingToken()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();

        var teacherToken = await LoginAsync(client, seeded.TeacherEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var update = await client.PutAsJsonAsync($"/api/admin/users/{seeded.TeacherId}", new
        {
            fullName = "Giáo viên thử nghiệm", email = seeded.TeacherEmail,
            roles = new[] { RoleNames.Teacher }, status = "SUSPENDED",
            classIds = new[] { seeded.ClassId }, studentIds = Array.Empty<Guid>(),
            inspectorAccessUntil = (DateOnly?)null,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateMultiRoleAccountThatCanLogin()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var create = await client.PostAsJsonAsync("/api/admin/users", new
        {
            fullName = "Nhân viên kiêm nhiệm", email = "multi@test.local",
            roles = new[] { RoleNames.KitchenStaff, RoleNames.Teacher }, status = "ACTIVE",
            classIds = new[] { seeded.ClassId }, studentIds = Array.Empty<Guid>(),
            inspectorAccessUntil = (DateOnly?)null,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var temporaryPassword = created.RootElement.GetProperty("temporaryPassword").GetString()!;

        var token = await LoginAsync(client, "multi@test.local", temporaryPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var roles = me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(RoleNames.KitchenStaff, roles);
        Assert.Contains(RoleNames.Teacher, roles);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);

        var newPassword = "Changed!9" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var change = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = temporaryPassword, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var nextToken = await LoginAsync(client, "multi@test.local", newPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nextToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ParentAbsenceReducesSettledPortionsAndTeacherSeesOnlyAssignedClass()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        Guid absentId;
        Guid otherId;
        Guid dayId;
        const string parentEmail = "parent@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var present = new Student { FullName = "Trẻ có ăn", ClassId = seeded.ClassId };
            var absent = new Student { FullName = "Trẻ báo vắng", ClassId = seeded.ClassId };
            var otherClass = new SchoolClass { Name = "Lớp khác", SchoolYear = "2026-2027" };
            var other = new Student { FullName = "Trẻ lớp khác", Class = otherClass };
            db.Students.AddRange(present, absent, other);
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seeded.TeacherId, ClassId = seeded.ClassId });
            var day = new MealDay { Date = today, MealType = "Bữa trưa", SchoolYear = "2026-2027", CutoffAt = DateTimeOffset.UtcNow.AddMinutes(10) };
            db.MealDays.Add(day);
            await db.SaveChangesAsync();
            var parent = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Phụ huynh thử nghiệm", UserName = parentEmail, Email = parentEmail, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(parent, seeded.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
            db.ParentStudents.Add(new ParentStudent { UserId = parent.Id, StudentId = absent.Id });
            await db.SaveChangesAsync();
            absentId = absent.Id;
            otherId = other.Id;
            dayId = day.Id;
        }

        var parentToken = await LoginAsync(client, parentEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);
        var forbidden = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = otherId, fromDate = today, toDate = today, reason = "Nghỉ" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var absence = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = absentId, fromDate = today, toDate = today, reason = "Nghỉ" });
        Assert.Equal(HttpStatusCode.Created, absence.StatusCode);

        var teacherToken = await LoginAsync(client, seeded.TeacherEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        var teacherPreview = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{dayId}/portions");
        var teacherClasses = teacherPreview.GetProperty("classes").EnumerateArray().ToArray();
        Assert.Single(teacherClasses);
        Assert.Equal(seeded.ClassId, teacherClasses[0].GetProperty("classId").GetGuid());
        Assert.Single(teacherClasses[0].GetProperty("studentIds").EnumerateArray());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(dayId);
            day!.CutoffAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var settle = await client.PostAsync($"/api/meal-days/{dayId}/settle", null);
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var snapshots = await db.PortionSettlements.Include(x => x.Students).Where(x => x.MealDayId == dayId).ToListAsync();
            Assert.Equal(2, snapshots.Count);
            var classroom = Assert.Single(snapshots, x => x.ClassId == seeded.ClassId);
            Assert.Equal(1, classroom.Count);
            Assert.DoesNotContain(classroom.Students, x => x.StudentId == absentId);
            db.Students.Add(new Student { FullName = "Trẻ mới sau chốt", ClassId = seeded.ClassId });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);
        using (var absenceJson = JsonDocument.Parse(await absence.Content.ReadAsStringAsync()))
        {
            var absenceId = absenceJson.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/parent/absences/{absenceId}/cancel", null)).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var settledPreview = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{dayId}/portions");
        var settledClass = settledPreview.GetProperty("classes").EnumerateArray().Single(x => x.GetProperty("classId").GetGuid() == seeded.ClassId);
        Assert.True(settledClass.GetProperty("isSettled").GetBoolean());
        Assert.Single(settledClass.GetProperty("studentIds").EnumerateArray());
        var mealDays = await client.GetFromJsonAsync<JsonElement>("/api/meal-days");
        var mealDay = mealDays.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == dayId);
        Assert.Equal(2, mealDay.GetProperty("settledPortions").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/meal-days/{dayId}/settle", null)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminLinksNewAndExistingParentToStudentsAndFiltersTeachersByClass(bool usePhone)
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        Guid firstId;
        Guid secondId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var first = new Student { FullName = "Trẻ A", ClassId = seeded.ClassId };
            var second = new Student { FullName = "Trẻ B", ClassId = seeded.ClassId };
            db.Students.AddRange(first, second);
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seeded.TeacherId, ClassId = seeded.ClassId });
            await db.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }

        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var identity = new { email = usePhone ? null : "parent-new@test.local", phoneNumber = usePhone ? "+84 901 234 567" : null, fullName = "Phụ huynh A" };
        var invalid = await client.PostAsJsonAsync($"/api/admin/students/{firstId}/parents", new { phoneNumber = "123", fullName = "Invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var create = await client.PostAsJsonAsync($"/api/admin/students/{firstId}/parents", identity);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(created.GetProperty("created").GetBoolean());
        var password = created.GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/admin/students/{firstId}/parents", identity)).StatusCode);

        var linkSecond = await client.PostAsJsonAsync($"/api/admin/students/{secondId}/parents", new { identity.email, phoneNumber = usePhone ? "0901234567" : null, fullName = "" });
        Assert.Equal(HttpStatusCode.OK, linkSecond.StatusCode);
        var linked = await linkSecond.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(linked.GetProperty("created").GetBoolean());
        Assert.Equal(JsonValueKind.Null, linked.GetProperty("temporaryPassword").ValueKind);

        // Admin can add another guardian for a child who already has a linked parent.
        var extraParent = await client.PostAsJsonAsync($"/api/admin/students/{firstId}/parents", new { phoneNumber = "0901111222", fullName = "Second guardian" });
        Assert.Equal(HttpStatusCode.OK, extraParent.StatusCode);
        var classChildren = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{seeded.ClassId}/students");
        Assert.Equal(2, classChildren.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == firstId).GetProperty("parents").GetArrayLength());

        var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users?classId={seeded.ClassId}");
        var accounts = filtered.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(accounts);
        Assert.Equal(seeded.TeacherId, accounts[0].GetProperty("id").GetGuid());

        if (usePhone)
        {
            Assert.Equal("0901234567", created.GetProperty("phoneNumber").GetString());
            Assert.Equal(JsonValueKind.Null, created.GetProperty("email").ValueKind);
            var byInternational = await client.PostAsJsonAsync("/api/auth/login", new { identifier = "+84901234567", password });
            Assert.Equal(HttpStatusCode.OK, byInternational.StatusCode);
        }
        var parentToken = await LoginAsync(client, usePhone ? "0901234567" : "parent-new@test.local", password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);
        var children = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Equal(2, children.EnumerateArray().Count());
    }

    [Fact]
    public async Task AdminCreatesPhoneOnlyAccountAndPhoneChangeRevokesOldLogin()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var input = new { fullName = "Phone account", email = "", phoneNumber = "0907654321", roles = new[] { RoleNames.Teacher }, status = "ACTIVE", classIds = new[] { seeded.ClassId }, studentIds = Array.Empty<Guid>(), inspectorAccessUntil = (string?)null };
        var response = await client.PostAsJsonAsync("/api/admin/users", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = json.GetProperty("user").GetProperty("id").GetGuid();
        var password = json.GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/users", input)).StatusCode);
        var token = await LoginAsync(client, "+84907654321", password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var updated = await client.PutAsJsonAsync($"/api/admin/users/{id}", new { input.fullName, input.email, phoneNumber = "+84 908 765 432", input.roles, input.status, input.classIds, input.studentIds, input.inspectorAccessUntil });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0907654321", password })).StatusCode);
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0908765432", password });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var current = await newLogin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("0908765432", current.GetProperty("user").GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task TeacherCannotRecordExceptionForStudentFromDifferentSchoolYear()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        Guid studentId, dayId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var oldClass = new SchoolClass { Name = "Old class", SchoolYear = "2025-2026" };
            var student = new Student { FullName = "Old student", Class = oldClass };
            var day = new MealDay { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), MealType = "Lunch", SchoolYear = "2026-2027", CutoffAt = DateTimeOffset.UtcNow.AddDays(1) };
            db.Students.Add(student);
            db.MealDays.Add(day);
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seeded.TeacherId, Class = oldClass });
            await db.SaveChangesAsync();
            studentId = student.Id; dayId = day.Id;
        }
        var token = await LoginAsync(client, seeded.TeacherEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/meal-days/{dayId}/exceptions", new { studentId, willEat = false, reason = "Wrong year" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemovedGuardianCannotReadOrCancelChildAbsences()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        Guid childId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { FullName = "Child", ClassId = seeded.ClassId };
            db.Students.Add(child); await db.SaveChangesAsync(); childId = child.Id;
        }
        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var created = await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0903333444", fullName = "Guardian" });
        created.EnsureSuccessStatusCode();
        var parent = await created.Content.ReadFromJsonAsync<JsonElement>();
        var parentId = parent.GetProperty("parentId").GetGuid();
        var token = await LoginAsync(client, "0903333444", parent.GetProperty("temporaryPassword").GetString()!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var absence = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = childId, fromDate = today, toDate = today, reason = "Absent" });
        absence.EnsureSuccessStatusCode();
        var absenceId = (await absence.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            await db.ParentStudents.Where(x => x.UserId == parentId && x.StudentId == childId).ExecuteDeleteAsync();
        }
        var history = await client.GetFromJsonAsync<JsonElement>("/api/parent/absences");
        Assert.Empty(history.EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/parent/absences/{absenceId}/cancel", null)).StatusCode);
    }

    [Theory]
    [InlineData("NUTRITIONIST")]
    [InlineData("ACCOUNTANT")]
    public async Task AdminCannotAssignRetiredRoles(string retiredRole)
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        var token = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/admin/users", new {
            fullName = "Retired role", email = "retired@test.local", roles = new[] { retiredRole }, status = "ACTIVE",
            classIds = Array.Empty<Guid>(), studentIds = Array.Empty<Guid>(), inspectorAccessUntil = (DateOnly?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdminResetPasswordRevokesOldCredentialsPreservesParentLinksAndAuditsReason(bool isActive)
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        Guid childId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { FullName = "Reset child", ClassId = seeded.ClassId };
            db.Students.Add(child); await db.SaveChangesAsync(); childId = child.Id;
        }
        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var parentResponse = await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0905555666", fullName = "Reset parent" });
        parentResponse.EnsureSuccessStatusCode();
        var parent = await parentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var parentId = parent.GetProperty("parentId").GetGuid();
        var oldPassword = parent.GetProperty("temporaryPassword").GetString()!;
        var parentToken = await LoginAsync(client, "0905555666", oldPassword);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await manager.FindByIdAsync(parentId.ToString()))!;
            user.IsActive = isActive;
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
            Assert.True((await manager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15))).Succeeded);
            Assert.True((await manager.AccessFailedAsync(user)).Succeeded);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var invalid = await client.PostAsJsonAsync($"/api/admin/users/{parentId}/reset-password", new { reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var reset = await client.PostAsJsonAsync($"/api/admin/users/{parentId}/reset-password", new { reason = "Verified school enrollment record" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains("no-store", reset.Headers.CacheControl!.ToString());
        var result = await reset.Content.ReadFromJsonAsync<JsonElement>();
        var password = result.GetProperty("temporaryPassword").GetString()!;
        Assert.NotEqual(oldPassword, password);
        Assert.Equal(isActive, result.GetProperty("isActive").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0905555666", password = oldPassword })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0905555666", password });
        Assert.Equal(isActive ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, login.StatusCode);
        using var checkScope = factory.Services.CreateScope();
        var dbCheck = checkScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var audit = Assert.Single(await dbCheck.AccountPasswordResetAudits.ToListAsync());
        Assert.Equal(parentId, audit.UserId);
        Assert.Equal("Verified school enrollment record", audit.Reason);
        Assert.Equal(seeded.AdminEmail, (await dbCheck.Users.FindAsync(audit.PerformedByUserId))!.Email);
        Assert.True(await dbCheck.ParentStudents.AnyAsync(x => x.UserId == parentId && x.StudentId == childId));
        var managerCheck = checkScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updatedUser = (await managerCheck.FindByIdAsync(parentId.ToString()))!;
        Assert.True(await managerCheck.IsInRoleAsync(updatedUser, RoleNames.Parent));
        Assert.Equal(isActive, updatedUser.IsActive);
        Assert.Null(updatedUser.LockoutEnd);
    }

    [Fact]
    public async Task ResetPasswordRequiresAdminAndRejectsSelfResetOrMissingUser()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var seeded = await factory.SeedUsersAsync();
        var path = $"/api/admin/users/{seeded.TeacherId}/reset-password";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, new { reason = "Reset" })).StatusCode);
        var teacherToken = await LoginAsync(client, seeded.TeacherEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, new { reason = "Reset" })).StatusCode);
        var adminToken = await LoginAsync(client, seeded.AdminEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/users/{me.GetProperty("id").GetGuid()}/reset-password", new { reason = "Self" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/reset-password", new { reason = "Missing" })).StatusCode);
    }

    internal static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/auth/login", new { identifier = email, password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    public sealed class SqliteTestModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            // SQLite has no native DateTimeOffset ordering. This test-only converter
            // lets integration tests exercise history queries; production uses PostgreSQL.
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties())
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }

    internal sealed class AuthTestFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        private readonly Dictionary<string, string?> _previousEnvironment = new();
        private readonly string? _postgresConnection;
        private readonly string? _schema;
        private bool _disposed;
        private readonly MealTrace.Application.Abstractions.Notifications.IRegistrationOtpSender? _otpSender;
        private readonly TimeProvider? _clock;
        private readonly Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? _commands;

        public AuthTestFactory(bool postgres = false, TimeProvider? clock = null,
            Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? commands = null,
            MealTrace.Application.Abstractions.Notifications.IRegistrationOtpSender? otpSender = null)
        {
            _otpSender = otpSender;
            _clock = clock;
            _commands = commands;
            if (postgres)
            {
                var connection = Environment.GetEnvironmentVariable("MEALTRACE_TEST_CONNECTION")
                    ?? throw new InvalidOperationException("PostgreSQL test connection is not configured.");
                _schema = "mealtrace_test_" + Guid.NewGuid().ToString("N");
                using var admin = new Npgsql.NpgsqlConnection(connection);
                admin.Open();
                using var command = admin.CreateCommand();
                command.CommandText = $"CREATE SCHEMA \"{_schema}\"";
                command.ExecuteNonQuery();
                _postgresConnection = new Npgsql.NpgsqlConnectionStringBuilder(connection) { SearchPath = _schema }.ConnectionString;
            }
            else _connection.Open();
            foreach (var setting in new Dictionary<string, string>
            {
                ["ConnectionStrings__MealTrace"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Jwt__Key"] = _key,
                ["Jwt__Issuer"] = "MealTrace.Api.Tests",
                ["Jwt__Audience"] = "MealTrace.Api.Tests",
            })
            {
                _previousEnvironment[setting.Key] = Environment.GetEnvironmentVariable(setting.Key);
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MealTrace"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Jwt:Key"] = _key,
                ["Jwt:Issuer"] = "MealTrace.Api.Tests",
                ["Jwt:Audience"] = "MealTrace.Api.Tests",
            }));
            builder.ConfigureServices(services =>
            {
                if (_otpSender is not null)
                {
                    services.RemoveAll<MealTrace.Application.Abstractions.Notifications.IRegistrationOtpSender>();
                    services.AddSingleton(_otpSender);
                    services.RemoveAll<MealTrace.Application.Features.Notifications.NotificationPolicy>();
                    services.AddSingleton(new MealTrace.Application.Features.Notifications.NotificationPolicy(true, "0901234567"));
                }
                if (_clock is not null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(_clock); }
                services.RemoveAll<DbContextOptions<MealTraceDbContext>>();
                services.AddDbContext<MealTraceDbContext>(options =>
                {
                    if (_postgresConnection is not null) options.UseNpgsql(_postgresConnection);
                    else options.UseSqlite(_connection).ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>();
                    if (_commands is not null) options.AddInterceptors(_commands);
                });
            });
        }

        public async Task<(Guid TeacherId, Guid ClassId, string TeacherEmail, string AdminEmail, string Password)> SeedUsersAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            if (_postgresConnection is not null) await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            else await db.Database.EnsureCreatedAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var role in RoleNames.All)
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            var password = "Test!9" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var admin = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Admin thử nghiệm", UserName = "admin@test.local", Email = "admin@test.local", EmailConfirmed = true };
            var teacher = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Giáo viên thử nghiệm", UserName = "teacher@test.local", Email = "teacher@test.local", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(admin, password)).Succeeded);
            Assert.True((await users.CreateAsync(teacher, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, RoleNames.Admin)).Succeeded);
            Assert.True((await users.AddToRoleAsync(teacher, RoleNames.Teacher)).Succeeded);
            var room = new SchoolClass { Name = "Lớp test", SchoolYear = "2026-2027" };
            db.Classes.Add(room);
            var testToday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
            db.AcademicYears.Add(new AcademicYear { Code = room.SchoolYear, StartDate = testToday.AddYears(-1), EndDate = testToday.AddMonths(8) });
            await db.SaveChangesAsync();
            return (teacher.Id, room.Id, teacher.Email!, admin.Email!, password);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            base.Dispose(disposing: disposing);
            if (disposing)
            {
                _connection.Dispose();
                foreach (var setting in _previousEnvironment)
                    Environment.SetEnvironmentVariable(setting.Key, setting.Value);
                if (_postgresConnection is not null)
                {
                    // Only the randomly generated test schema is removed; never delete the database.
                    if (_schema is null || !System.Text.RegularExpressions.Regex.IsMatch(_schema, "^mealtrace_test_[a-f0-9]{32}$"))
                        throw new InvalidOperationException("Unsafe test schema name.");
                    using var admin = new Npgsql.NpgsqlConnection(_postgresConnection);
                    admin.Open();
                    using var command = admin.CreateCommand();
                    command.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Data;
using MealTrace.Api.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEALTRACE_TEST_CONNECTION")))
            Skip = "Set MEALTRACE_TEST_CONNECTION to run against an isolated PostgreSQL schema.";
    }
}

public sealed class PostgresConcurrencyTests
{
    [PostgresFact]
    public async Task SimultaneousExceptionsWithSameSourceAppendOnlyOneEventAndRestoreInSequence()
    {
        var clock = new MealExceptionTests.TestClock(); using var factory = new AuthTestFactory(postgres: true, clock: clock);
        using var client = factory.CreateClient(); var seed = await MealExceptionTests.SeedScenario(factory, clock);
        await MealExceptionTests.Authorize(client, seed.AdminEmail, seed.Password);
        var responses = await Task.WhenAll(MealExceptionTests.Record(client, seed.DayId, seed.AbsentId, "EAT"), MealExceptionTests.Record(client, seed.DayId, seed.AbsentId, "ABSENT"));
        var winner = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var firstId = await MealExceptionTests.EventId(winner);
        await MealExceptionTests.EventId(await MealExceptionTests.Record(client, seed.DayId, seed.AbsentId, "DEFAULT", firstId));
        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions");
        var child = decisions.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.AbsentId);
        Assert.False(child.GetProperty("willEat").GetBoolean()); Assert.Equal("PARENT_ABSENCE", child.GetProperty("source").GetString());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var records = await db.MealRegistrations.OrderBy(x => x.Sequence).ToListAsync(); Assert.Equal(2, records.Count);
        Assert.Equal(1, records[0].Sequence); Assert.Equal(2, records[1].Sequence); Assert.Equal(firstId, records[1].SupersedesId);
        Assert.All(records, x => Assert.NotNull(x.RecordedByUserId));
    }

    [PostgresFact]
    public async Task SimultaneousTransfersWithSameRevisionCommitOnlyOneEnrollment()
    {
        using var factory = new AuthTestFactory(postgres: true); using var client = factory.CreateClient(); var seed = await factory.SeedUsersAsync();
        Guid childId; Guid roomId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var room = new SchoolClass { Name = "Transfer destination", SchoolYear = "2026-2027" }; db.Classes.Add(room); roomId = room.Id;
            var child = new Student { FullName = "Concurrent child", ClassId = seed.ClassId }; db.Students.Add(child); childId = child.Id;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var input = new { classId = roomId, effectiveDate = StudentAdministrationEndpoints.Today.AddDays(1), reason = "Transfer", revision = 0 };
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments", input), client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments", input));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(2, await database.Enrollments.CountAsync(x => x.StudentId == childId));
        Assert.Equal(1, await database.Enrollments.CountAsync(x => x.StudentId == childId && x.EndDate == null));
    }

    [PostgresFact]
    public async Task SimultaneousAbsencesAndSettlementsDoNotDuplicateRows()
    {
        using var factory = new AuthTestFactory(postgres: true); using var client = factory.CreateClient(); var seed = await factory.SeedUsersAsync();
        Guid childId; Guid dayId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { FullName = "Concurrent meal child", ClassId = seed.ClassId }; db.Students.Add(child); childId = child.Id;
            await db.SaveChangesAsync();
            var day = new MealDay { Date = StudentAdministrationEndpoints.Today, SchoolYear = "2026-2027", MealType = "Lunch", CutoffAt = DateTimeOffset.UtcNow };
            db.MealDays.Add(day); dayId = day.Id; await db.SaveChangesAsync();
        }
        var adminToken = await LoginAsync(client, seed.AdminEmail, seed.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var linked = await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0901234567", fullName = "Guardian" }); linked.EnsureSuccessStatusCode();
        var result = await linked.Content.ReadFromJsonAsync<JsonElement>();
        var parentToken = await LoginAsync(client, "0901234567", result.GetProperty("temporaryPassword").GetString()!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);
        var date = StudentAdministrationEndpoints.Today.AddDays(1);
        var absence = new { studentId = childId, fromDate = date, toDate = date, reason = "Absent" };
        var reports = await Task.WhenAll(client.PostAsJsonAsync("/api/parent/absences", absence), client.PostAsJsonAsync("/api/parent/absences", absence));
        Assert.Single(reports, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(reports, x => x.StatusCode == HttpStatusCode.Conflict);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var settlements = await Task.WhenAll(client.PostAsync($"/api/meal-days/{dayId}/settle", null), client.PostAsync($"/api/meal-days/{dayId}/settle", null));
        Assert.Single(settlements, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(settlements, x => x.StatusCode == HttpStatusCode.Conflict);
        using var check = factory.Services.CreateScope(); var dbCheck = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(1, await dbCheck.MealAbsences.CountAsync(x => x.StudentId == childId));
        Assert.Equal(1, await dbCheck.PortionSettlements.CountAsync(x => x.MealDayId == dayId));
    }
}

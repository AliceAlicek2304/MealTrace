using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class LongMealAbsenceTests
{
    [Fact]
    public async Task SchoolYearMustBeConfiguredAndCannotBeSilentlyChangedAfterRegistration()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        var start = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.AcademicYears.RemoveRange(await db.AcademicYears.ToListAsync()); await db.SaveChangesAsync();
        }
        await Authorize(client, "parent@test.local", seed.Password);
        var input = new { studentId = seed.PresentId, fromDate = start, toDate = start.AddDays(20), reason = "Không ăn đến hết năm học" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/parent/absences", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/admin/academic-years/2026-2027", new { startDate = start, endDate = start.AddDays(20) })).StatusCode);
        await Authorize(client, seed.AdminEmail, seed.Password);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/admin/academic-years/2026-2027", new { startDate = start, endDate = start.AddDays(20) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/admin/academic-years/2026-2027", new { startDate = start, endDate = start.AddDays(25) })).StatusCode);
        await Authorize(client, "parent@test.local", seed.Password);
        var created = await client.PostAsJsonAsync("/api/parent/absences", input); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var finalScope = factory.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal("2026-2027", (await finalDb.MealAbsences.SingleAsync(x => x.StudentId == seed.PresentId)).SchoolYear);
        var children = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Equal(start.AddDays(20).ToString("yyyy-MM-dd"), children.EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.PresentId).GetProperty("yearEndDate").GetString());
    }

    [PostgresFact]
    public async Task ConcurrentPeriodChangesReplaceOriginalOnlyOnce()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: true, clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, "parent@test.local", seed.Password);
        var start = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var created = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = seed.PresentId, fromDate = start, toDate = start.AddMonths(8), reason = "Không ăn cả năm học" });
        created.EnsureSuccessStatusCode(); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var changes = await Task.WhenAll(new[] { 7, 30 }.Select(days => client.PostAsJsonAsync($"/api/parent/absences/{id}/replace",
            new { studentId = seed.PresentId, fromDate = start, toDate = start.AddDays(days - 1), reason = "Đổi khoảng" })));
        Assert.Single(changes, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(changes, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(2, await db.MealAbsences.CountAsync(x => x.StudentId == seed.PresentId));
        Assert.Equal(1, await db.MealAbsences.CountAsync(x => x.StudentId == seed.PresentId && x.CancelledAt == null));
    }

    [Fact]
    public async Task UntilSchoolYearEndKeepsEnrollmentAndCanBeShortenedOrCancelled()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, "parent@test.local", seed.Password);
        var start = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var end = start.AddMonths(8);
        var invalid = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = seed.PresentId, fromDate = start, toDate = end.AddDays(1), reason = "Không ăn" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var created = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = seed.PresentId, fromDate = start, toDate = end, reason = "Đi học nhưng không ăn tại trường" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var updated = await client.PostAsJsonAsync($"/api/parent/absences/{id}/replace", new { studentId = seed.PresentId, fromDate = start, toDate = start.AddDays(6), reason = "Đổi sang một tuần" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var replacement = (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/parent/absences/{id}/replace", new { studentId = seed.PresentId, fromDate = start, toDate = end, reason = "Nguồn cũ" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/parent/absences/{replacement}/cancel", null)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var original = await db.MealAbsences.FindAsync(id); Assert.Equal(end, original!.ToDate); Assert.NotNull(original.CancelledAt);
        Assert.True(await db.Enrollments.AnyAsync(x => x.StudentId == seed.PresentId && x.EndDate == null));
        Assert.Equal(2, await db.MealAbsences.CountAsync(x => x.StudentId == seed.PresentId));
        await Authorize(client, seed.AdminEmail, seed.Password);
        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions");
        Assert.True(decisions.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.PresentId).GetProperty("willEat").GetBoolean());
    }

    [Fact]
    public async Task ReplacementAfterCutoffPreservesEarlierDecisionAndRejectsOverlapOrWrongChild()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, "parent@test.local", seed.Password);
        var start = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var created = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = seed.PresentId, fromDate = start, toDate = start.AddMonths(8), reason = "Không ăn" });
        created.EnsureSuccessStatusCode(); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        clock.Set(seed.Cutoff.AddSeconds(1));
        var changed = await client.PostAsJsonAsync($"/api/parent/absences/{id}/replace", new { studentId = seed.PresentId, fromDate = start.AddDays(1), toDate = start.AddDays(7), reason = "Ăn lại hôm nay" });
        changed.EnsureSuccessStatusCode(); var replacement = (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/parent/absences/{replacement}/replace", new { studentId = seed.AbsentId, fromDate = start, toDate = start.AddDays(1), reason = "Sai trẻ" })).StatusCode);
        var other = await client.PostAsJsonAsync("/api/parent/absences", new { studentId = seed.PresentId, fromDate = start.AddDays(10), toDate = start.AddDays(12), reason = "Khoảng khác" });
        other.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/parent/absences/{replacement}/replace", new { studentId = seed.PresentId, fromDate = start.AddDays(1), toDate = start.AddDays(11), reason = "Trùng" })).StatusCode);
        await Authorize(client, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/parent/absences/{replacement}/replace", new { studentId = seed.PresentId, fromDate = start, toDate = start.AddDays(2), reason = "Sai role" })).StatusCode);
        await Authorize(client, seed.AdminEmail, seed.Password);
        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions");
        Assert.False(decisions.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.PresentId).GetProperty("willEat").GetBoolean());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Null((await db.MealAbsences.FindAsync(replacement))!.CancelledAt);
    }
}

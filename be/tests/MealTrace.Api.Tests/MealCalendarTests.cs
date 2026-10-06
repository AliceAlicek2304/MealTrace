using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class MealCalendarTests
{
    private const string Root = "/api/admin/meal-calendar/2026-2027";
    private static async Task<(Scenario Seed, DateOnly Monday)> Setup(AuthTestFactory factory, TestClock clock, HttpClient client, int days = 30)
    {
        var seed = await SeedScenario(factory, clock); await Authorize(client, seed.AdminEmail, seed.Password);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var monday = today.AddDays(1); while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var year = (await db.AcademicYears.FindAsync("2026-2027"))!; year.StartDate = monday; year.EndDate = monday.AddDays(days - 1); await db.SaveChangesAsync();
        var setup = await client.PutAsJsonAsync($"{Root}/schedule", new { weekdays = new[] { 1, 2, 3, 4, 5 }, mealTypes = new[] { "Lunch", "Snack" }, expectedRevision = 0, reason = "School schedule" });
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        return (seed, monday);
    }
    private static async Task<JsonElement> Preview(HttpClient client, DateOnly from, DateOnly to, int revision = 1)
    {
        var response = await client.PostAsJsonAsync($"{Root}/preview", new { from, to, expectedRevision = revision }); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static Task<HttpResponseMessage> Generate(HttpClient client, DateOnly from, DateOnly to, JsonElement preview, int revision = 1) =>
        client.PostAsJsonAsync($"{Root}/generate", new { from, to, expectedRevision = revision, previewToken = preview.GetProperty("previewToken").GetString() });
    private static Task<HttpResponseMessage> EditDay(HttpClient client, DateOnly day, string mode, int revision, string[]? types = null) =>
        client.PutAsJsonAsync($"{Root}/days/{day:yyyy-MM-dd}", new { mode, mealTypes = types ?? [], expectedRevision = revision, reason = "Calendar change" });

    [Fact]
    public async Task FullSchoolYearPreviewSkipsHolidayIncludesMakeupAndGenerationIsIdempotent()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var (_, monday) = await Setup(factory, clock, client, 365); var end = monday.AddDays(364);
        Assert.Equal(HttpStatusCode.OK, (await EditDay(client, monday, "CLOSED", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EditDay(client, monday.AddDays(5), "OPEN", 2, ["Lunch"])).StatusCode);
        var preview = await Preview(client, monday, end, 3);
        var weekdayCount = Enumerable.Range(0, 365).Count(x => monday.AddDays(x).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        var expected = weekdayCount * 2 - 2 + 1;
        Assert.Equal(expected, preview.GetProperty("createCount").GetInt32());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            Assert.False(await db.MealDays.AnyAsync(x => x.Date >= monday)); // preview is read-only
        }
        var generated = await Generate(client, monday, end, preview, 3); generated.EnsureSuccessStatusCode();
        Assert.Equal(expected, (await generated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32());
        var again = await Preview(client, monday, end, 3); Assert.Equal(0, again.GetProperty("createCount").GetInt32());
        Assert.Equal(expected, again.GetProperty("existingCount").GetInt32()); (await Generate(client, monday, end, again, 3)).EnsureSuccessStatusCode();
        using var finalScope = factory.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(expected, await finalDb.MealDays.CountAsync(x => x.Date >= monday));
        Assert.False(await finalDb.MealDays.AnyAsync(x => x.Date == monday));
        Assert.Single(await finalDb.MealDays.Where(x => x.Date == monday.AddDays(5)).ToListAsync());
        Assert.True(await finalDb.MealDays.Where(x => x.Date >= monday).AllAsync(x => x.SettledAt == null));
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/meal-days/workflow?date=" + monday.AddDays(1).ToString("yyyy-MM-dd"));
        Assert.Equal(2, filtered.GetProperty("total").GetInt32()); Assert.Equal(2, filtered.GetProperty("items").GetArrayLength());
        var secondPage = await client.GetFromJsonAsync<JsonElement>("/api/meal-days/workflow?page=2");
        Assert.Equal(expected + 1, secondPage.GetProperty("total").GetInt32()); Assert.Equal(25, secondPage.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ClosingCancelsWithoutDeletingAndReturningToWeeklyRestoresSameSessionIds()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var (seed, monday) = await Setup(factory, clock, client); var preview = await Preview(client, monday, monday); (await Generate(client, monday, monday, preview)).EnsureSuccessStatusCode();
        Guid lunch;
        using (var scope = factory.Services.CreateScope()) lunch = (await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().MealDays.SingleAsync(x => x.Date == monday && x.MealType == "Lunch")).Id;
        Assert.Equal(HttpStatusCode.OK, (await EditDay(client, monday, "CLOSED", 1)).StatusCode);
        var portions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{lunch}/portions"); Assert.True(portions.GetProperty("isCancelled").GetBoolean()); Assert.Empty(portions.GetProperty("classes").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await Record(client, lunch, seed.PresentId, "EAT")).StatusCode);
        var settle = await client.PostAsync($"/api/meal-days/{lunch}/settle", null); Assert.Equal(HttpStatusCode.Conflict, settle.StatusCode); Assert.Contains("hủy", await settle.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await EditDay(client, monday, "DEFAULT", 2)).StatusCode);
        using var finalScope = factory.Services.CreateScope(); var db = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.False((await db.MealDays.FindAsync(lunch))!.IsCancelled); Assert.Equal(2, await db.MealDays.CountAsync(x => x.Date == monday));
        Assert.True((await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{lunch}/decisions")).GetProperty("items").GetArrayLength() > 0);
        var audits = await db.MealCalendarAudits.OrderBy(x => x.RecordedAt).ToListAsync(); Assert.Equal(4, audits.Count); Assert.All(audits, x => Assert.False(string.IsNullOrWhiteSpace(x.ActorName)));
    }

    [Fact]
    public async Task StalePreviewStaleEditInvalidRangeAndUnscheduledManualMealAreRejected()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var (seed, monday) = await Setup(factory, clock, client); var preview = await Preview(client, monday, monday);
        Assert.Equal(HttpStatusCode.OK, (await EditDay(client, monday, "CLOSED", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Generate(client, monday, monday, preview)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await EditDay(client, monday, "OPEN", 1, ["Lunch"])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Root}/preview", new { from = monday.AddDays(-1), to = monday, expectedRevision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/meal-days", new { date = monday, mealType = "Lunch", schoolYear = "2026-2027" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/meal-days", new { date = monday.AddDays(1), mealType = "Unknown", schoolYear = "2026-2027" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/meal-days", new { date = monday.AddDays(1), mealType = "Lunch", schoolYear = "2026-2027" })).StatusCode);
        await Authorize(client, seed.TeacherEmail, seed.Password); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Root)).StatusCode);
        await Authorize(client, "parent@test.local", seed.Password); Assert.Equal(HttpStatusCode.Forbidden, (await EditDay(client, monday, "CLOSED", 2)).StatusCode);
    }

    [Fact]
    public async Task CutoffBoundaryPreventsChangingDayOrApplyingEarlierGenerationPreview()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var (_, monday) = await Setup(factory, clock, client); var preview = await Preview(client, monday, monday);
        clock.Set(MealCalendarUseCases.Cutoff(monday));
        Assert.Equal(HttpStatusCode.Conflict, (await EditDay(client, monday, "CLOSED", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Generate(client, monday, monday, preview)).StatusCode);
        var now = await Preview(client, monday, monday); Assert.All(now.GetProperty("items").EnumerateArray(), x => Assert.Equal("LOCKED", x.GetProperty("action").GetString()));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>(); Assert.False(await db.MealDays.AnyAsync(x => x.Date == monday));
    }

    [Fact]
    public async Task ChangingWeeklyScheduleCannotCancelSignedSessionAndRollsBackEntireEdit()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var (_, monday) = await Setup(factory, clock, client); var preview = await Preview(client, monday, monday); (await Generate(client, monday, monday, preview)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>(); var lunch = await db.MealDays.SingleAsync(x => x.Date == monday && x.MealType == "Lunch");
            lunch.SettledAt = clock.GetUtcNow(); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await EditDay(client, monday, "CLOSED", 1)).StatusCode);
        var changed = await client.PutAsJsonAsync($"{Root}/schedule", new { weekdays = new[] { 2, 3, 4, 5 }, mealTypes = new[] { "Lunch", "Snack" }, expectedRevision = 1, reason = "Remove Monday" });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var finalScope = factory.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(1, (await finalDb.MealSchedules.FindAsync("2026-2027"))!.Revision); Assert.False(await finalDb.MealDays.AnyAsync(x => x.Date == monday && x.IsCancelled));
    }

    [PostgresFact]
    public async Task ConcurrentGenerationOfSamePreviewCreatesOnlyOneSetOfSessions()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: true, clock: clock); using var client = factory.CreateClient();
        var (_, monday) = await Setup(factory, clock, client); var preview = await Preview(client, monday, monday.AddDays(6));
        var responses = await Task.WhenAll(Generate(client, monday, monday.AddDays(6), preview), Generate(client, monday, monday.AddDays(6), preview));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>(); Assert.Equal(10, await db.MealDays.CountAsync(x => x.Date >= monday));
        Assert.Equal(1, await db.MealCalendarAudits.CountAsync(x => x.Kind == "GENERATE"));
    }

    [PostgresFact]
    public async Task ConcurrentDayEditsWithSameRevisionKeepSingleWinnerAndAudit()
    {
        var clock = new TestClock(); using var factory = new AuthTestFactory(postgres: true, clock: clock); using var client = factory.CreateClient();
        var (_, monday) = await Setup(factory, clock, client);
        var responses = await Task.WhenAll(EditDay(client, monday, "CLOSED", 1), EditDay(client, monday, "OPEN", 1, ["Lunch"]));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(2, (await db.MealSchedules.FindAsync("2026-2027"))!.Revision); Assert.Single(await db.MealCalendarExceptions.ToListAsync()); Assert.Equal(2, await db.MealCalendarAudits.CountAsync());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class AcademicYearTests
{
    [Fact]
    public async Task NextYearPreviewHandlesLeapDayAndDoesNotCreateUntilAdminSaves()
    {
        using var factory = new AuthTestFactory(); using var client = factory.CreateClient(); var seed = await factory.SeedUsersAsync();
        await Authorize(client, seed.AdminEmail, seed.Password);
        var source = new { startDate = new DateOnly(2027, 9, 1), endDate = new DateOnly(2028, 2, 29) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/admin/academic-years/2027-2028", source)).StatusCode);
        var preview = await client.GetFromJsonAsync<JsonElement>("/api/admin/academic-years/2027-2028/next");
        Assert.Equal("2028-2029", preview.GetProperty("code").GetString());
        Assert.Equal("2028-09-01", preview.GetProperty("startDate").GetString());
        Assert.Equal("2029-02-28", preview.GetProperty("endDate").GetString());
        Assert.False(preview.GetProperty("isConfigured").GetBoolean());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            Assert.False(await db.AcademicYears.AnyAsync(x => x.Code == "2028-2029"));
            Assert.False(await db.Classes.AnyAsync(x => x.SchoolYear == "2027-2028"));
        }
        // The proposed dates remain editable; saving uses the school's confirmed dates.
        var saved = await client.PutAsJsonAsync("/api/admin/academic-years/2028-2029", new { startDate = new DateOnly(2028, 9, 5), endDate = new DateOnly(2029, 5, 30), sourceYearCode = "2027-2028" });
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/admin/academic-years/2027-2028/next")).GetProperty("isConfigured").GetBoolean());
        var years = await client.GetFromJsonAsync<JsonElement>("/api/admin/academic-years");
        Assert.Contains(years.EnumerateArray(), x => x.GetProperty("code").GetString() == "2028-2029");
        using var finalScope = factory.Services.CreateScope(); var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(new DateOnly(2028, 9, 5), (await finalDb.AcademicYears.FindAsync("2028-2029"))!.StartDate);
        Assert.Equal(new DateOnly(2027, 9, 1), (await finalDb.AcademicYears.FindAsync("2027-2028"))!.StartDate);
    }

    [Fact]
    public async Task OnlyAdminCanCopyYearsAndInvalidCodesDatesOrSourceAreRejected()
    {
        using var factory = new AuthTestFactory(); using var client = factory.CreateClient(); var seed = await factory.SeedUsersAsync();
        await Authorize(client, seed.TeacherEmail, seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/academic-years/2026-2027/next")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/admin/academic-years/2027-2028", new { startDate = "2027-09-01", endDate = "2028-05-31" })).StatusCode);
        await Authorize(client, seed.AdminEmail, seed.Password);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/admin/academic-years/2027-2029", new { startDate = "2027-09-01", endDate = "2028-05-31" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/admin/academic-years/2027-2028", new { startDate = "2028-05-31", endDate = "2027-09-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/admin/academic-years/2028-2029", new { startDate = "2028-09-01", endDate = "2029-05-31", sourceYearCode = "2026-2027" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/academic-years/2030-2031/next")).StatusCode);
    }
}

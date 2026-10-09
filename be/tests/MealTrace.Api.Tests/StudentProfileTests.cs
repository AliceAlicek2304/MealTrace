using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class StudentProfileTests
{
    [Fact]
    public async Task AdminCreatesEditsAndClearsOptionalProfileWhileRejectingInvalidAndStaleUpdates()
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var created = await client.PostAsJsonAsync("/api/students", new { fullName = "Trẻ hồ sơ", classId = seed.ClassId, dateOfBirth = "2021-03-04", gender = "MALE" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var revision = await Revision();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Sai", revision, dateOfBirth = "2999-01-01", gender = "MALE" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Sai", revision, gender = "INVALID" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Đã sửa", revision, dateOfBirth = "2022-04-05", gender = "FEMALE" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Cũ", revision })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var child = await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.SingleAsync();
            Assert.Equal(new DateOnly(2022, 4, 5), child.DateOfBirth);
            Assert.Equal("FEMALE", child.Gender);
        }
        revision = await Revision();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Chỉ đổi tên", revision })).StatusCode);
        using (var check = factory.Services.CreateScope())
        {
            var preserved = await check.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.SingleAsync();
            Assert.Equal(new DateOnly(2022, 4, 5), preserved.DateOfBirth); Assert.Equal("FEMALE", preserved.Gender);
        }
        revision = await Revision();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/admin/students/{id}", new { fullName = "Đã sửa", revision, dateOfBirth = (string?)null, gender = (string?)null, updateProfile = true })).StatusCode);
        using var final = factory.Services.CreateScope();
        var cleared = await final.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.SingleAsync();
        Assert.Null(cleared.DateOfBirth); Assert.Null(cleared.Gender);
        async Task<int> Revision()
        {
            using var scope = factory.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.SingleAsync()).Revision;
        }
    }

    [Theory]
    [InlineData("2999-01-01", "MALE")]
    [InlineData("2021-01-01", "INVALID")]
    public async Task InvalidCreateNeverWrites(string birth, string gender)
    {
        using var factory = new AuthTestFactory(); var seed = await factory.SeedUsersAsync(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/students", new { fullName = "Sai", classId = seed.ClassId, dateOfBirth = birth, gender })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.AnyAsync());
    }
}

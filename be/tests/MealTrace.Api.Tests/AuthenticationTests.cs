using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
            roles = new[] { RoleNames.KitchenStaff, RoleNames.Accountant }, status = "ACTIVE",
            classIds = Array.Empty<Guid>(), studentIds = Array.Empty<Guid>(),
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
        Assert.Contains(RoleNames.Accountant, roles);
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

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private sealed class AuthTestFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        private readonly Dictionary<string, string?> _previousEnvironment = new();

        public AuthTestFactory()
        {
            _connection.Open();
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
                services.RemoveAll<DbContextOptions<MealTraceDbContext>>();
                services.AddDbContext<MealTraceDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public async Task<(Guid TeacherId, Guid ClassId, string TeacherEmail, string AdminEmail, string Password)> SeedUsersAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            await db.Database.EnsureCreatedAsync();
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
            await db.SaveChangesAsync();
            return (teacher.Id, room.Id, teacher.Email!, admin.Email!, password);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing: disposing);
            if (disposing)
            {
                _connection.Dispose();
                foreach (var setting in _previousEnvironment)
                    Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            }
        }
    }
}

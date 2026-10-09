using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Dtos.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class ParentSignupTests
{
    public sealed class RecordingOtpSender : IRegistrationOtpSender
    {
        public string? LastCode { get; private set; }
        public NotificationOutcome Outcome { get; set; } = NotificationOutcome.Accepted;
        public int Calls { get; private set; }
        public Task<NotificationSendResult> SendAsync(string number, string code, CancellationToken ct)
        {
            Assert.Equal("84901234567", number);
            LastCode = code;
            Calls++;
            return Task.FromResult(new NotificationSendResult(Outcome, null, "Test send"));
        }
    }

    internal static async Task<RegisterParentRequest> SignupInputAsync(HttpClient client, AuthTestFactory factory, string name = "Phụ huynh")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register/otp", new { phoneNumber = "+84 901234567" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var challenge = await response.Content.ReadFromJsonAsync<ParentOtpResponse>();
        var sender = (RecordingOtpSender)factory.Services.GetRequiredService<IRegistrationOtpSender>();
        return new(name, "0901234567", Password, challenge!.ChallengeId, sender.LastCode);
    }

    // Generated for isolated test accounts, never used by the application seeder.
    private static readonly string Password = $"Aa1!{Guid.NewGuid():N}";

    [Fact]
    public async Task PublicSignupLimitsRepeatedAttempts()
    {
        using var factory = new AuthTestFactory(otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Phụ huynh", phoneNumber = "123", password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Phụ huynh", phoneNumber = "0901234567", password = Password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Users.CountAsync());
    }

    [Fact]
    public Task PublicSignupCreatesOnlyAnUnlinkedVerifiedParent() => VerifySignup(false);

    [PostgresFact]
    public Task PostgreSqlSignupCreatesOnlyAnUnlinkedVerifiedParent() => VerifySignup(true);

    [PostgresFact]
    public async Task ConcurrentSignupCreatesOnlyOneAccountForThePhone()
    {
        using var factory = new AuthTestFactory(postgres: true, otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var input = await SignupInputAsync(first, factory, "Phụ huynh");
        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/auth/register", input), second.PostAsJsonAsync("/api/auth/register", input));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict, HttpStatusCode.BadRequest }));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.PhoneNumber == input.PhoneNumber));
        var login = await first.PostAsJsonAsync("/api/auth/login", new { identifier = input.PhoneNumber, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { RoleNames.Parent }, body.GetProperty("user").GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        foreach (var response in responses) response.Dispose();
    }

    private static async Task VerifySignup(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres: postgres, otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", await SignupInputAsync(client, factory, "  Phụ huynh mới  "));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0901234567", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { RoleNames.Parent }, body.GetProperty("user").GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/meal-days/workflow")).StatusCode);
        var children = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Empty(children.EnumerateArray());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var account = await db.Users.SingleAsync(x => x.PhoneNumber == "0901234567");
        Assert.Equal("Phụ huynh mới", account.FullName);
        Assert.True(account.PhoneNumberConfirmed);
        Assert.False(account.EmailConfirmed);
        Assert.NotEqual(Password, account.PasswordHash);
        Assert.False(await db.ParentStudents.AnyAsync(x => x.UserId == account.Id));
        Assert.False(await db.TeacherAssignments.AnyAsync(x => x.UserId == account.Id));
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("studentIds")]
    [InlineData("isActive")]
    [InlineData("phoneNumberConfirmed")]
    public async Task PublicSignupRejectsPrivilegeOrLinkFields(string field)
    {
        using var factory = new AuthTestFactory(otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        var request = new Dictionary<string, object> { ["fullName"] = "Phụ huynh", ["phoneNumber"] = "0901234567", ["password"] = Password, [field] = field is "roles" or "studentIds" ? new[] { "ADMIN" } : true };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Users.AnyAsync(x => x.PhoneNumber == "0901234567"));
    }

    [Theory]
    [InlineData("", "0901234567", null)]
    [InlineData("Phụ huynh", "123", null)]
    [InlineData("Phụ huynh", "0901234567", "short")]
    [InlineData("Phụ huynh", "0901234567", "lowercaseonly123!")]
    public async Task InvalidSignupDoesNotCreateAnAccount(string name, string phone, string? password)
    {
        using var factory = new AuthTestFactory(otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { fullName = name, phoneNumber = phone, password = password ?? Password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task DuplicateInternationalPhoneDoesNotChangeExistingCredentials()
    {
        using var factory = new AuthTestFactory(otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/register", await SignupInputAsync(client, factory, "Phụ huynh"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Người khác", phoneNumber = "84901234567", password = "DifferentPassword!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0901234567", password = Password })).StatusCode);
    }
}

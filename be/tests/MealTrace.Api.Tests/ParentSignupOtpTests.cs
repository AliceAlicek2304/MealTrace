using System.Net;
using System.Net.Http.Json;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Dtos.Auth;
using MealTrace.Application.Features.Auth;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.ParentSignupTests;

namespace MealTrace.Api.Tests;

public sealed class ParentSignupOtpTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task MissingOtpCannotCreateParent()
    {
        using var factory = new AuthTestFactory();
        await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Phụ huynh", phoneNumber = "0901234567", password = $"Aa1!{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task ResendInvalidatesOldCodeAndVerifiedSignupConsumesNewCode()
    {
        var clock = new Clock(); var sender = new RecordingOtpSender();
        using var factory = new AuthTestFactory(clock: clock, otpSender: sender);
        await factory.SeedUsersAsync(); using var client = factory.CreateClient();
        var old = await SignupInputAsync(client, factory);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register/otp", new { old.PhoneNumber })).StatusCode);
        clock.Now += TimeSpan.FromSeconds(61);
        var fresh = await SignupInputAsync(client, factory);
        Assert.NotEqual(old.ChallengeId, fresh.ChallengeId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", old)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/register", fresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", fresh)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var otp = await db.ParentSignupOtps.SingleAsync();
        Assert.True(otp.Used); Assert.Empty(otp.CodeHash);
        Assert.True((await db.Users.SingleAsync(x => x.PhoneNumber == old.PhoneNumber)).PhoneNumberConfirmed);
    }

    [Fact]
    public async Task ExpiredOrDifferentPhoneCannotUseTheCode()
    {
        var clock = new Clock();
        using var factory = new AuthTestFactory(clock: clock, otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync(); using var client = factory.CreateClient();
        var input = await SignupInputAsync(client, factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", input with { PhoneNumber = "0905555555" })).StatusCode);
        clock.Now += TimeSpan.FromMinutes(5);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", input)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task FiveWrongAttemptsLockTheChallengePersistently()
    {
        using var factory = new AuthTestFactory(otpSender: new RecordingOtpSender());
        await factory.SeedUsersAsync(); using var client = factory.CreateClient();
        var input = await SignupInputAsync(client, factory);
        using (var scope = factory.Services.CreateScope())
        {
            var otp = await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().ParentSignupOtps.SingleAsync();
            Assert.NotEqual(input.OtpCode, otp.CodeHash);
            Assert.Equal(64, otp.CodeHash.Length);
        }
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", input with { OtpCode = input.OtpCode == "000000" ? "111111" : "000000" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            Assert.Equal(5, (await db.ParentSignupOtps.SingleAsync()).FailedAttempts);
            Assert.False((await scope.ServiceProvider.GetRequiredService<AuthService>().RegisterParentAsync(input)).IsSuccess);
            Assert.Equal(2, await db.Users.CountAsync());
        }
    }

    [Theory]
    [InlineData(NotificationOutcome.Failed, HttpStatusCode.BadRequest)]
    [InlineData(NotificationOutcome.Unknown, HttpStatusCode.OK)]
    public async Task SendFailureNeverCreatesAccountAndUnknownDoesNotResend(NotificationOutcome outcome, HttpStatusCode expected)
    {
        var sender = new RecordingOtpSender { Outcome = outcome };
        using var factory = new AuthTestFactory(otpSender: sender);
        await factory.SeedUsersAsync(); using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register/otp", new { phoneNumber = "0901234567" });
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain(sender.LastCode!, await response.Content.ReadAsStringAsync());
        Assert.Equal(1, sender.Calls);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task HourlyPhoneLimitSurvivesNewRequestsAndScopes()
    {
        var clock = new Clock(); var sender = new RecordingOtpSender();
        using var factory = new AuthTestFactory(clock: clock, otpSender: sender);
        await factory.SeedUsersAsync();
        for (var i = 0; i < 6; i++)
        {
            using var scope = factory.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ParentSignupOtpService>().RequestAsync(new("0901234567"));
            Assert.Equal(i < 5, result.IsSuccess);
            clock.Now += TimeSpan.FromSeconds(61);
        }
        Assert.Equal(5, sender.Calls);
    }
}

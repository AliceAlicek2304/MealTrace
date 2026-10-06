using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Application.Abstractions.Notifications;
using MealTrace.Application.Features.Notifications;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class RegistrationNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TeacherRegistersScopedChildAndParentCanLoginEvenIfNotificationFails(bool failNotification)
    {
        using var factory = new AuthTestFactory();
        var seeded = await factory.SeedUsersAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seeded.TeacherId, ClassId = seeded.ClassId });
            await db.SaveChangesAsync();
        }
        var sender = new RecordingSender(failNotification);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationSender>();
            services.AddSingleton<INotificationSender>(sender);
            services.RemoveAll<NotificationPolicy>();
            services.AddSingleton(new NotificationPolicy(true, "0901234567"));
        }));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seeded.TeacherEmail, seeded.Password));
        var child = await client.PostAsJsonAsync("/api/students", new { fullName = "Child Notification", classId = seeded.ClassId });
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);
        var childId = (await child.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var linked = await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0901234567", fullName = "Guardian", sendRegistrationNotification = true });
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal("no-store", linked.Headers.CacheControl!.ToString());
        var result = await linked.Content.ReadFromJsonAsync<JsonElement>();
        var password = result.GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(failNotification ? "UNKNOWN" : "ACCEPTED", result.GetProperty("notification").GetProperty("status").GetString());
        Assert.Equal(1, sender.Calls);
        Assert.Equal("84901234567", sender.Number);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0901234567", fullName = "Guardian", sendRegistrationNotification = true })).StatusCode);
        Assert.Equal(1, sender.Calls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "0901234567", password));
        var children = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Contains(children.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == childId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seeded.AdminEmail, seeded.Password));
        var update = await client.PutAsJsonAsync($"/api/admin/users/{result.GetProperty("parentId").GetGuid()}", new
        {
            fullName = "Guardian", email = (string?)null, phoneNumber = "0907654321", roles = new[] { "PARENT" }, status = "ACTIVE",
            classIds = Array.Empty<Guid>(), studentIds = new[] { childId }, inspectorAccessUntil = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { identifier = "0901234567", password })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, "0907654321", password));
        var afterUpdate = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Contains(afterUpdate.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == childId);
    }

    [Fact]
    public async Task TeacherCannotRegisterOrLinkChildrenOutsideAssignedClass()
    {
        using var factory = new AuthTestFactory();
        var seeded = await factory.SeedUsersAsync();
        Guid childId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var child = new Student { FullName = "Other class", ClassId = seeded.ClassId };
            db.Students.Add(child);
            await db.SaveChangesAsync();
            childId = child.Id;
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seeded.TeacherEmail, seeded.Password));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/students", new { fullName = "Outside scope", classId = seeded.ClassId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/students/{childId}/parents", new { phoneNumber = "0901234567", fullName = "Guardian", sendRegistrationNotification = true })).StatusCode);
    }

    [Fact]
    public async Task ExistingAccountNotificationDoesNotResetPasswordAndOtherNumbersAreNotSent()
    {
        var sender = new RecordingSender(false);
        var service = new ParentRegistrationNotificationService(sender, new NotificationPolicy(true, "0901234567"), new NotificationSendGate(TimeProvider.System));
        Assert.Equal("DISABLED", (await service.SendAsync("0907654321")).Status);
        Assert.Equal(0, sender.Calls);
        Assert.Equal("ACCEPTED", (await service.SendAsync("0901234567")).Status);
        Assert.Equal(1, sender.Calls);
    }

    private sealed class RecordingSender(bool fail) : INotificationSender
    {
        public int Calls { get; private set; }
        public string Number { get; private set; } = "";
        public Task<NotificationSendResult> SendAsync(string number, CancellationToken ct)
        {
            Calls++; Number = number;
            if (fail) throw new HttpRequestException("simulated failure");
            return Task.FromResult(new NotificationSendResult(NotificationOutcome.Accepted, Guid.NewGuid().ToString(), "Accepted, not delivered"));
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class RoleBoundaryTests
{
    [Theory]
    [InlineData(RoleNames.Teacher)]
    [InlineData(RoleNames.KitchenStaff)]
    [InlineData(RoleNames.Parent)]
    public async Task OperationalRolesCannotInvokeOtherRolesManagementActions(string role)
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, new TestClock());
        var email = seed.TeacherEmail;
        if (role != RoleNames.Teacher)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            email = $"{role.ToLowerInvariant()}@boundary.test";
            var user = new ApplicationUser { UserName = email, Email = email, FullName = role };
            Assert.True((await users.CreateAsync(user, seed.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        await Authorize(client, email, seed.Password);
        var id = seed.DayId;
        var child = seed.PresentId;
        var routes = new List<(HttpMethod Method, string Path)>
        {
            (HttpMethod.Get, "/api/admin/users"),
            (HttpMethod.Post, "/api/admin/users"),
            (HttpMethod.Put, $"/api/admin/users/{seed.TeacherId}"),
            (HttpMethod.Post, $"/api/admin/users/{seed.TeacherId}/reset-password"),
            (HttpMethod.Get, "/api/admin/academic-years"),
            (HttpMethod.Put, "/api/admin/academic-years/2026-2027"),
            (HttpMethod.Post, "/api/classes"),
            (HttpMethod.Put, $"/api/admin/classes/{seed.ClassId}"),
            (HttpMethod.Put, $"/api/admin/students/{child}"),
            (HttpMethod.Post, $"/api/admin/students/{child}/enrollments"),
            (HttpMethod.Post, "/api/meal-days"),
            (HttpMethod.Post, $"/api/meal-days/{id}/settle"),
            (HttpMethod.Post, $"/api/meal-days/{id}/amendments/{Guid.NewGuid()}/review"),
            (HttpMethod.Post, "/api/admin/notifications/test"),
            (HttpMethod.Get, "/api/admin/students/import/history")
        };
        if (role != RoleNames.KitchenStaff)
        {
            routes.AddRange([
                (HttpMethod.Get, "/api/kitchen/settlements/search"),
                (HttpMethod.Post, $"/api/kitchen/settlements/{id}/acknowledge"),
                (HttpMethod.Post, "/api/kitchen/ingredients"),
                (HttpMethod.Post, "/api/kitchen/recipes")
            ]);
        }
        if (role != RoleNames.Teacher)
        {
            routes.AddRange([
                (HttpMethod.Post, "/api/students"),
                (HttpMethod.Post, $"/api/admin/students/{child}/parents"),
                (HttpMethod.Post, $"/api/meal-days/{id}/exceptions"),
                (HttpMethod.Post, $"/api/meal-days/{id}/amendments"),
                (HttpMethod.Post, "/api/student-link-requests/bulk-review"),
                (HttpMethod.Post, $"/api/student-link-requests/{Guid.NewGuid()}/review"),
                (HttpMethod.Post, $"/api/student-link-requests/{Guid.NewGuid()}/revoke")
            ]);
        }
        if (role != RoleNames.Parent)
        {
            routes.AddRange([
                (HttpMethod.Post, "/api/parent/absences"),
                (HttpMethod.Post, "/api/parent/link-requests")
            ]);
        }
        foreach (var (method, path) in routes)
        {
            using var request = new HttpRequestMessage(method, path);
            if (method != HttpMethod.Get) request.Content = JsonContent.Create(new { });
            using var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"{role}: {method} {path} must return 403, got {(int)response.StatusCode}.");
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class PhaseOneRbacTests
{
    // Explicit baseline expectations, independent of endpoint authorization metadata.
    // A=Admin T=Teacher K=Kitchen P=Parent. Each row is checked for all 5 identities.
    private const string Matrix = """
        GET /api/kitchen/ingredients AK
        POST /api/kitchen/ingredients AK
        POST /api/kitchen/ingredients/{id:guid}/ingredient-versions AK
        POST /api/kitchen/recipes AK
        POST /api/kitchen/recipes/{id:guid}/versions AK
        GET /api/kitchen/recipes/{id:guid}/versions/{v:int}/nutrition AK
        GET /api/admin/audit-logs A
        GET /api/admin/audit-logs/{auditId} A
        GET /api/admin/audit-logs/entities/{entityType}/{entityId} A
        GET /api/admin/audit-logs/export A
        GET /api/academic-years ATKP
        GET /api/admin/academic-years A
        GET /api/admin/academic-years/{code}/next A
        PUT /api/admin/academic-years/{code} A
        GET /api/classes ATK
        POST /api/classes A
        GET /api/classes/{classId:guid}/students AT
        POST /api/students AT
        POST /api/admin/students/{studentId:guid}/parents AT
        GET /api/parent/students P
        GET /api/parent/absences P
        GET /api/parent/absences/search P
        POST /api/parent/absences P
        POST /api/parent/absences/{id:guid}/cancel P
        POST /api/parent/absences/{id:guid}/replace P
        POST /api/meal-days A
        GET /api/meal-days/workflow ATK
        GET /api/meal-days/{id:guid}/portions ATK
        POST /api/meal-days/{id:guid}/settle A
        GET /api/meal-days AK
        GET /api/meal-days/search AK
        GET /api/meal-days/{id:guid} AK
        GET /api/meal-days/{id:guid}/decisions AT
        GET /api/meal-days/{id:guid}/students/{studentId:guid}/exceptions AT
        POST /api/meal-days/{id:guid}/exceptions AT
        GET /api/meal-days/{dayId:guid}/amendments ATK
        POST /api/meal-days/{dayId:guid}/amendments AT
        GET /api/meal-days/{dayId:guid}/amendments/{requestId:guid} ATK
        POST /api/meal-days/{dayId:guid}/amendments/{requestId:guid}/review A
        GET /api/admin/meal-calendar/{code} A
        GET /api/admin/meal-calendar/{code}/history A
        PUT /api/admin/meal-calendar/{code}/schedule A
        PUT /api/admin/meal-calendar/{code}/days/{date} A
        POST /api/admin/meal-calendar/{code}/preview A
        POST /api/admin/meal-calendar/{code}/generate A
        GET /api/admin/classes A
        PUT /api/admin/classes/{id:guid} A
        GET /api/admin/students A
        GET /api/students/search AT
        PUT /api/admin/students/{id:guid} A
        GET /api/admin/students/{id:guid}/enrollments A
        POST /api/admin/students/{id:guid}/enrollments A
        GET /api/parent/link-requests/classes P
        GET /api/parent/link-requests P
        POST /api/parent/link-requests P
        POST /api/parent/link-requests/{id:guid}/cancel P
        GET /api/student-link-requests AT
        GET /api/student-link-requests/classes AT
        POST /api/student-link-requests/{id:guid}/review AT
        POST /api/student-link-requests/bulk-review AT
        POST /api/student-link-requests/{id:guid}/revoke AT
        POST /api/admin/students/import/preview A
        POST /api/admin/students/import/confirm A
        GET /api/admin/students/import/history A
        GET /api/admin/students/import/history/{id:guid} A
        GET /api/admin/users A
        GET /api/admin/users/{id:guid} A
        POST /api/admin/users A
        PUT /api/admin/users/{id:guid} A
        POST /api/admin/users/{id:guid}/reset-password A
        GET /api/admin/scope-options A
        """;

    [Fact]
    public async Task PhaseOnePolicyMatrixCoversFourRolesAndAnonymous()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        var roles = new Dictionary<char, string> { ['A'] = RoleNames.Admin, ['T'] = RoleNames.Teacher, ['K'] = RoleNames.KitchenStaff, ['P'] = RoleNames.Parent };
        foreach (var line in Matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cells = line.Split(' ');
            var matches = endpoints.Where(e => e.RoutePattern.RawText!.TrimEnd('/') == cells[1] &&
                e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(cells[0])).ToArray();
            Assert.True(matches.Length == 1, $"Missing or duplicate endpoint: {line}");
            var endpoint = matches.Single();
            var policy = await AuthorizationPolicy.CombineAsync(provider, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
            Assert.NotNull(policy);
            foreach (var role in roles)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role.Value)], "Matrix"));
                var allowed = (await authorization.AuthorizeAsync(principal, endpoint, policy)).Succeeded;
                Assert.True(allowed == cells[2].Contains(role.Key), $"{line}, role={role.Value}, allowed={allowed}");
            }
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), endpoint, policy)).Succeeded);
        }
        Assert.All(endpoints.Where(x => x.RoutePattern.RawText!.StartsWith("/api/admin/audit-logs")),
            e => Assert.Equal(new[] { "GET" }, e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.KitchenStaff)]
    [InlineData(RoleNames.Teacher)]
    [InlineData(RoleNames.Parent)]
    [InlineData(null)]
    public async Task KitchenHttpMatrixAndErrorCodes(string? role)
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        if (role is not null)
        {
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(seed.TeacherId.ToString()))!;
            await users.RemoveFromRoleAsync(user, RoleNames.Teacher);
            await users.AddToRoleAsync(user, role);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.TeacherEmail, seed.Password));
        }
        var allowed = role is RoleNames.Admin or RoleNames.KitchenStaff;
        var denied = role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        var create = await client.PostAsJsonAsync("/api/kitchen/ingredients", new { name = "Gạo thử", unit = "g" });
        Assert.Equal(allowed ? HttpStatusCode.Created : denied, create.StatusCode);
        var ingredient = allowed ? (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid() : Guid.NewGuid();
        // The pre-v1.3 payload must fail validation for authorized roles, never bypass authorization for others.
        var legacy = await client.PostAsJsonAsync($"/api/kitchen/ingredients/{ingredient}/ingredient-versions",
            new { validFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), ediblePercent = 100, energyKcal = 100, proteinG = 5 });
        Assert.Equal(allowed ? HttpStatusCode.BadRequest : denied, legacy.StatusCode);
        if (allowed)
        {
            Assert.Equal("application/problem+json", legacy.Content.Headers.ContentType!.MediaType);
            var error = await legacy.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
            var missingFields = error.GetProperty("errors");
            Assert.True(missingFields.TryGetProperty("fatG", out _));
            Assert.True(missingFields.TryGetProperty("carbG", out _));
            Assert.True(missingFields.TryGetProperty("source", out _));
        }
        var version = await client.PostAsJsonAsync($"/api/kitchen/ingredients/{ingredient}/ingredient-versions",
            new { validFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), ediblePercent = 100, energyKcal = 100,
                proteinG = 5, fatG = 1m, carbG = 17.75m, source = "HTTP role matrix composition table" });
        Assert.Equal(allowed ? HttpStatusCode.Created : denied, version.StatusCode);
        Assert.Equal(allowed ? HttpStatusCode.OK : denied, (await client.GetAsync("/api/kitchen/ingredients")).StatusCode);
        var lines = new[] { new { ingredientId = ingredient, quantity = 100, unit = "g" } };
        var recipeResponse = await client.PostAsJsonAsync("/api/kitchen/recipes", new { name = "Cháo", servingBasis = 1, lines });
        Assert.Equal(allowed ? HttpStatusCode.Created : denied, recipeResponse.StatusCode);
        var recipe = allowed ? (await recipeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recipeId").GetGuid() : Guid.NewGuid();
        var next = await client.PostAsJsonAsync($"/api/kitchen/recipes/{recipe}/versions", new { expectedVersion = 1, servingBasis = 1, lines });
        Assert.Equal(allowed ? HttpStatusCode.Created : denied, next.StatusCode);
        var nutrition = await client.GetAsync($"/api/kitchen/recipes/{recipe}/versions/1/nutrition");
        Assert.Equal(allowed ? HttpStatusCode.OK : denied, nutrition.StatusCode);
        if (allowed)
        {
            var perServing = (await nutrition.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("perServing");
            Assert.Equal(100m, perServing.GetProperty("kcal").GetDecimal());
            Assert.Equal(5m, perServing.GetProperty("proteinG").GetDecimal());
            Assert.Equal(1m, perServing.GetProperty("fatG").GetDecimal());
            Assert.Equal(17.8m, perServing.GetProperty("carbG").GetDecimal());
            var stale = await client.PostAsJsonAsync($"/api/kitchen/recipes/{recipe}/versions", new { expectedVersion = 1, servingBasis = 1, lines });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("application/problem+json", stale.Content.Headers.ContentType!.MediaType);
            Assert.Equal("RECIPE_VERSION_CONFLICT", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            // Existing ingredient with no nutrient version must produce a business 422.
            var missing = await client.PostAsJsonAsync("/api/kitchen/ingredients", new { name = "Chưa có dữ liệu", unit = "g" });
            var missingId = (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var invalid = await client.PostAsJsonAsync("/api/kitchen/recipes", new { name = "Thiếu", servingBasis = 1,
                lines = new[] { new { ingredientId = missingId, quantity = 100, unit = "g" } } });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
            Assert.Equal("NUTRIENT_DATA_MISSING", (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
    }
}

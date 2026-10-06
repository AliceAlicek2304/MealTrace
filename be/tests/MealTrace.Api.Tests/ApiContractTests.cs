using MealTrace.Application.Features.Workflow;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public void DtoPropertiesCannotExposeDomainEntitiesOrUntypedObjects()
    {
        var contracts = typeof(WorkflowService).Assembly.GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("MealTrace.Application.Dtos.") == true);
        foreach (var contract in contracts)
            foreach (var property in contract.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Check(property.PropertyType, $"{contract.Name}.{property.Name}");

        static void Check(Type type, string path)
        {
            Assert.NotEqual(typeof(object), type);
            Assert.NotEqual(typeof(Student).Assembly, type.Assembly);
            if (type.IsArray) Check(type.GetElementType()!, path);
            foreach (var argument in type.GetGenericArguments())
                if (!argument.IsGenericParameter) Check(argument, path);
        }
    }

    [Fact]
    public void SwaggerDescribesEverySuccessfulApiResponse()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        var swagger = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        foreach (var path in swagger.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var successes = operation.Value.Responses.Where(response => response.Key.StartsWith('2')).ToArray();
                Assert.NotEmpty(successes);
                foreach (var response in successes)
                {
                    if (response.Key == "204") { Assert.Empty(response.Value.Content); continue; }
                    Assert.True(response.Value.Content.ContainsKey("application/json"), $"Missing schema: {operation.Key} {path.Key} ({response.Key})");
                    var schema = response.Value.Content["application/json"].Schema;
                    Assert.True(schema.Reference is not null || schema.Items?.Reference is not null,
                        $"Untyped schema: {operation.Key} {path.Key}");
                }
            }
        }
        Assert.Contains("SettlementSnapshot", swagger.Components.Schemas.Keys);
        Assert.DoesNotContain("Student", swagger.Components.Schemas.Keys);
        Assert.DoesNotContain("ApplicationUser", swagger.Components.Schemas.Keys);
        var snapshot = swagger.Components.Schemas["SettlementSnapshot"];
        Assert.NotNull(snapshot.Properties["decisions"].Items.Reference);
    }

    [Theory]
    [InlineData(RoleNames.Admin, HttpStatusCode.Created)]
    [InlineData(RoleNames.KitchenStaff, HttpStatusCode.Created)]
    [InlineData(RoleNames.Teacher, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Parent, HttpStatusCode.Forbidden)]
    public async Task KitchenUsesSchoolRolesAndRejectsOtherActors(string role, HttpStatusCode expected)
    {
        using var factory = new AuthTestFactory();
        var seeded = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        var body = new { name = "Contract test ingredient", unit = "g" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/kitchen/ingredients", body)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var teacher = (await users.FindByIdAsync(seeded.TeacherId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(teacher, RoleNames.Teacher)).Succeeded);
            Assert.True((await users.AddToRoleAsync(teacher, role)).Succeeded);
        }
        var token = await LoginAsync(client, seeded.TeacherEmail, seeded.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var result = await client.PostAsJsonAsync("/api/kitchen/ingredients", body);
        Assert.Equal(expected, result.StatusCode);
    }
}

using Microsoft.AspNetCore.Identity;
using MealTrace.Application.Features.Workflow;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Identity;
using System.Reflection;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Features.Kitchen;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void LayersCannotReferenceOutwardProjectsOrProviderFromApplication()
    {
        CheckProjectReferences(typeof(Student).Assembly, []);
        CheckProjectReferences(typeof(WorkflowService).Assembly, ["MealTrace.Domain"]);
        CheckProjectReferences(typeof(MealTraceDbContext).Assembly, ["MealTrace.Domain", "MealTrace.Application"]);
        var domainDependencies = typeof(Student).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        Assert.DoesNotContain(domainDependencies, name => name.StartsWith("Microsoft.") || name.StartsWith("Npgsql"));
        var applicationDependencies = typeof(WorkflowService).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        Assert.DoesNotContain(applicationDependencies, name => name.StartsWith("Microsoft.AspNetCore") || name.StartsWith("Microsoft.EntityFrameworkCore") || name.StartsWith("Npgsql"));
    }

    [Fact]
    public void RepositoryPortsCannotExposeQueryProvidersOrUntypedData()
    {
        var ports = typeof(IWorkflowRepository).Assembly.GetExportedTypes()
            .Where(type => type.IsInterface && type.Namespace == "MealTrace.Application.Abstractions.Repositories").ToArray();
        Assert.NotEmpty(ports);
        foreach (var port in ports)
        {
            Assert.Empty(port.GetProperties());
            foreach (var method in port.GetMethods())
            {
                Check(method.ReturnType);
                foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType);
            }
        }
        static void Check(Type type)
        {
            Assert.NotEqual(typeof(object), type);
            Assert.False(typeof(IQueryable).IsAssignableFrom(type), $"Queryable port: {type}");
            Assert.False(typeof(Delegate).IsAssignableFrom(type), $"Query callback port: {type}");
            Assert.False(type.Namespace?.StartsWith("System.Linq.Expressions") == true);
            foreach (var argument in type.GetGenericArguments()) Check(argument);
            if (type.IsArray) Check(type.GetElementType()!);
        }
    }

    [Fact]
    public void ServiceOperationsReceiveBusinessInputRatherThanRuntimeDependencies()
    {
        var services = typeof(WorkflowService).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && type.Namespace?.StartsWith("MealTrace.Application.Features.") == true && type.Name.EndsWith("Service")).ToArray();
        Assert.NotEmpty(services);
        foreach (var service in services)
        {
            Assert.NotEmpty(service.GetConstructors());
            foreach (var method in service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var parameter in method.GetParameters())
                {
                    Assert.False(parameter.ParameterType.Namespace?.StartsWith("MealTrace.Application.Abstractions") == true,
                        $"Dependency passed per call: {service.Name}.{method.Name}");
                    Assert.NotEqual(typeof(System.Security.Claims.ClaimsPrincipal), parameter.ParameterType);
                    Assert.NotEqual(typeof(TimeProvider), parameter.ParameterType);
                }
        }
    }

    [Fact]
    public void BusinessResultKeepsPayloadTypeAndHasNoHttpMetadata()
    {
        var properties = typeof(Result<string>).GetProperties();
        Assert.Equal(typeof(string), properties.Single(property => property.Name == "Value").PropertyType);
        Assert.DoesNotContain(properties, property => property.Name is "Status" or "StatusCode" or "Location");
        var success = Result.Success("value");
        Assert.True(success.IsSuccess);
        Assert.Equal("value", success.Value);
        Assert.Null(success.Error);
        Result<string> failure = Result.Conflict("Concurrent change");
        Assert.False(failure.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, failure.Error!.Kind);
        Assert.Null(failure.Value);
    }

    private static void CheckProjectReferences(Assembly assembly, string[] allowed) =>
        Assert.All(assembly.GetReferencedAssemblies().Where(name => name.Name!.StartsWith("MealTrace.")),
            name => Assert.Contains(name.Name!, allowed));

    [Fact]
    public void MigrationHistoryPreservesOriginalMigrationsAndMatchesCurrentModel()
    {
        using var db = new MealTraceDbContext(new DbContextOptionsBuilder<MealTraceDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.True(migrations.Length >= 12);
        Assert.Equal("20260930031430_InitialCreate", migrations[0]);
        Assert.Equal("20261001122617_PortionAmendmentWorkflow", migrations[11]);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(typeof(MealTraceDbContext).Assembly, db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>().Assembly);
    }

    [Fact]
    public async Task IdentityStoreReturnsConcurrencyFailureForStaleAccountUpdate()
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstManager = firstScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var secondManager = secondScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var first = await firstManager.FindByEmailAsync(seed.TeacherEmail);
        var stale = await secondManager.FindByEmailAsync(seed.TeacherEmail);
        Assert.NotNull(first);
        Assert.NotNull(stale);
        first.FullName = "First update";
        Assert.True((await firstManager.UpdateAsync(first)).Succeeded);
        stale.FullName = "Stale update";
        var result = await secondManager.UpdateAsync(stale);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
        using var check = factory.Services.CreateScope();
        var persisted = await check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(seed.TeacherEmail);
        Assert.Equal("First update", persisted!.FullName);
    }

    [Fact]
    public async Task IdentityAndBusinessChangesRollbackTogetherThroughPorts()
    {
        using var factory = new AuthTestFactory();
        await factory.SeedUsersAsync();
        var id = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var data = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var accounts = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await using var transaction = await data.BeginTransactionAsync();
            var created = await accounts.CreateAsync(new IdentityAccount
            {
                Id = id,
                FullName = "Rollback account",
                UserName = "rollback@test.local",
                Email = "rollback@test.local"
            }, "RollbackTest!123");
            Assert.True(created.Succeeded);
            repository.AddAccountPasswordResetAudit(new AccountPasswordResetAudit { UserId = id, PerformedByUserId = id, Reason = "Rollback test" });
            await data.SaveChangesAsync();
            // No Commit: disposal must roll back both writes, including the Identity store's SaveChanges.
        }
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.False(await db.Users.AnyAsync(user => user.Id == id));
        Assert.False(await db.AccountPasswordResetAudits.AnyAsync(audit => audit.UserId == id));
    }

    [Fact]
    public void SwaggerResolvesMovedRequestContractsAndExistingRoutes()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Contains("/api/auth/login", document.Paths.Keys);
        Assert.Contains("/api/admin/students/{id}/enrollments", document.Paths.Keys);
        Assert.Contains("/api/admin/meal-calendar/{code}/generate", document.Paths.Keys);
        Assert.Contains("/api/meal-days/{dayId}/amendments/{requestId}/review", document.Paths.Keys);
        Assert.Contains("/api/kitchen/recipes/{id}/versions/{v}/nutrition", document.Paths.Keys);
        Assert.NotNull(document.Paths["/api/auth/login"].Operations[Microsoft.OpenApi.Models.OperationType.Post].RequestBody);
        var loginSchema = document.Paths["/api/auth/login"].Operations[Microsoft.OpenApi.Models.OperationType.Post]
            .Responses["200"].Content["application/json"].Schema;
        Assert.Equal("LoginResponse", loginSchema.Reference.Id);
        Assert.Contains("accessToken", document.Components.Schemas["LoginResponse"].Properties.Keys);
        Assert.DoesNotContain("passwordHash", document.Components.Schemas["AccountView"].Properties.Keys);
        Assert.DoesNotContain("securityStamp", document.Components.Schemas["AccountView"].Properties.Keys);
    }

    [PostgresFact]
    public async Task MovedKitchenServicesResolveAndKeepIngredientRecipeNutritionFlow()
    {
        using var factory = new AuthTestFactory(postgres: true);
        await factory.SeedUsersAsync();
        using var scope = factory.Services.CreateScope();
        var ingredients = scope.ServiceProvider.GetRequiredService<IIngredientService>();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipeService>();
        var nutrition = scope.ServiceProvider.GetRequiredService<INutritionService>();
        var now = DateTimeOffset.UtcNow;
        var ingredient = await ingredients.CreateAsync(new("Test Rice", "g"), default);
        await ingredients.AddVersionAsync(ingredient.Id, new(now.AddDays(-1), 100m, 100m, 10m, 20000m), default);
        var result = await ingredients.ListAsync("rIcE", 1, 25, default);
        Assert.Equal(ingredient.Id, Assert.Single(result.Items).Id);
        var recipe = await recipes.CreateAsync(new("Test Rice Dish", 2, [new(ingredient.Id, 200m, "g")]), default);
        var calculated = await nutrition.CalculateAsync(recipe.RecipeId, recipe.Version, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, default);
        Assert.Equal(100m, calculated.PerServing.Kcal);
        Assert.Equal(10m, calculated.PerServing.ProteinG);
    }
}

using System.Net;
using System.Net.Http.Json;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class KitchenVersionAuditTests
{
    private const string Root = "/api/admin/audit-logs";

    [Fact]
    public Task IngredientAndRecipeAuditUsesPersistedVersionsOnSqlite() => VerifyVersionAudit(postgres: false);

    [PostgresFact]
    public Task IngredientAndRecipeAuditUsesPersistedVersionsOnPostgres() => VerifyVersionAudit(postgres: true);

    private static async Task VerifyVersionAudit(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres: postgres);
        using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync();
        var initial = new DateTimeOffset(2026, 11, 18, 1, 0, 0, TimeSpan.Zero);
        var corrected = initial.AddDays(1);
        var ingredient = new Ingredient { Name = "Fixture ingredient", Unit = "g" };
        var recipe = new Recipe { Name = "Fixture recipe" };
        var ingredientV1 = new IngredientVersion
        {
            Ingredient = ingredient, Version = 1, RecordedAt = initial, EffectiveFrom = initial,
            EnergyKcalPer100G = 100m
        };
        var recipeV1 = new RecipeVersion { Recipe = recipe, Version = 1, RecordedAt = initial };

        const string privateRef = "https://example.test/receipt?signature=PRIVATE_NUTRIENT_REFERENCE";
        const string reason = "=SUM(1,2), \"adjusted\"\nNew serving basis";
        Guid actorId;
        IngredientVersion ingredientV2;
        RecipeVersion recipeV2;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            actorId = await db.Set<ApplicationUser>().Where(user => user.Email == seed.AdminEmail)
                .Select(user => user.Id).SingleAsync();
            ingredientV2 = new IngredientVersion
            {
                Ingredient = ingredient, Version = 2, RecordedAt = corrected, EffectiveFrom = initial,
                EnergyKcalPer100G = 110m, FatGPer100G = 2m, Source = "Documented nutrient source",
                SourceRef = privateRef, RecordedByUserId = actorId, SupersedesId = ingredientV1.Id
            };
            recipeV2 = new RecipeVersion
            {
                Recipe = recipe, Version = 2, RecordedAt = corrected, RecordedByUserId = actorId,
                ServingBasis = 20, RoundingKcalDecimals = 0, RoundingMacroDecimals = 1, Reason = reason
            };
            db.AddRange(ingredientV1, ingredientV2, recipeV1, recipeV2);
            await db.SaveChangesAsync();
        }

        await Authorize(client, seed.AdminEmail, seed.Password);
        var ingredients = await client.GetFromJsonAsync<AuditPage>($"{Root}/entities/Ingredient/{ingredient.Id}");
        Assert.Equal(new[] { $"ingredient-version:{ingredientV2.Id:D}", $"ingredient-version:{ingredientV1.Id:D}" },
            ingredients!.Items.Select(entry => entry.Id));
        var nutrient = ingredients.Items[0];
        Assert.Equal(new AuditActor(actorId, null), nutrient.Actor);
        Assert.Equal(corrected, nutrient.OccurredAt);
        Assert.Equal(ingredientV1.Id, nutrient.After!.Value.GetProperty("supersedesId").GetGuid());
        Assert.Equal(110m, nutrient.After.Value.GetProperty("energyKcalPer100G").GetDecimal());
        Assert.True(nutrient.After.Value.GetProperty("hasSourceReference").GetBoolean());
        Assert.False(nutrient.After.Value.TryGetProperty("sourceRef", out _));
        Assert.Equal(100m, nutrient.Before!.Value.GetProperty("energyKcalPer100G").GetDecimal());
        Assert.True(nutrient.Before.Value.GetProperty("actorMissing").GetBoolean());
        Assert.Null(ingredients.Items[1].Actor);
        Assert.Null(ingredients.Items[1].Before);

        var recipeDetail = await client.GetFromJsonAsync<AuditEntry>($"{Root}/recipe-version:{recipeV2.Id:D}");
        Assert.Equal(recipe.Id.ToString("D"), recipeDetail!.EntityId);
        Assert.Equal(reason, recipeDetail.Reason);
        Assert.Equal(recipeV1.Id, recipeDetail.Before!.Value.GetProperty("id").GetGuid());
        Assert.Equal(20, recipeDetail.After!.Value.GetProperty("servingBasis").GetInt32());
        Assert.Equal(1, recipeDetail.After.Value.GetProperty("roundingMacroDecimals").GetInt32());

        var actors = await client.GetFromJsonAsync<AuditPage>($"{Root}?actorId={actorId:D}&action=kitchen.ingredient.version_recorded");
        Assert.Equal(nutrient.Id, Assert.Single(actors!.Items).Id);
        var export = $"{Root}/export?from=2026-11-18T00:00:00Z&to=2026-11-20T00:00:00Z";
        var csv = await client.GetStringAsync(export);
        Assert.Contains($"recipe-version:{recipeV2.Id:D}", csv);
        Assert.Contains("\"'=SUM(1,2), \"\"adjusted\"\"\nNew serving basis\"", csv);
        Assert.Contains("Documented nutrient source", csv);
        Assert.DoesNotContain("PRIVATE_NUTRIENT_REFERENCE", csv);

        await Authorize(client, seed.TeacherEmail, seed.Password);
        using var refused = await client.GetAsync(export);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}

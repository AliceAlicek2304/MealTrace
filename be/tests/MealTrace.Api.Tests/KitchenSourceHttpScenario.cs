using MealTrace.Api.Tests;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit.Abstractions;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MealTrace.Api.Tests;

/// <summary>
/// The HTTP steps shared by the SQLite and PostgreSQL tests: two real source records of one ingredient, a published menu
/// that uses it, corrections based on a prior version, and checks that earlier history is never rewritten. Everything
/// goes through the public routes as an Admin (who may use the Kitchen routes too).
/// </summary>
internal static class KitchenSourceHttpScenario
{
    internal sealed record Source(Guid EntryId, Guid VersionId, Guid SupplierVersionId, DateOnly Date, decimal Quantity);

    /// <summary>The state of the world right after publication; later reads are compared with it.</summary>
    internal sealed record Seeded(
        Guid IngredientId, Source A, Source B, Guid MenuId,
        JsonElement HistoryA, JsonElement HistoryB, JsonElement MenuHistory, JsonElement MenuNutrition);

    public static async Task<JsonElement> Body(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<Seeded> SeedAsync(HttpClient client, Func<Task<Guid>> createMealDay)
    {
    var ingredient = await Body(await client.PostAsJsonAsync("/api/kitchen/ingredients", new { name = "Rice", unit = "g" }), HttpStatusCode.Created);
    var ingredientId = ingredient.GetProperty("id").GetGuid();
    await Body(await client.PostAsJsonAsync($"/api/kitchen/ingredients/{ingredientId}/ingredient-versions", new
            {
        validFrom = DateTimeOffset.UtcNow.AddDays(-30), ediblePercent = 100, energyKcal = 360, proteinG = 7, fatG = 1,
carbG = 80, source = "Test reference table"
        }), HttpStatusCode.Created);

    var date = DateOnly.FromDateTime(DateTime.UtcNow);
    var a = await CreateSource(client, ingredientId, "Farm A", date, 600m, "DN-A", "LOT-A");
    var b = await CreateSource(client, ingredientId, "Farm B", date, 400m, "DN-B", "LOT-B");

    var recipe = await Body(await client.PostAsJsonAsync("/api/kitchen/recipes", new
            {
        name = "Plain rice", servingBasis = 1, lines = new[] { new { ingredientId, quantity = 100, unit = "g" } }
                }), HttpStatusCode.Created);

    var mealDayId = await createMealDay();
    var menu = await Body(await client.PostAsJsonAsync("/api/kitchen/menus", new
            {
        mealDayId, reason = "first draft",
dishes = new[] { new { recipeVersionId = recipe.GetProperty("versionId").GetGuid(), plannedPortions = 20 } }
        }), HttpStatusCode.Created);
    var menuId = menu.GetProperty("id").GetGuid();
    await Body(await client.PostAsJsonAsync($"/api/kitchen/menus/{menuId}/submit", new { expectedVersion = 1 }), HttpStatusCode.OK);
    await Body(await client.PostAsJsonAsync($"/api/admin/menus/{menuId}/review", new { expectedVersion = 1, decision = "approve" }), HttpStatusCode.OK);
    await Body(await client.PostAsJsonAsync($"/api/admin/menus/{menuId}/publish", new { expectedVersion = 1 }), HttpStatusCode.OK);

            return new Seeded(
    ingredientId, a, b, menuId,
    await History(client, a.EntryId), await History(client, b.EntryId),
    await Body(await client.GetAsync($"/api/kitchen/menus/{menuId}/history"), HttpStatusCode.OK),
    await Body(await client.GetAsync($"/api/kitchen/menus/{menuId}/nutrition"), HttpStatusCode.OK));
        }

    private static async Task<Source> CreateSource(
        HttpClient client, Guid ingredientId, string supplierName, DateOnly date, decimal quantity, string delivery, string lot)
    {
    var supplier = await Body(await client.PostAsJsonAsync("/api/kitchen/suppliers",
    new { name = supplierName, contact = "0900 000 000", reference = "SUP-" + supplierName[^1] }), HttpStatusCode.Created);
    var supplierVersionId = supplier.GetProperty("versionId").GetGuid();

    var entry = await Body(await client.PostAsJsonAsync("/api/kitchen/ingredient-source-entries", new
            {
        ingredientId, supplierVersionId, referenceDate = date, quantity, unit = "g", deliveryRef = delivery, lotRef = lot
                }), HttpStatusCode.Created);
    Assert.Equal(1, entry.GetProperty("version").GetInt32());
            return new Source(entry.GetProperty("id").GetGuid(), entry.GetProperty("versionId").GetGuid(), supplierVersionId, date, quantity);
        }

    /// <summary>A correction carries the full new values and the version it was based on.</summary>
    public static Task<HttpResponseMessage> Correct(HttpClient client, Source source, int expectedVersion, decimal quantity, string reason) =>
client.PostAsJsonAsync($"/api/kitchen/ingredient-source-entries/{source.EntryId}/versions", new
        {
expectedVersion, reason, supplierVersionId = source.SupplierVersionId, referenceDate = source.Date, quantity,
unit = "g", deliveryRef = "DN-FIX", lotRef = "LOT-FIX"
        });

    public static async Task<JsonElement> History(HttpClient client, Guid entryId) =>
await Body(await client.GetAsync($"/api/kitchen/ingredient-source-entries/{entryId}/history"), HttpStatusCode.OK);

    /// <summary>
    /// After exactly one correction of record A has been accepted: its first version, record B, and the menu history
    /// and frozen nutrition are byte-for-byte what they were; the refused correction left no trace.
    /// </summary>
    public static async Task AssertHistoryIntactAsync(
        HttpClient client, Seeded seeded, decimal winnerQuantity, string winnerReason, string loserReason)
    {
    var historyA = await History(client, seeded.A.EntryId);
    var items = historyA.GetProperty("items").EnumerateArray().ToArray();
    Assert.Equal(2, items.Length);
    Assert.Equal(seeded.HistoryA.GetProperty("items")[0].GetRawText(), items[0].GetRawText());

    Assert.Equal(2, items[1].GetProperty("version").GetInt32());
    Assert.Equal(winnerQuantity, items[1].GetProperty("quantity").GetDecimal());
    Assert.Equal(winnerReason, items[1].GetProperty("reason").GetString());
    Assert.Equal(seeded.A.VersionId, items[1].GetProperty("priorVersionId").GetGuid());
    Assert.True(items.All(i => i.GetProperty("reason").GetString() != loserReason), "The refused correction must not appear in history.");

    Assert.Equal(seeded.HistoryB.GetRawText(), (await History(client, seeded.B.EntryId)).GetRawText());

    var menuHistory = await Body(await client.GetAsync($"/api/kitchen/menus/{seeded.MenuId}/history"), HttpStatusCode.OK);
    Assert.Equal(seeded.MenuHistory.GetRawText(), menuHistory.GetRawText());
    var nutrition = await Body(await client.GetAsync($"/api/kitchen/menus/{seeded.MenuId}/nutrition"), HttpStatusCode.OK);
    Assert.Equal(seeded.MenuNutrition.GetRawText(), nutrition.GetRawText());

            // The listing shows the latest version of each record: the corrected A plus the untouched B.
    var list = await Body(await client.GetAsync($"/api/kitchen/ingredient-source-entries?ingredientId={seeded.IngredientId}"), HttpStatusCode.OK);
    Assert.Equal(2, list.GetProperty("total").GetInt32());
    Assert.Equal(winnerQuantity + seeded.B.Quantity, list.GetProperty("items").EnumerateArray().Sum(i => i.GetProperty("quantity").GetDecimal()));
        }

    /// <summary>The refused correction is a 409 problem that names the version that is current.</summary>
    public static async Task AssertConflictAsync(HttpResponseMessage response, int currentVersion)
    {
    var problem = await Body(response, HttpStatusCode.Conflict);
    Assert.Equal("SOURCE_ENTRY_VERSION_CONFLICT", problem.GetProperty("code").GetString());
    Assert.Equal(currentVersion, problem.GetProperty("currentVersion").GetInt32());
        }

    /// <summary>One accepted correction, one refused as stale, history intact, and the chain continues from the accepted version.</summary>
    public static async Task RunSequentialAsync(HttpClient client, Seeded seeded)
    {
    var accepted = await Body(await Correct(client, seeded.A, 1, 580m, "weighed again"), HttpStatusCode.Created);
    Assert.Equal(2, accepted.GetProperty("version").GetInt32());
    Assert.Equal(seeded.A.VersionId, accepted.GetProperty("priorVersionId").GetGuid());
    Assert.Equal(580m, accepted.GetProperty("quantity").GetDecimal());

            // Still based on version 1, so it is stale now.
    await AssertConflictAsync(await Correct(client, seeded.A, 1, 570m, "late correction"), 2);

    await AssertHistoryIntactAsync(client, seeded, 580m, "weighed again", "late correction");

            // A correction based on the accepted version is fine and links to it.
    var next = await Body(await Correct(client, seeded.A, 2, 575m, "final check"), HttpStatusCode.Created);
    Assert.Equal(3, next.GetProperty("version").GetInt32());
    Assert.Equal(accepted.GetProperty("versionId").GetGuid(), next.GetProperty("priorVersionId").GetGuid());

    var items = (await History(client, seeded.A.EntryId)).GetProperty("items").EnumerateArray().ToArray();
    Assert.Equal(3, items.Length);
    Assert.Equal(seeded.HistoryA.GetProperty("items")[0].GetRawText(), items[0].GetRawText());
    Assert.Equal(accepted.GetProperty("versionId").GetGuid(), items[1].GetProperty("versionId").GetGuid());
        }
}

using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.KitchenSourceHttpScenario;

namespace MealTrace.Api.Tests;

/// <summary>
/// Two real source records of one ingredient (600 g and 400 g from different suppliers) and a published menu. A
/// correction names the version it is based on: one correction is accepted, a second one based on the same version is a
/// 409, and neither the source history nor the menu history nor the frozen menu nutrition is rewritten.
/// </summary>
public sealed class KitchenSourceHttpTests
{
    [Fact]
    public async Task AStaleSourceCorrectionConflictsAndLeavesSourceAndMenuHistoryIntact() => await Sequential(postgres: false);

    [PostgresFact]
    public async Task AStaleSourceCorrectionConflictsAndLeavesSourceAndMenuHistoryIntactOnPostgreSql() => await Sequential(postgres: true);

    [PostgresFact]
    public async Task SimultaneousSourceCorrectionsFromTheSamePriorVersionCommitOnlyOne()
    {
        using var factory = new AuthTestFactory(postgres: true);
        using var client = factory.CreateClient();
        var seeded = await Seed(factory, client);

        var responses = await Task.WhenAll(
Correct(client, seeded.A, 1, 580m, "first racer"),
Correct(client, seeded.A, 1, 570m, "second racer"));

        var created = responses.Where(x => x.StatusCode == HttpStatusCode.Created).ToArray();
        var refused = responses.Where(x => x.StatusCode == HttpStatusCode.Conflict).ToArray();
        Assert.Single(created);
        Assert.Single(refused);
        await AssertConflictAsync(refused[0], 2);

        var winner = await Body(created[0], HttpStatusCode.Created);
        var winnerQuantity = winner.GetProperty("quantity").GetDecimal();
        var(winnerReason, loserReason) = winnerQuantity == 580m ? ("first racer", "second racer") : ("second racer", "first racer");
        await AssertHistoryIntactAsync(client, seeded, winnerQuantity, winnerReason, loserReason);

        // The unique indexes, not just the service check, left exactly one version 2 built on version 1.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var versions = await db.IngredientSourceEntryVersions.Where(v => v.SourceEntryId == seeded.A.EntryId).OrderBy(v => v.Version).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(seeded.A.VersionId, versions[1].PriorVersionId);
        Assert.Equal(1, await db.IngredientSourceEntryVersions.CountAsync(v => v.PriorVersionId == seeded.A.VersionId));
        Assert.All(versions, v => Assert.NotNull(v.RecordedByUserId));
    }

    private static async Task Sequential(bool postgres)
    {
            using var factory = new AuthTestFactory(postgres: postgres);
            using var client = factory.CreateClient();
    var seeded = await Seed(factory, client);
    await RunSequentialAsync(client, seeded);
        }

    private static async Task<Seeded> Seed(AuthTestFactory factory, HttpClient client)
    {
    var seed = await factory.SeedUsersAsync();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
            return await SeedAsync(client, async () =>
            {
                    using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var day = new MealDay
                    {
            Date = MealTrace.Domain.Time.SchoolTime.Today(TimeProvider.System.GetUtcNow()), SchoolYear = "2026-2027",
MealType = "Lunch", CutoffAt = DateTimeOffset.UtcNow
            }
        ;
        db.MealDays.Add(day);
        await db.SaveChangesAsync();
                    return day.Id;
                });
        }
}

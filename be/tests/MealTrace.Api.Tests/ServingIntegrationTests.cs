using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Workers;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.KitchenSettlementTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class ServingIntegrationTests
{
    private static DateOnly FutureSchoolDay => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(10);
    private static DateTimeOffset At(DateOnly day, int hour, int minute) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(7)).ToUniversalTime();

    private sealed record ServingHarness(
        AuthTestFactory Factory,
        HttpClient Client,
        Scenario Seed,
        string KitchenEmail,
        Guid SettlementId,
        Guid ForeignSettlementId,
        string SnapshotToken,
        Guid IngredientId,
        Guid IngredientVersionId,
        Guid RecipeVersionId,
        Guid AltRecipeVersionId) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    private static async Task<ServingHarness> CreateSettledHarnessAsync(bool postgres)
    {
        var clock = new TestClock();
        var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);

        Guid ingredientId;
        Guid ingredientVersionId;
        Guid recipeVersionId;
        Guid altRecipeVersionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var ingredient = new Ingredient { Name = "Thịt nạc thăn", Unit = "g" };
            var ingVersion = new IngredientVersion
            {
                Ingredient = ingredient,
                Version = 1,
                EnergyKcalPer100G = 143m,
                ProteinGPer100G = 21m,
                FatGPer100G = 6m,
                CarbGPer100G = 0m,
                PricePerKg = 140000m,
                EdibleFraction = 1m,
                EffectiveFrom = clock.GetUtcNow().AddDays(-1).ToUniversalTime(),
                RecordedAt = clock.GetUtcNow().ToUniversalTime(),
                Source = "VDD 2007"
            };
            var recipe = new Recipe { Name = "Thịt kho tàu" };
            var recipeVer = new RecipeVersion
            {
                Recipe = recipe,
                Version = 1,
                RecordedAt = clock.GetUtcNow().ToUniversalTime(),
                ServingBasis = 1,
                RoundingKcalDecimals = 0,
                RoundingMacroDecimals = 1
            };
            var altRecipe = new Recipe { Name = "Thịt rim mặn ngọt" };
            var altRecipeVer = new RecipeVersion
            {
                Recipe = altRecipe,
                Version = 1,
                RecordedAt = clock.GetUtcNow().ToUniversalTime(),
                ServingBasis = 1,
                RoundingKcalDecimals = 0,
                RoundingMacroDecimals = 1
            };

            db.Ingredients.Add(ingredient);
            db.IngredientVersions.Add(ingVersion);
            db.Recipes.AddRange(recipe, altRecipe);
            db.RecipeVersions.AddRange(recipeVer, altRecipeVer);
            await db.SaveChangesAsync();

            ingredientId = ingredient.Id;
            ingredientVersionId = ingVersion.Id;
            recipeVersionId = recipeVer.Id;
            altRecipeVersionId = altRecipeVer.Id;
        }

        await Authorize(client, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff);
        (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();

        var kitchen = await CreateKitchenUser(factory, seed);
        await Authorize(client, kitchen.Email!, seed.Password);

        var diff = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        var snapshotToken = diff.GetProperty("snapshotToken").GetString()!;

        var ack = await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", new
        {
            snapshotToken,
            note = "Bếp xác nhận đủ suất để nấu"
        });
        Assert.Equal(HttpStatusCode.Created, ack.StatusCode);

        Guid settlementId;
        Guid foreignSettlementId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            settlementId = await db.PortionSettlements
                .Where(s => s.MealDayId == seed.DayId && s.ClassId == seed.ClassId)
                .OrderByDescending(s => s.Version)
                .Select(s => s.Id)
                .FirstAsync();
            foreignSettlementId = await db.PortionSettlements
                .Where(s => s.MealDayId == seed.DayId && s.ClassId == seed.ForeignClassId)
                .OrderByDescending(s => s.Version)
                .Select(s => s.Id)
                .FirstAsync();
        }

        return new ServingHarness(
            factory,
            client,
            seed,
            kitchen.Email!,
            settlementId,
            foreignSettlementId,
            snapshotToken,
            ingredientId,
            ingredientVersionId,
            recipeVersionId,
            altRecipeVersionId);
    }

    private static CreateServingRequest BuildSplitServingRequest(
        ServingHarness h,
        Guid clientRequestId,
        Guid source1,
        Guid source2,
        Guid? classId = null,
        Guid? settlementId = null,
        string? snapshotToken = null,
        Guid? recipeVersionId = null,
        int servedPortions = 1) =>
        new(
            ClientRequestId: clientRequestId,
            MealDayId: h.Seed.DayId,
            ClassId: classId ?? h.Seed.ClassId,
            MenuVersionId: Guid.NewGuid(),
            SettlementId: settlementId ?? h.SettlementId,
            SnapshotToken: snapshotToken ?? h.SnapshotToken,
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeVersionId ?? h.RecipeVersionId,
                    ActualRecipeVersionId: recipeVersionId ?? h.RecipeVersionId,
                    ServedPortions: servedPortions,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: h.IngredientId,
                            NutrientVersionId: h.IngredientVersionId,
                            Quantity: 1000m,
                            Unit: "g",
                            WeightBasis: "gross",
                            Allocations:
                            [
                                new SourceAllocationInput(source1, 600m, "g"),
                                new SourceAllocationInput(source2, 400m, "g")
                            ],
                            SourceGap: null)
                    ])
            ]);

    private static async Task RunWithReadDbAsync(ServingHarness h, bool postgres, Func<MealTraceDbContext, Task> assertAsync)
    {
        using var scope = h.Factory.Services.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        if (!postgres)
        {
            await assertAsync(scopedDb);
            return;
        }

        var connectionString = scopedDb.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Missing PostgreSQL connection string.");
        var options = new DbContextOptionsBuilder<MealTraceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var freshDb = new MealTraceDbContext(options);
        await assertAsync(freshDb);
    }

    // ---- Scenario 1: Split allocations 600g + 400g survive scope/context restart ----

    [Fact]
    public Task SplitAllocations_600And400_PersistAcrossScopeAndNewClient_Sqlite() =>
        RunSplitAllocationsAsync(postgres: false);

    [PostgresFact]
    public Task SplitAllocations_600And400_PersistAcrossScopeAndNewClient_Postgres() =>
        RunSplitAllocationsAsync(postgres: true);

    private static async Task RunSplitAllocationsAsync(bool postgres)
    {
        using var h = await CreateSettledHarnessAsync(postgres);
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();
        var request = BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2);

        var postRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings", request);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var created = await postRes.Content.ReadFromJsonAsync<ServingResponse>();
        Assert.NotNull(created);
        Assert.Equal(1, created.Version);

        await RunWithReadDbAsync(h, postgres, async db =>
        {
            var serving = await db.Servings.AsNoTracking()
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Dishes)
                        .ThenInclude(d => d.IngredientUses)
                            .ThenInclude(u => u.Allocations)
                .SingleAsync(s => s.Id == created.Id);

            var version = Assert.Single(serving.Versions);
            var dish = Assert.Single(version.Dishes);
            var ing = Assert.Single(dish.IngredientUses);
            Assert.Equal(1000m, ing.Quantity);
            Assert.Equal(2, ing.Allocations.Count);
            Assert.Contains(ing.Allocations, a => a.SourceEntryVersionId == source1 && a.Quantity == 600m);
            Assert.Contains(ing.Allocations, a => a.SourceEntryVersionId == source2 && a.Quantity == 400m);
        });

        using var freshClient = h.Factory.CreateClient();
        await Authorize(freshClient, h.KitchenEmail, h.Seed.Password);
        var detail = await freshClient.GetFromJsonAsync<ServingDetailResponse>($"/api/kitchen/servings/{created.Id}");
        Assert.NotNull(detail);
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(1, detail.Version);
        var ingDetail = Assert.Single(Assert.Single(detail.Dishes).Ingredients);
        Assert.Equal(1000m, ingDetail.Quantity);
        Assert.Equal(2, ingDetail.Allocations.Count);
        Assert.Contains(ingDetail.Allocations, a => a.SourceEntryVersionId == source1 && a.Quantity == 600m);
        Assert.Contains(ingDetail.Allocations, a => a.SourceEntryVersionId == source2 && a.Quantity == 400m);
    }

    // ---- Scenario 2: Correction creates v2 while leaving v1 completely immutable ----

    [Fact]
    public Task CorrectionVersion2_PreservesVersion1Immutably_Sqlite() =>
        RunCorrectionPreservesV1Async(postgres: false);

    [PostgresFact]
    public Task CorrectionVersion2_PreservesVersion1Immutably_Postgres() =>
        RunCorrectionPreservesV1Async(postgres: true);

    private static async Task RunCorrectionPreservesV1Async(bool postgres)
    {
        using var h = await CreateSettledHarnessAsync(postgres);
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();
        var source3 = Guid.NewGuid();

        var v1Res = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2));
        Assert.Equal(HttpStatusCode.Created, v1Res.StatusCode);
        var v1 = (await v1Res.Content.ReadFromJsonAsync<ServingResponse>())!;
        Assert.Null(v1.PriorVersionId);

        var v2Request = new CreateServingVersionRequest(
            ExpectedVersion: 1,
            Reason: "Điều chỉnh món thực tế do thiếu nguyên liệu kho",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: h.RecipeVersionId,
                    ActualRecipeVersionId: h.AltRecipeVersionId,
                    ServedPortions: 1,
                    SubstitutionReason: "Đổi sang thịt rim mặn ngọt",
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: h.IngredientId,
                            NutrientVersionId: h.IngredientVersionId,
                            Quantity: 850m,
                            Unit: "g",
                            WeightBasis: "gross",
                            Allocations: [new SourceAllocationInput(source3, 850m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var v2Res = await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{v1.Id}/versions", v2Request);
        Assert.Equal(HttpStatusCode.Created, v2Res.StatusCode);
        var v2 = (await v2Res.Content.ReadFromJsonAsync<ServingResponse>())!;
        Assert.Equal(2, v2.Version);
        Assert.Equal(v1.VersionId, v2.PriorVersionId);

        var latest = await h.Client.GetFromJsonAsync<ServingDetailResponse>($"/api/kitchen/servings/{v1.Id}");
        Assert.NotNull(latest);
        Assert.Equal(2, latest.Version);
        Assert.Equal(h.AltRecipeVersionId, Assert.Single(latest.Dishes).ActualRecipeVersionId);

        var history = await h.Client.GetFromJsonAsync<List<ServingHistoryItem>>($"/api/kitchen/servings/{v1.Id}/history");
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(1, history[0].Version);
        Assert.Null(history[0].Reason);
        Assert.Null(history[0].PriorVersionId);
        Assert.Equal(2, history[1].Version);
        Assert.Equal("Điều chỉnh món thực tế do thiếu nguyên liệu kho", history[1].Reason);
        Assert.Equal(v1.VersionId, history[1].PriorVersionId);

        await RunWithReadDbAsync(h, postgres, async db =>
        {
            var versions = await db.ServingVersions.AsNoTracking()
                .Include(v => v.Dishes)
                    .ThenInclude(d => d.IngredientUses)
                        .ThenInclude(u => u.Allocations)
                .Where(v => v.ServingId == v1.Id)
                .OrderBy(v => v.Version)
                .ToListAsync();

            Assert.Equal(2, versions.Count);
            var persistedV1 = versions[0];
            Assert.Equal(1, persistedV1.Version);
            Assert.Null(persistedV1.PriorVersionId);
            Assert.Equal(persistedV1.Id, versions[1].PriorVersionId);
            var v1Dish = Assert.Single(persistedV1.Dishes);
            Assert.Equal(h.RecipeVersionId, v1Dish.ActualRecipeVersionId);
            var v1Ing = Assert.Single(v1Dish.IngredientUses);
            Assert.Equal(1000m, v1Ing.Quantity);
            Assert.Equal(2, v1Ing.Allocations.Count);
            Assert.Contains(v1Ing.Allocations, a => a.SourceEntryVersionId == source1 && a.Quantity == 600m);
            Assert.Contains(v1Ing.Allocations, a => a.SourceEntryVersionId == source2 && a.Quantity == 400m);
        });
    }

    // ---- Scenario 3: Idempotency retry, Validation & Concurrency ----

    [Fact]
    public Task ValidationIdempotencyAndStaleVersion_BehaveAsSpecified_Sqlite() =>
        RunValidationIdempotencyAndConcurrencyAsync(postgres: false);

    [PostgresFact]
    public Task ValidationIdempotencyAndConcurrentWrites_BehaveAsSpecified_Postgres() =>
        RunValidationIdempotencyAndConcurrencyAsync(postgres: true);

    private static async Task RunValidationIdempotencyAndConcurrencyAsync(bool postgres)
    {
        using var h = await CreateSettledHarnessAsync(postgres);
        var clientReqId = Guid.NewGuid();
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();

        var badTokenRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2, snapshotToken: new string('f', 64)));
        Assert.Equal(HttpStatusCode.BadRequest, badTokenRes.StatusCode);

        var badRecipeRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2, recipeVersionId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, badRecipeRes.StatusCode);

        var emptyMenuReq = BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2) with { MenuVersionId = Guid.Empty };
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", emptyMenuReq)).StatusCode);

        var dupSourceReq = BuildSplitServingRequest(h, Guid.NewGuid(), source1, source1);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", dupSourceReq)).StatusCode);

        var badNutrientReq = BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2);
        badNutrientReq = badNutrientReq with
        {
            Dishes =
            [
                badNutrientReq.Dishes[0] with
                {
                    Ingredients = [badNutrientReq.Dishes[0].Ingredients[0] with { NutrientVersionId = Guid.NewGuid() }]
                }
            ]
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", badNutrientReq)).StatusCode);

        var validReq = BuildSplitServingRequest(h, clientReqId, source1, source2);
        var firstRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings", validReq);
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);
        var first = (await firstRes.Content.ReadFromJsonAsync<ServingResponse>())!;

        var retrySameRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings", validReq);
        Assert.Equal(HttpStatusCode.OK, retrySameRes.StatusCode);
        var retrySame = (await retrySameRes.Content.ReadFromJsonAsync<ServingResponse>())!;
        Assert.Equal(first.Id, retrySame.Id);
        Assert.Equal(first.VersionId, retrySame.VersionId);

        await RunWithReadDbAsync(h, postgres, async db =>
        {
            Assert.Equal(1, await db.Servings.CountAsync(s => s.MealDayId == h.Seed.DayId && s.ClassId == h.Seed.ClassId));
            Assert.Equal(1, await db.ServingVersions.CountAsync(v => v.ServingId == first.Id));
        });

        var mismatchReq = BuildSplitServingRequest(h, clientReqId, source1, source2, servedPortions: 5);
        var mismatchRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings", mismatchReq);
        Assert.Equal(HttpStatusCode.Conflict, mismatchRes.StatusCode);

        var otherClientReqSameSession = BuildSplitServingRequest(h, Guid.NewGuid(), source1, source2);
        var duplicateSessionRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings", otherClientReqSameSession);
        Assert.Equal(HttpStatusCode.Conflict, duplicateSessionRes.StatusCode);

        var staleVersionReq = new CreateServingVersionRequest(
            ExpectedVersion: 99,
            Reason: "Stale update",
            Dishes: validReq.Dishes);
        var staleRes = await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{first.Id}/versions", staleVersionReq);
        Assert.Equal(HttpStatusCode.Conflict, staleRes.StatusCode);

        if (!postgres)
        {
            return;
        }

        // Concurrent POST /versions with same expectedVersion=1 -> exactly 1 Created, 1 Conflict
        var v2Payload = new CreateServingVersionRequest(
            ExpectedVersion: 1,
            Reason: "Concurrent correction",
            Dishes: validReq.Dishes);
        using var client2 = h.Factory.CreateClient();
        await Authorize(client2, h.KitchenEmail, h.Seed.Password);
        var versionRace = await Task.WhenAll(
            h.Client.PostAsJsonAsync($"/api/kitchen/servings/{first.Id}/versions", v2Payload),
            client2.PostAsJsonAsync($"/api/kitchen/servings/{first.Id}/versions", v2Payload));
        Assert.Single(versionRace, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(versionRace, r => r.StatusCode == HttpStatusCode.Conflict);

        // Concurrent POST /servings with same ClientRequestId + same payload -> 0 server errors, 0 duplicate rows
        var foreignClientReqId = Guid.NewGuid();
        var foreignReq = BuildSplitServingRequest(
            h,
            foreignClientReqId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            classId: h.Seed.ForeignClassId,
            settlementId: h.ForeignSettlementId);
        var servingRace = await Task.WhenAll(
            h.Client.PostAsJsonAsync("/api/kitchen/servings", foreignReq),
            client2.PostAsJsonAsync("/api/kitchen/servings", foreignReq));
        Assert.Single(servingRace, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(servingRace, r => r.StatusCode == HttpStatusCode.OK);
        var r1 = (await servingRace[0].Content.ReadFromJsonAsync<ServingResponse>())!;
        var r2 = (await servingRace[1].Content.ReadFromJsonAsync<ServingResponse>())!;
        Assert.Equal(r1.Id, r2.Id);
        Assert.Equal(r1.VersionId, r2.VersionId);

        await RunWithReadDbAsync(h, postgres: true, async db =>
        {
            Assert.Equal(1, await db.Servings.CountAsync(s => s.MealDayId == h.Seed.DayId && s.ClassId == h.Seed.ForeignClassId));
            Assert.Equal(1, await db.ServingVersions.CountAsync(v => v.ClientRequestId == foreignClientReqId));
        });
    }

    // ---- Scenario 3b: Strict RBAC & Stale Settlement Protection ----

    [Fact]
    public async Task ServingEndpoints_EnforceStrictRbac()
    {
        using var h = await CreateSettledHarnessAsync(postgres: false);
        var createReq = BuildSplitServingRequest(h, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var createdRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings", createReq);
        Assert.Equal(HttpStatusCode.Created, createdRes.StatusCode);
        var created = (await createdRes.Content.ReadFromJsonAsync<ServingResponse>())!;

        var versionReq = new CreateServingVersionRequest(1, "Cập nhật", createReq.Dishes);
        var foreignReq = BuildSplitServingRequest(
            h,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            classId: h.Seed.ForeignClassId,
            settlementId: h.ForeignSettlementId);

        // 1. Anonymous -> 401
        h.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", foreignReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{created.Id}/versions", versionReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}/history")).StatusCode);

        // 2. Teacher -> 403
        await Authorize(h.Client, h.Seed.TeacherEmail, h.Seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", foreignReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{created.Id}/versions", versionReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}/history")).StatusCode);

        // 3. Parent -> 403
        await Authorize(h.Client, "parent@test.local", h.Seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", foreignReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{created.Id}/versions", versionReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}/history")).StatusCode);

        // 4. Admin -> 403 on writes, 200 on reads
        await Authorize(h.Client, h.Seed.AdminEmail, h.Seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", foreignReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{created.Id}/versions", versionReq)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}/history")).StatusCode);

        // 5. Dual-role KitchenStaff + Admin -> 403 on writes, 200 on reads
        using (var scope = h.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var kitchenUser = await users.FindByEmailAsync(h.KitchenEmail);
            Assert.NotNull(kitchenUser);
            Assert.True((await users.AddToRoleAsync(kitchenUser, RoleNames.Admin)).Succeeded);
        }

        await Authorize(h.Client, h.KitchenEmail, h.Seed.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync("/api/kitchen/servings", foreignReq)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.Client.PostAsJsonAsync($"/api/kitchen/servings/{created.Id}/versions", versionReq)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.Client.GetAsync($"/api/kitchen/servings/{created.Id}/history")).StatusCode);
    }

    [Fact]
    public Task StaleSettlementOrSnapshotToken_Returns409AndFreshSucceeds_Sqlite() =>
        RunStaleSettlementCheckAsync(postgres: false);

    [PostgresFact]
    public Task StaleSettlementOrSnapshotToken_Returns409AndFreshSucceeds_Postgres() =>
        RunStaleSettlementCheckAsync(postgres: true);

    private static async Task RunStaleSettlementCheckAsync(bool postgres)
    {
        using var h = await CreateSettledHarnessAsync(postgres);

        await Authorize(h.Client, h.Seed.TeacherEmail, h.Seed.Password);
        var submitRes = await h.Client.PostAsJsonAsync($"/api/meal-days/{h.Seed.DayId}/amendments", new
        {
            classId = h.Seed.ClassId,
            studentId = h.Seed.AbsentId,
            baseSettlementId = h.SettlementId,
            willEat = true,
            reason = "Trẻ đi học lại bổ sung suất"
        });
        var amendmentId = await EventId(submitRes);

        await Authorize(h.Client, h.Seed.AdminEmail, h.Seed.Password);
        var reviewRes = await h.Client.PostAsJsonAsync($"/api/meal-days/{h.Seed.DayId}/amendments/{amendmentId}/review", new
        {
            approve = true,
            reason = "Duyệt bổ sung suất ăn"
        });
        reviewRes.EnsureSuccessStatusCode();

        Guid settlementV2Id;
        using (var scope = h.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            settlementV2Id = await db.PortionSettlements
                .Where(s => s.MealDayId == h.Seed.DayId && s.ClassId == h.Seed.ClassId)
                .OrderByDescending(s => s.Version)
                .Select(s => s.Id)
                .FirstAsync();
        }
        Assert.NotEqual(h.SettlementId, settlementV2Id);

        await Authorize(h.Client, h.KitchenEmail, h.Seed.Password);

        // Old settlement_v1 + old token_v1 -> 409 Conflict
        var staleBothRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                settlementId: h.SettlementId, snapshotToken: h.SnapshotToken));
        Assert.Equal(HttpStatusCode.Conflict, staleBothRes.StatusCode);

        // New settlement_v2 + old acknowledged token_v1 -> 409 Conflict
        var staleTokenRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                settlementId: settlementV2Id, snapshotToken: h.SnapshotToken));
        Assert.Equal(HttpStatusCode.Conflict, staleTokenRes.StatusCode);

        var diffV2 = await h.Client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{h.Seed.DayId}/diff");
        var tokenV2 = diffV2.GetProperty("snapshotToken").GetString()!;
        Assert.NotEqual(h.SnapshotToken, tokenV2);

        // Fresh token_v2 before acknowledge -> 400 BadRequest
        var unackedRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                settlementId: settlementV2Id, snapshotToken: tokenV2));
        Assert.Equal(HttpStatusCode.BadRequest, unackedRes.StatusCode);

        // Kitchen acknowledges token_v2
        var ackV2Res = await h.Client.PostAsJsonAsync($"/api/kitchen/settlements/{h.Seed.DayId}/acknowledge", new
        {
            snapshotToken = tokenV2,
            note = "Bếp xác nhận bản chốt v2"
        });
        Assert.Equal(HttpStatusCode.Created, ackV2Res.StatusCode);

        // Old settlement_v1 + fresh token_v2 -> 409 Conflict
        var staleSettlementRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                settlementId: h.SettlementId, snapshotToken: tokenV2));
        Assert.Equal(HttpStatusCode.Conflict, staleSettlementRes.StatusCode);

        // Fresh settlement_v2 + fresh acknowledged token_v2 -> 201 Created
        var freshRes = await h.Client.PostAsJsonAsync("/api/kitchen/servings",
            BuildSplitServingRequest(h, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                settlementId: settlementV2Id, snapshotToken: tokenV2));
        Assert.Equal(HttpStatusCode.Created, freshRes.StatusCode);
    }

    // ---- Scenario 4: Transfer and absence at 07:15 after Lunch cutoff 07:00 keep student in ClassA ----

    [Fact]
    public Task TransferAt0715AfterLunchCutoff0700_KeepsStudentInClassA_Sqlite() =>
        RunTransferAfterLunchCutoffAsync(postgres: false);

    [PostgresFact]
    public Task TransferAt0715AfterLunchCutoff0700_KeepsStudentInClassA_Postgres() =>
        RunTransferAfterLunchCutoffAsync(postgres: true);

    private static async Task RunTransferAfterLunchCutoffAsync(bool postgres)
    {
        var date = FutureSchoolDay;
        var clock = new TestClock();
        clock.Set(At(date, 6, 0));

        using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync();

        Guid classAId = seed.ClassId;
        Guid classBId;
        Guid studentId;
        Guid dayId;
        const string parentEmail = "parent_late_cutoff@test.local";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var classB = new SchoolClass { Name = "Lớp Chuyển Đến B", SchoolYear = "2026-2027" };
            db.Classes.Add(classB);

            var student = new Student
            {
                FullName = "Bé Nguyễn Minh Khang",
                ClassId = classAId,
                Enrollments =
                [
                    new Enrollment
                    {
                        ClassId = classAId,
                        StartDate = date.AddDays(-2),
                        RecordedAt = At(date.AddDays(-2), 8, 0)
                    }
                ]
            };
            db.Students.Add(student);

            var lunchDay = new MealDay
            {
                Date = date,
                SchoolYear = "2026-2027",
                MealType = "Lunch",
                CutoffAt = SchoolTime.Cutoff(date, "Lunch")
            };
            db.MealDays.Add(lunchDay);

            var parent = new ApplicationUser
            {
                FullName = "Phụ huynh Nguyễn Minh Khang",
                UserName = parentEmail,
                Email = parentEmail,
                EmailConfirmed = true
            };
            Assert.True((await users.CreateAsync(parent, seed.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
            db.ParentStudents.Add(new ParentStudent { UserId = parent.Id, StudentId = student.Id });
            await db.SaveChangesAsync();

            classBId = classB.Id;
            studentId = student.Id;
            dayId = lunchDay.Id;
        }

        clock.Set(At(date, 7, 15));

        await Authorize(client, seed.AdminEmail, seed.Password);
        var transferRes = await client.PostAsJsonAsync($"/api/admin/students/{studentId}/enrollments", new
        {
            classId = classBId,
            effectiveDate = date,
            reason = "Chuyển lớp lúc 07:15 sau giờ chốt bữa trưa",
            revision = 0
        });
        Assert.Equal(HttpStatusCode.NoContent, transferRes.StatusCode);

        await Authorize(client, parentEmail, seed.Password);
        var absenceRes = await client.PostAsJsonAsync("/api/parent/absences", new
        {
            studentId,
            fromDate = date,
            toDate = date,
            reason = "Báo vắng lúc 07:15 sau giờ chốt bữa trưa"
        });
        Assert.Equal(HttpStatusCode.Created, absenceRes.StatusCode);

        clock.Set(At(date, 8, 0));
        var worker = new MealSettlementWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MealSettlementWorker>.Instance,
            clock);
        var settledCount = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(1, settledCount);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var settlements = await db.PortionSettlements
                .AsNoTracking()
                .Include(s => s.Students)
                .Where(s => s.MealDayId == dayId)
                .ToListAsync();

            var settlement = Assert.Single(settlements);
            Assert.Equal(classAId, settlement.ClassId);
            Assert.Equal(1, settlement.Count);
            Assert.Equal("system", settlement.SettledBy);
            Assert.Equal(studentId, Assert.Single(settlement.Students).StudentId);
            Assert.DoesNotContain(settlements, s => s.ClassId == classBId);
        }

        await Authorize(client, seed.AdminEmail, seed.Password);
        var portions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{dayId}/portions");
        var room = Assert.Single(portions.GetProperty("classes").EnumerateArray());
        Assert.Equal(classAId, room.GetProperty("classId").GetGuid());
        Assert.Equal(1, room.GetProperty("count").GetInt32());
        Assert.Equal(studentId, Assert.Single(room.GetProperty("studentIds").EnumerateArray()).GetGuid());
    }
}

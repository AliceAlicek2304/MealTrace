using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class KitchenSettlementTests
{
    private static async Task<Scenario> SetupSettled(AuthTestFactory factory, TestClock clock, HttpClient client)
    {
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff);
        (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();
        return seed;
    }

    private static async Task<ApplicationUser> CreateKitchenUser(AuthTestFactory factory, Scenario seed)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var kitchen = new ApplicationUser
        {
            UserName = "kitchen_staff@test.local",
            Email = "kitchen_staff@test.local",
            FullName = "Bếp Trưởng Nguyễn Thị Mai"
        };
        Assert.True((await users.CreateAsync(kitchen, seed.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(kitchen, RoleNames.KitchenStaff)).Succeeded);
        return kitchen;
    }

    [Fact]
    public async Task KitchenEndpointsEnforceAuthenticationAndStrictRbac()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient();
        var seed = await SetupSettled(factory, clock, client);

        // 1. Anonymous -> 401
        client.DefaultRequestHeaders.Authorization = null;
        var anon = await client.GetAsync("/api/kitchen/settlements");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        // 2. Teacher -> 403
        await Authorize(client, seed.TeacherEmail, seed.Password);
        var teacher = await client.GetAsync("/api/kitchen/settlements");
        Assert.Equal(HttpStatusCode.Forbidden, teacher.StatusCode);

        var teacherDiff = await client.GetAsync($"/api/kitchen/settlements/{seed.DayId}/diff");
        Assert.Equal(HttpStatusCode.Forbidden, teacherDiff.StatusCode);

        var teacherAck = await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", new { note = "test" });
        Assert.Equal(HttpStatusCode.Forbidden, teacherAck.StatusCode);

        // 3. Admin -> 200
        await Authorize(client, seed.AdminEmail, seed.Password);
        var admin = await client.GetAsync("/api/kitchen/settlements");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);

        // 4. KitchenStaff -> 200
        var kitchen = await CreateKitchenUser(factory, seed);
        await Authorize(client, kitchen.Email!, seed.Password);
        var kitchenStaff = await client.GetAsync("/api/kitchen/settlements");
        Assert.Equal(HttpStatusCode.OK, kitchenStaff.StatusCode);
    }

    [Fact]
    public async Task UnsettledMealDayRejectsDiffAndAcknowledgeWithConflict()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); // Not settled yet!
        var kitchen = await CreateKitchenUser(factory, seed);
        await Authorize(client, kitchen.Email!, seed.Password);

        // Diff on unsettled day -> 409 Conflict
        var diff = await client.GetAsync($"/api/kitchen/settlements/{seed.DayId}/diff");
        Assert.Equal(HttpStatusCode.Conflict, diff.StatusCode);

        // Acknowledge on unsettled day -> 409 Conflict
        var ack = await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", new { note = "Nấu sớm", snapshotToken = new string('0', 64) });
        Assert.Equal(HttpStatusCode.Conflict, ack.StatusCode);
    }

    [Fact]
    public async Task KitchenReadsSettlementDiffReflectingInitialAndAmendedCounts()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient();
        var seed = await SetupSettled(factory, clock, client);
        var kitchen = await CreateKitchenUser(factory, seed);

        // Step 1: Kitchen checks initial settled state
        await Authorize(client, kitchen.Email!, seed.Password);
        var date = MealTrace.Domain.Time.SchoolTime.Today(seed.Cutoff.AddMinutes(-20));
        var listResponse = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements?date={date:yyyy-MM-dd}");
        Assert.True(listResponse.GetArrayLength() > 0);
        var item = listResponse.EnumerateArray().First(x => x.GetProperty("mealDayId").GetGuid() == seed.DayId);
        Assert.Equal(2, item.GetProperty("originalPortions").GetInt32());
        Assert.Equal(2, item.GetProperty("appliedPortions").GetInt32());
        Assert.Equal(0, item.GetProperty("diff").GetInt32());
        Assert.False(item.GetProperty("isAcknowledged").GetBoolean());

        var diffResponse = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        Assert.Equal(2, diffResponse.GetProperty("originalTotal").GetInt32());
        Assert.Equal(2, diffResponse.GetProperty("appliedTotal").GetInt32());
        Assert.Equal(0, diffResponse.GetProperty("totalDiff").GetInt32());
        var classDiff = diffResponse.GetProperty("classDiffs").EnumerateArray().First(c => c.GetProperty("classId").GetGuid() == seed.ClassId);
        Assert.Equal(0, classDiff.GetProperty("addedStudents").GetArrayLength());
        Assert.Equal(0, classDiff.GetProperty("removedStudents").GetArrayLength());
        Assert.Equal(1, classDiff.GetProperty("originalCount").GetInt32());
        Assert.Equal(1, classDiff.GetProperty("appliedCount").GetInt32());

        // Step 2: Teacher submits an amendment to add the absent child back (+1)
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var origSettlement = db.PortionSettlements.First(x => x.MealDayId == seed.DayId && x.ClassId == seed.ClassId);
            await Authorize(client, seed.TeacherEmail, seed.Password);
            var submitRes = await client.PostAsJsonAsync($"/api/meal-days/{seed.DayId}/amendments", new
            {
                classId = seed.ClassId,
                studentId = seed.AbsentId,
                baseSettlementId = origSettlement.Id,
                willEat = true,
                reason = "Trẻ đi học lại đột xuất"
            });
            var amendmentId = await EventId(submitRes);

            // Admin approves the amendment
            await Authorize(client, seed.AdminEmail, seed.Password);
            (await client.PostAsJsonAsync($"/api/meal-days/{seed.DayId}/amendments/{amendmentId}/review", new
            {
                approve = true,
                reason = "Duyệt bổ sung suất"
            })).EnsureSuccessStatusCode();
        }

        // Step 3: Kitchen checks updated diff
        await Authorize(client, kitchen.Email!, seed.Password);
        var updatedDiff = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        Assert.Equal(2, updatedDiff.GetProperty("originalTotal").GetInt32());
        Assert.Equal(3, updatedDiff.GetProperty("appliedTotal").GetInt32());
        Assert.Equal(1, updatedDiff.GetProperty("totalDiff").GetInt32());

        var updatedClassDiff = updatedDiff.GetProperty("classDiffs").EnumerateArray().First(c => c.GetProperty("classId").GetGuid() == seed.ClassId);
        Assert.Equal(2, updatedClassDiff.GetProperty("currentVersion").GetInt32());
        Assert.Equal(1, updatedClassDiff.GetProperty("diff").GetInt32());
        Assert.Single(updatedClassDiff.GetProperty("addedStudents").EnumerateArray());
        Assert.Equal(seed.AbsentId, updatedClassDiff.GetProperty("addedStudents")[0].GetProperty("studentId").GetGuid());
    }

    [Fact]
    public async Task KitchenCanAcknowledgeSettlementAndInspectAuditHistory()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient();
        var seed = await SetupSettled(factory, clock, client);
        var kitchen = await CreateKitchenUser(factory, seed);
        await Authorize(client, kitchen.Email!, seed.Password);

        var snapshot = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        // 1. Post acknowledgement
        var ackRes = await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", new
        {
            note = "Bếp xác nhận nhận đủ 2 suất và chuẩn bị nấu",
            snapshotToken = snapshot.GetProperty("snapshotToken").GetString()
        });
        Assert.Equal(HttpStatusCode.Created, ackRes.StatusCode);
        var ackData = await ackRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(seed.DayId, ackData.GetProperty("mealDayId").GetGuid());
        Assert.Equal(2, ackData.GetProperty("portionsCount").GetInt32());
        Assert.Equal("Bếp Trưởng Nguyễn Thị Mai", ackData.GetProperty("acknowledgedByName").GetString());
        Assert.Equal("Bếp xác nhận nhận đủ 2 suất và chuẩn bị nấu", ackData.GetProperty("note").GetString());

        // 2. Query acknowledgements history
        var historyRes = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/acknowledgements");
        Assert.Single(historyRes.EnumerateArray());
        Assert.Equal(ackData.GetProperty("id").GetGuid(), historyRes[0].GetProperty("id").GetGuid());

        // 3. Query settlements list reflects isAcknowledged = true
        var listRes = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements");
        var item = listRes.EnumerateArray().First(x => x.GetProperty("mealDayId").GetGuid() == seed.DayId);
        Assert.True(item.GetProperty("isAcknowledged").GetBoolean());
        Assert.Equal("Bếp Trưởng Nguyễn Thị Mai", item.GetProperty("lastAcknowledgedByName").GetString());
    }
}

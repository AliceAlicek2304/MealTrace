using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class KitchenSettlementRegressionTests
{
    [PostgresFact]
    public async Task ConcurrentKitchenAcknowledgementsNeverDuplicateHistory()
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(postgres: true, clock: clock);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(first, seed.AdminEmail, seed.Password);
        await Authorize(second, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff);
        (await first.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();
        var diff = await first.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        var input = new { snapshotToken = diff.GetProperty("snapshotToken").GetString(), note = "Đã kiểm tra" };
        var responses = await Task.WhenAll(first.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", input), second.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", input));
        Assert.Contains(responses, response => response.StatusCode == System.Net.HttpStatusCode.Created);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { System.Net.HttpStatusCode.Created, System.Net.HttpStatusCode.Conflict }));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().SettlementAcknowledgements.CountAsync());
    }

    [Fact]
    public void SwaggerDocumentsSnapshotConfirmationAndErrorResponses()
    {
        using var factory = new AuthTestFactory();
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>();
        var document = provider.GetSwagger("v1");
        var operation = document.Paths["/api/kitchen/settlements/{id}/acknowledge"].Operations[Microsoft.OpenApi.Models.OperationType.Post];
        Assert.Equal("AcknowledgeKitchenSettlement", operation.OperationId);
        Assert.All(new[] { "201", "400", "401", "403", "404", "409" }, status => Assert.True(operation.Responses.ContainsKey(status)));
        Assert.Contains("snapshotToken", document.Components.Schemas["AcknowledgeSettlementRequest"].Required);
        Assert.Contains("/api/kitchen/settlements/search", document.Paths.Keys);
    }

    [Fact] public Task MissingHistoricRosterMustNotInventAddedChildren() => MissingHistoricRosterMustNotInventAddedChildrenFlow(false);
    [PostgresFact] public Task PostgreSqlMissingHistoricRosterMustNotInventAddedChildren() => MissingHistoricRosterMustNotInventAddedChildrenFlow(true);
    private async Task MissingHistoricRosterMustNotInventAddedChildrenFlow(bool postgres)
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.SingleAsync(x => x.Id == seed.DayId);
            day.SettledAt = clock.GetUtcNow();
            var original = new PortionSettlement {
                MealDayId = seed.DayId, ClassId = seed.ClassId, ClassName = "Lớp test",
                Count = 1, SettledBy = seed.AdminEmail, CutoffAt = seed.Cutoff
            };
            db.PortionSettlements.Add(original);
            db.PortionSettlements.Add(new PortionSettlement {
                MealDayId = original.MealDayId, ClassId = original.ClassId, ClassName = original.ClassName,
                Version = 2, SupersedesId = original.Id, Count = original.Count,
                SettledBy = original.SettledBy, CutoffAt = original.CutoffAt,
                Students = [new SettlementStudent { StudentId = seed.PresentId, StudentName = "Trẻ đã ăn từ bản gốc" }]
            });
            await db.SaveChangesAsync();
        }
        var diff = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        var room = diff.GetProperty("classDiffs").EnumerateArray().Single(x => x.GetProperty("classId").GetGuid() == seed.ClassId);
        Assert.Empty(room.GetProperty("addedStudents").EnumerateArray());
        Assert.False(room.GetProperty("hasCompleteRoster").GetBoolean());
    }

    [Fact] public Task NewSettlementVersionMustRequireFreshKitchenAcknowledgement() => NewSettlementVersionMustRequireFreshKitchenAcknowledgementFlow(false);
    [PostgresFact] public Task PostgreSqlNewSettlementVersionMustRequireFreshKitchenAcknowledgement() => NewSettlementVersionMustRequireFreshKitchenAcknowledgementFlow(true);
    private async Task NewSettlementVersionMustRequireFreshKitchenAcknowledgementFlow(bool postgres)
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff);
        (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).EnsureSuccessStatusCode();
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/diff");
        var input = new { note = "Đã nhận bản đầu", snapshotToken = before.GetProperty("snapshotToken").GetString() };
        var acknowledged = await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", input);
        acknowledged.EnsureSuccessStatusCode();
        var first = await acknowledged.Content.ReadFromJsonAsync<JsonElement>();
        var repeated = await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", input);
        repeated.EnsureSuccessStatusCode();
        Assert.Equal(first.GetProperty("id").GetGuid(), (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var original = await db.PortionSettlements.FirstAsync(x => x.MealDayId == seed.DayId && x.ClassId == seed.ClassId);
            db.PortionSettlements.Add(new PortionSettlement {
                MealDayId = original.MealDayId, ClassId = original.ClassId, ClassName = original.ClassName,
                Version = 2, SupersedesId = original.Id, Count = original.Count + 1,
                KitchenAdjustment = 1, SettledBy = original.SettledBy, CutoffAt = original.CutoffAt,
                Students = await db.SettlementStudents.Where(x => x.PortionSettlementId == original.Id)
                    .Select(x => new SettlementStudent { StudentId = x.StudentId, StudentName = x.StudentName }).ToListAsync()
            });
            await db.SaveChangesAsync();
        }
        var list = await client.GetFromJsonAsync<JsonElement>("/api/kitchen/settlements");
        var day = list.EnumerateArray().Single(x => x.GetProperty("mealDayId").GetGuid() == seed.DayId);
        Assert.Equal(3, day.GetProperty("appliedPortions").GetInt32());
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", input)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/kitchen/settlements/{seed.DayId}/acknowledge", new { note = "No snapshot" })).StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/kitchen/settlements/{seed.DayId}/acknowledgements");
        Assert.Single(history.EnumerateArray());
        var page = await client.GetFromJsonAsync<JsonElement>("/api/kitchen/settlements/search?page=1&pageSize=1");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Single(page.GetProperty("items").EnumerateArray());
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/kitchen/settlements/search?page=2&pageSize=1");
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.False(day.GetProperty("isAcknowledged").GetBoolean(), "An acknowledgement of the old total must not confirm the new settlement version.");
    }
}

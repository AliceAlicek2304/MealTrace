using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class SettlementAcknowledgementAuditTests
{
    private const string Root = "/api/admin/audit-logs";
    private const string Action = "kitchen.settlement.acknowledge";
    private const string EntityType = nameof(SettlementAcknowledgement);

    [Fact]
    public Task SqliteAcknowledgementAuditUsesOnePersistedEventAfterRetry() => VerifyAcknowledgementAudit(false);

    [PostgresFact]
    public Task PostgreSqlAcknowledgementAuditUsesOnePersistedEventAfterRetry() => VerifyAcknowledgementAudit(true);

    private static async Task VerifyAcknowledgementAudit(bool postgres)
    {
        var clock = new TestClock();
        using var factory = new AuthTestFactory(postgres: postgres, clock: clock);
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        clock.Set(seed.Cutoff);
        using var settled = await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null);
        settled.EnsureSuccessStatusCode();

        var settlementsBefore = await client.GetFromJsonAsync<AuditPage>($"{Root}?action=portion.settle");
        Assert.NotEmpty(settlementsBefore!.Items);

        var kitchen = await KitchenSettlementTests.CreateKitchenUser(factory, seed);
        await Authorize(client, kitchen.Email!, seed.Password);
        var kitchenUrl = $"/api/kitchen/settlements/{seed.DayId}";
        var diff = await client.GetFromJsonAsync<KitchenSettlementDiffResponse>($"{kitchenUrl}/diff");
        const string privateNote = "NEVER_AUDIT_NOTE, =SUM(1,2)\r\n\"nội bộ\" password=NEVER_AUDIT_PASSWORD";
        var input = new AcknowledgeSettlementRequest(privateNote, diff!.SnapshotToken);

        var acknowledgedAt = seed.Cutoff.AddMinutes(5);
        clock.Set(acknowledgedAt);
        using var first = await client.PostAsJsonAsync($"{kitchenUrl}/acknowledge", input);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstAck = await first.Content.ReadFromJsonAsync<SettlementAcknowledgementResponse>();

        clock.Set(acknowledgedAt.AddMinutes(1));
        using var retry = await client.PostAsJsonAsync($"{kitchenUrl}/acknowledge", input);
        retry.EnsureSuccessStatusCode();
        var retryAck = await retry.Content.ReadFromJsonAsync<SettlementAcknowledgementResponse>();
        Assert.Equal(firstAck, retryAck);

        SettlementAcknowledgement stored;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            stored = await db.SettlementAcknowledgements.AsNoTracking().SingleAsync(x => x.MealDayId == seed.DayId);
        }
        Assert.Equal(firstAck!.Id, stored.Id);
        Assert.Equal(acknowledgedAt, stored.AcknowledgedAt);
        Assert.Equal(diff.AppliedTotal, stored.PortionsCount);
        Assert.Equal(diff.SnapshotToken, stored.SnapshotToken);
        Assert.Equal(privateNote, stored.Note);

        await Authorize(client, seed.AdminEmail, seed.Password);
        var page = await client.GetFromJsonAsync<AuditPage>($"{Root}?pageSize=100");
        var entry = Assert.Single(page!.Items, x => x.Source == "settlement-acknowledgement");
        Assert.Equal($"settlement-acknowledgement:{stored.Id:D}", entry.Id);
        Assert.Equal(EntityType, entry.EntityType);
        Assert.Equal(stored.Id.ToString("D"), entry.EntityId);
        Assert.Equal(Action, entry.Action);
        Assert.Equal(new AuditActor(kitchen.Id, null), entry.Actor);
        Assert.Equal(stored.AcknowledgedAt, entry.OccurredAt);
        Assert.Null(entry.Before);
        Assert.Null(entry.Reason);
        Assert.Null(entry.CorrelationId);
        var after = entry.After!.Value;
        Assert.Equal(new[] { "count", "id", "mealDayId" }, after.EnumerateObject().Select(field => field.Name).Order());
        Assert.Equal(stored.Id, after.GetProperty("id").GetGuid());
        Assert.Equal(stored.MealDayId, after.GetProperty("mealDayId").GetGuid());
        Assert.Equal(stored.PortionsCount, after.GetProperty("count").GetInt32());

        var at = Uri.EscapeDataString(stored.AcknowledgedAt.ToString("O"));
        var scopeFilter = $"entityType={EntityType}&entityId={stored.Id:D}";
        var filter = $"{scopeFilter}&actorId={kitchen.Id:D}&from={at}&to={at}&action={Action}";
        var filtered = await client.GetFromJsonAsync<AuditPage>($"{Root}?{filter}&pageSize=1");
        Assert.Equal(1L, filtered!.Total);
        Assert.Equal(entry.Id, Assert.Single(filtered.Items).Id);
        var later = Uri.EscapeDataString(stored.AcknowledgedAt.AddSeconds(1).ToString("O"));
        var excludingFilters = new[]
        {
            $"{scopeFilter}&actorId={seed.TeacherId:D}",
            $"{scopeFilter}&from={later}",
            $"entityType=PortionSettlement&entityId={stored.Id:D}",
            $"entityType={EntityType}&entityId={stored.MealDayId:D}",
            $"{scopeFilter}&action=portion.settle"
        };
        foreach (var query in excludingFilters)
        {
            var excluded = await client.GetFromJsonAsync<AuditPage>($"{Root}?{query}");
            Assert.Equal(0L, excluded!.Total);
            Assert.Empty(excluded.Items);
        }

        var detail = await client.GetFromJsonAsync<AuditEntry>($"{Root}/{entry.Id}");
        var timeline = await client.GetFromJsonAsync<AuditPage>($"{Root}/entities/{EntityType}/{stored.Id:D}");
        var entryJson = JsonSerializer.Serialize(entry);
        Assert.Equal(entryJson, JsonSerializer.Serialize(detail));
        Assert.Equal(entryJson, JsonSerializer.Serialize(Assert.Single(timeline!.Items)));
        var settlementsAfter = await client.GetFromJsonAsync<AuditPage>($"{Root}?action=portion.settle");
        Assert.Equal(JsonSerializer.Serialize(settlementsBefore), JsonSerializer.Serialize(settlementsAfter));

        using var export = await client.GetAsync($"{Root}/export?{filter}");
        export.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", export.Content.Headers.ContentType!.MediaType);
        var csv = Encoding.UTF8.GetString(await export.Content.ReadAsByteArrayAsync());
        var csvRow = Assert.Single(csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1));
        Assert.StartsWith($"\"{entry.Id}\",", csvRow);
        Assert.Contains($"\"{Action}\"", csvRow);
        Assert.Contains("\"" + after.GetRawText().Replace("\"", "\"\"") + "\"", csvRow);
        foreach (var output in new[] { JsonSerializer.Serialize(page), JsonSerializer.Serialize(detail), JsonSerializer.Serialize(timeline), csv })
        {
            Assert.DoesNotContain(diff.SnapshotToken, output);
            Assert.DoesNotContain("snapshotToken", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NEVER_AUDIT_", output);
            Assert.DoesNotContain(kitchen.FullName, output);
        }
    }

    [Fact]
    public async Task LegacyAcknowledgementsWithMissingInvalidOrEmptyGuidsKeepAnUnknownActor()
    {
        using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync();
        var at = new DateTimeOffset(2026, 11, 18, 4, 30, 0, TimeSpan.Zero);
        var day = new MealDay { Date = new DateOnly(2026, 11, 18), MealType = "Lunch", CutoffAt = at, SettledAt = at };
        var legacyActors = new[] { "", " ", "legacy-kitchen", Guid.Empty.ToString("D") };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            db.SettlementAcknowledgements.AddRange(legacyActors.Select(value => new SettlementAcknowledgement
            {
                MealDay = day,
                AcknowledgedAt = at,
                AcknowledgedBy = value,
                AcknowledgedByName = "Legacy staff label",
                PortionsCount = 2
            }));
            await db.SaveChangesAsync();
        }

        await Authorize(client, seed.AdminEmail, seed.Password);
        var page = await client.GetFromJsonAsync<AuditPage>($"{Root}?action={Action}");
        Assert.Equal(legacyActors.Length, page!.Items.Count);
        Assert.All(page.Items, entry =>
        {
            Assert.Null(entry.Actor);
            Assert.Equal(at, entry.OccurredAt);
            Assert.Equal(day.Id, entry.After!.Value.GetProperty("mealDayId").GetGuid());
        });
    }
}

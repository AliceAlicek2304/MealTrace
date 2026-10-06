using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Api.Data;
using MealTrace.Api.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class SchoolTimeConcurrencyTests
{
    private sealed class LockProbe(string table) : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public TaskCompletionSource<int> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains($"FROM \"{table}\"", StringComparison.Ordinal) &&
                command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
                Waiting.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            return ValueTask.FromResult(result);
        }
    }

    private static TestClock ClockBeforeCutoff()
    {
        var clock = new TestClock();
        var date = SchoolTime.Today(TimeProvider.System.GetUtcNow()).AddDays(10);
        clock.Set(new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 29)), SchoolTime.Offset));
        return clock;
    }

    private static async Task<HttpResponseMessage> AcrossLock(AuthTestFactory factory, LockProbe probe, string table,
        Guid id, TestClock clock, DateTimeOffset afterWait, Func<Task<HttpResponseMessage>> send)
    {
        if (table is not ("Students" or "MealDays")) throw new ArgumentException("Unsupported test lock.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        await using var blocker = new NpgsqlConnection(db.Database.GetConnectionString());
        await blocker.OpenAsync(); await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand($"SELECT \"Id\" FROM \"{table}\" WHERE \"Id\" = @id FOR UPDATE", blocker, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            Assert.NotNull(await command.ExecuteScalarAsync());
        }
        probe.Enabled = true;
        var request = send(); var released = false;
        try
        {
            var pid = await probe.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var watch = Stopwatch.StartNew(); var blocked = false;
            while (watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                await using var activity = new NpgsqlCommand("SELECT wait_event_type = 'Lock' FROM pg_stat_activity WHERE pid = @pid", blocker, transaction);
                activity.Parameters.AddWithValue("pid", pid);
                blocked = await activity.ExecuteScalarAsync() is true;
                if (blocked) break;
                await Task.Delay(25);
            }
            clock.Set(afterWait);
            await transaction.CommitAsync(); released = true;
            var response = await request.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(blocked, "The API must really wait on a PostgreSQL lock before advancing the clock.");
            return response;
        }
        finally
        {
            probe.Enabled = false;
            if (!released) await transaction.RollbackAsync();
            if (!request.IsCompleted) await request.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [PostgresFact]
    public async Task TransferWaitingOnStudentLockRejectsSameDayAfterCutoffWithoutPartialChanges()
    {
        var clock = ClockBeforeCutoff(); var probe = new LockProbe("Students");
        using var factory = new AuthTestFactory(postgres: true, clock: clock, commands: probe);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);
        var date = SchoolTime.Today(clock.GetUtcNow());
        using var response = await AcrossLock(factory, probe, "Students", seed.PresentId, clock, SchoolTime.Cutoff(date).AddMinutes(1),
            () => client.PostAsJsonAsync($"/api/admin/students/{seed.PresentId}/enrollments",
                new { classId = seed.ForeignClassId, effectiveDate = date, reason = "Cross cutoff while waiting", revision = 0 }));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var student = (await db.Students.Include(x => x.Enrollments).SingleAsync(x => x.Id == seed.PresentId));
        Assert.Equal(0, student.Revision); Assert.Equal(seed.ClassId, student.ClassId);
        Assert.Null(Assert.Single(student.Enrollments).EndDate);
    }

    [PostgresFact]
    public async Task ExceptionWaitingOnDayLockCannotAppendAfterCutoff()
    {
        var clock = ClockBeforeCutoff(); var probe = new LockProbe("MealDays");
        using var factory = new AuthTestFactory(postgres: true, clock: clock, commands: probe);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        var cutoff = SchoolTime.Cutoff(SchoolTime.Today(clock.GetUtcNow()));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.CutoffAt = cutoff; await db.SaveChangesAsync();
        }
        await Authorize(client, seed.TeacherEmail, seed.Password);
        using var response = await AcrossLock(factory, probe, "MealDays", seed.DayId, clock, cutoff,
            () => Record(client, seed.DayId, seed.PresentId, "ABSENT"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.False(await database.MealRegistrations.AnyAsync());
        Assert.Equal(0, (await database.MealDays.FindAsync(seed.DayId))!.DecisionRevision);
    }

    [PostgresFact]
    public async Task ParentReportAfterLockWaitIsTimestampedLateAndOnlyAffectsLaterMeals()
    {
        var clock = ClockBeforeCutoff(); var probe = new LockProbe("Students");
        using var factory = new AuthTestFactory(postgres: true, clock: clock, commands: probe);
        using var client = factory.CreateClient(); var seed = await SeedScenario(factory, clock);
        var date = SchoolTime.Today(clock.GetUtcNow()); var cutoff = SchoolTime.Cutoff(date); Guid tomorrowId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.CutoffAt = cutoff;
            var tomorrow = new MealDay { Date = date.AddDays(1), MealType = "Lunch", SchoolYear = "2026-2027", CutoffAt = SchoolTime.Cutoff(date.AddDays(1)) };
            tomorrowId = tomorrow.Id; db.MealDays.Add(tomorrow); await db.SaveChangesAsync();
        }
        await Authorize(client, "parent@test.local", seed.Password);
        using var response = await AcrossLock(factory, probe, "Students", seed.PresentId, clock, cutoff.AddMinutes(1),
            () => client.PostAsJsonAsync("/api/parent/absences",
                new { studentId = seed.PresentId, fromDate = date, toDate = date.AddDays(1), reason = "Late report" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(clock.GetUtcNow(), body.GetProperty("reportedAt").GetDateTimeOffset());
        await Authorize(client, seed.AdminEmail, seed.Password);
        foreach (var (dayId, willEat) in new[] { (seed.DayId, true), (tomorrowId, false) })
        {
            var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{dayId}/decisions");
            Assert.Equal(willEat, decisions.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.PresentId).GetProperty("willEat").GetBoolean());
        }
    }
}

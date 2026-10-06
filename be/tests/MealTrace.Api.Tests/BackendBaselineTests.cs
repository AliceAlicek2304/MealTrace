using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features.Calendar;
using MealTrace.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class BaselineFactAttribute : FactAttribute
{
    public BaselineFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MEALTRACE_RUN_BASELINE") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEALTRACE_TEST_CONNECTION")))
            Skip = "Opt-in measurement: set MEALTRACE_RUN_BASELINE=1 and MEALTRACE_TEST_CONNECTION.";
    }
}

// Measurement harness, not a latency gate. All data lives in the factory's disposable schema.
public sealed class BackendBaselineTests
{
    private sealed record Query(string Sql, double ExecuteMs,
        [property: JsonIgnore] NpgsqlParameter[] Parameters);

    private sealed class CommandProbe : DbCommandInterceptor
    {
        private readonly List<Query> _queries = [];
        public bool Enabled { get; set; }
        public void Reset() => _queries.Clear();
        public Query[] Snapshot() => _queries.ToArray();
        private void Capture(DbCommand command, CommandExecutedEventData data)
        {
            if (Enabled) _queries.Add(new(command.CommandText, data.Duration.TotalMilliseconds,
                command.Parameters.Cast<NpgsqlParameter>().Select(p => p.Clone()).ToArray()));
        }
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData data, DbDataReader result)
        { Capture(command, data); return result; }
        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData data, int result)
        { Capture(command, data); return result; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Capture(command, data);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data,
            int result, CancellationToken cancellationToken = default)
        {
            Capture(command, data);
            return ValueTask.FromResult(result);
        }
    }

    private sealed record Sample(double RequestMs, double CommandExecuteMs, int Commands, int ResponseBytes);
    private sealed record QueryPlan(string Sql, double? ServerExecutionMs, double? RootRows,
        JsonElement? Plan, string? Note);
    private sealed record Measurement(string Name, string Method, string Route, int Samples,
        double RequestP50Ms, double RequestP95Ms, double CommandP50Ms, double CommandP95Ms,
        int MinCommands, int MaxCommands, int ResponseBytes, QueryPlan[] Queries);

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values.Order().ToArray();
        return ordered[(int)Math.Ceiling(ordered.Length * percentile) - 1];
    }

    private static async Task<QueryPlan[]> ExplainAsync(AuthTestFactory factory, Query[] queries)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync();
        var plans = new List<QueryPlan>();
        foreach (var query in queries)
        {
            // Writes/locking reads must never be replayed by EXPLAIN ANALYZE.
            if (!query.Sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                query.Sql.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                plans.Add(new(query.Sql, null, null, null, "Not replayed: write or locking command."));
                continue;
            }
            await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Sql,
                connection, transaction);
            command.Parameters.AddRange(query.Parameters.Select(p => p.Clone()).ToArray());
            var raw = (string)(await command.ExecuteScalarAsync())!;
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement[0];
            plans.Add(new(query.Sql, root.GetProperty("Execution Time").GetDouble(),
                root.GetProperty("Plan").GetProperty("Actual Rows").GetDouble(), root.Clone(), null));
        }
        await transaction.RollbackAsync();
        return plans.ToArray();
    }

    private static async Task<Measurement> MeasureAsync(string name, string route, HttpClient client,
        AuthTestFactory factory, CommandProbe probe, Action<JsonElement> verify, bool write = false)
    {
        async Task<(Sample, Query[])> Run()
        {
            probe.Reset(); probe.Enabled = true;
            var watch = Stopwatch.StartNew();
            byte[] bytes;
            try
            {
                using var response = write ? await client.PostAsync(route, null) : await client.GetAsync(route);
                bytes = await response.Content.ReadAsByteArrayAsync();
                watch.Stop();
                response.EnsureSuccessStatusCode();
            }
            finally { probe.Enabled = false; }
            using var body = JsonDocument.Parse(bytes);
            verify(body.RootElement);
            var commands = probe.Snapshot();
            return (new(watch.Elapsed.TotalMilliseconds, commands.Sum(q => q.ExecuteMs), commands.Length, bytes.Length), commands);
        }
        if (!write) { await Run(); await Run(); }
        var samples = new List<Sample>(); Query[] first = [];
        for (var i = 0; i < (write ? 1 : 10); i++)
        {
            var (sample, queries) = await Run(); samples.Add(sample);
            if (i == 0) first = queries;
        }
        // The write changes the tables: replaying its SELECTs afterwards would give misleading row counts.
        var plans = write
            ? first.Select(q => new QueryPlan(q.Sql, null, null, null, "Not replayed: measured write changed database state.")).ToArray()
            : await ExplainAsync(factory, first);
        return new(name, write ? "POST" : "GET", route, samples.Count,
            Percentile(samples.Select(x => x.RequestMs), .5), Percentile(samples.Select(x => x.RequestMs), .95),
            Percentile(samples.Select(x => x.CommandExecuteMs), .5), Percentile(samples.Select(x => x.CommandExecuteMs), .95),
            samples.Min(x => x.Commands), samples.Max(x => x.Commands), samples[0].ResponseBytes, plans);
    }

    [BaselineFact]
    public async Task CaptureWorkflowPostgresBaseline()
    {
        var output = Environment.GetEnvironmentVariable("MEALTRACE_BASELINE_OUTPUT")
            ?? throw new InvalidOperationException("Set MEALTRACE_BASELINE_OUTPUT to an absolute output directory.");
        if (!Path.IsPathFullyQualified(output)) throw new InvalidOperationException("Baseline output must be absolute.");
        Directory.CreateDirectory(output);
        foreach (var count in new[] { 500, 2000 })
        {
            var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
            var clock = new TestClock();
            clock.Set(new DateTimeOffset(date.ToDateTime(new TimeOnly(6, 0)), TimeSpan.FromHours(7)));
            var probe = new CommandProbe();
            using var factory = new AuthTestFactory(postgres: true, clock: clock, commands: probe);
            var users = await factory.SeedUsersAsync();
            using var admin = factory.CreateClient(); using var teacher = factory.CreateClient();
            await Authorize(admin, users.AdminEmail, users.Password);
            await Authorize(teacher, users.TeacherEmail, users.Password);
            var day = new MealDay { Date = date, MealType = "Lunch", SchoolYear = "2026-2027", CutoffAt = MealCalendarService.Cutoff(date) };
            var history = new MealDay { Date = date.AddDays(1), MealType = "Lunch", SchoolYear = day.SchoolYear,
                CutoffAt = MealCalendarService.Cutoff(date.AddDays(1)), SettledAt = clock.GetUtcNow() };
            int roomCount = count / 25;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var parent = new ApplicationUser { FullName = "Synthetic parent", UserName = "baseline-parent@test.local", Email = "baseline-parent@test.local" };
                Assert.True((await manager.CreateAsync(parent, users.Password)).Succeeded);
                Assert.True((await manager.AddToRoleAsync(parent, RoleNames.Parent)).Succeeded);
                var year = (await db.AcademicYears.FindAsync(day.SchoolYear))!;
                year.StartDate = date.AddDays(-30); year.EndDate = date.AddDays(300);
                db.AcademicYears.AddRange(new AcademicYear { Code = "2024-2025", StartDate = date.AddYears(-2), EndDate = date.AddYears(-1).AddDays(-1) },
                    new AcademicYear { Code = "2025-2026", StartDate = date.AddYears(-1), EndDate = date.AddDays(-1) });
                var rooms = new List<SchoolClass> { (await db.Classes.FindAsync(users.ClassId))! };
                for (var i = 1; i < roomCount; i++) rooms.Add(new() { Name = $"Synthetic class {i:D3}", SchoolYear = day.SchoolYear! });
                db.Classes.AddRange(rooms.Skip(1));
                db.TeacherAssignments.Add(new() { UserId = users.TeacherId, ClassId = users.ClassId });
                db.MealDays.AddRange(day, history);
                db.MealSchedules.Add(new() { SchoolYear = day.SchoolYear!, WeekdayMask = 62, MealTypesJson = "[\"Lunch\"]", Revision = 1 });
                var children = new List<Student>();
                for (var i = 0; i < count; i++)
                {
                    var child = new Student { FullName = $"Synthetic child {i:D5}", StudentCode = $"BASE-{i:D5}", ClassId = rooms[i / 25].Id,
                        Enrollments = [new() { ClassId = rooms[i / 25].Id, StartDate = date.AddDays(-20), RecordedAt = clock.GetUtcNow().AddDays(-20) }] };
                    children.Add(child); db.Students.Add(child);
                    db.ParentStudents.Add(new() { UserId = parent.Id, StudentId = child.Id });
                    if (i % 10 == 0)
                    {
                        db.MealAbsences.Add(new() { StudentId = child.Id, SchoolYear = day.SchoolYear, ReportedByUserId = parent.Id,
                            FromDate = date, ToDate = date.AddDays(30), Reason = "Synthetic absence", ReportedAt = clock.GetUtcNow().AddHours(-1) });
                        Guid? previous = null;
                        for (var sequence = 1; sequence <= 5; sequence++)
                        {
                            var entry = new MealRegistration { MealDayId = day.Id, StudentId = child.Id, Sequence = sequence,
                                WillEat = sequence == 5 ? null : sequence % 2 == 0, SupersedesId = previous,
                                RecordedAt = clock.GetUtcNow().AddMinutes(-10 + sequence), RecordedByUserId = users.TeacherId,
                                RecordedByName = "Synthetic teacher", Reason = "Synthetic override history" };
                            previous = entry.Id; db.MealRegistrations.Add(entry);
                        }
                    }
                }
                foreach (var room in rooms)
                {
                    Guid? previous = null;
                    for (var version = 1; version <= 3; version++)
                    {
                        var snapshot = new PortionSettlement { MealDayId = history.Id, ClassId = room.Id, ClassName = room.Name,
                            Version = version, SupersedesId = previous, Count = 25, CutoffAt = history.CutoffAt,
                            SettledBy = users.TeacherId.ToString(), SettledAt = clock.GetUtcNow().AddMinutes(version),
                            Students = children.Where(x => x.ClassId == room.Id).Select(x => new SettlementStudent { StudentId = x.Id, StudentName = x.FullName }).ToList() };
                        previous = snapshot.Id; db.PortionSettlements.Add(snapshot);
                    }
                }
                // A realistic year of sessions exercises calendar and date pagination without writing through production APIs.
                for (var offset = 2; offset < 300; offset++)
                    if (date.AddDays(offset).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                        db.MealDays.Add(new() { Date = date.AddDays(offset), MealType = "Lunch", SchoolYear = day.SchoolYear, CutoffAt = MealCalendarService.Cutoff(date.AddDays(offset)) });
                await db.SaveChangesAsync();
                // Resolve only these existing tables in the isolated SearchPath; never ANALYZE the whole database.
                await db.Database.ExecuteSqlRawAsync("ANALYZE \"Students\", \"Enrollments\", \"Classes\", \"MealDays\", \"MealAbsences\", \"MealRegistrations\", \"AcademicYears\", \"PortionSettlements\", \"SettlementStudents\", \"ParentStudents\", \"TeacherAssignments\"");
            }

            var results = new List<Measurement>();
            async Task Read(string name, string route, HttpClient client, Action<JsonElement> verify) =>
                results.Add(await MeasureAsync(name, route, client, factory, probe, verify));
            await Read("students-page", "/api/admin/students?page=1&pageSize=20", admin,
                b => { Assert.Equal(count, b.GetProperty("total").GetInt32()); Assert.Equal(20, b.GetProperty("items").GetArrayLength()); });
            await Read("students-class-filter", $"/api/admin/students?classId={users.ClassId}&status=ACTIVE&pageSize=20", admin,
                b => Assert.Equal(25, b.GetProperty("total").GetInt32()));
            await Read("students-search", "/api/admin/students?search=BASE-000&pageSize=20", admin,
                b => Assert.Equal(100, b.GetProperty("total").GetInt32()));
            await Read("portions-admin-preview", $"/api/meal-days/{day.Id}/portions", admin,
                b => Assert.Equal(roomCount, b.GetProperty("classes").GetArrayLength()));
            await Read("portions-teacher-preview", $"/api/meal-days/{day.Id}/portions", teacher,
                b => { Assert.Equal(1, b.GetProperty("classes").GetArrayLength()); Assert.Equal(users.ClassId, b.GetProperty("classes")[0].GetProperty("classId").GetGuid()); });
            await Read("decisions-admin-class-page", $"/api/meal-days/{day.Id}/decisions?classId={users.ClassId}&pageSize=20", admin,
                b => { Assert.Equal(25, b.GetProperty("total").GetInt32()); Assert.Equal(20, b.GetProperty("items").GetArrayLength()); });
            await Read("decisions-teacher-page", $"/api/meal-days/{day.Id}/decisions?pageSize=20", teacher,
                b => Assert.Equal(25, b.GetProperty("total").GetInt32()));
            await Read("portions-admin-three-versions", $"/api/meal-days/{history.Id}/portions", admin,
                b => { Assert.Equal(roomCount, b.GetProperty("classes").GetArrayLength()); Assert.All(b.GetProperty("classes").EnumerateArray(), x => Assert.Equal(3, x.GetProperty("version").GetInt32())); });
            await Read("portions-teacher-three-versions", $"/api/meal-days/{history.Id}/portions", teacher,
                b => Assert.Equal(1, b.GetProperty("classes").GetArrayLength()));
            await Read("calendar-month", $"/api/admin/meal-calendar/2026-2027?from={date:yyyy-MM-dd}&to={date.AddDays(29):yyyy-MM-dd}", admin,
                b => Assert.Equal(30, b.GetProperty("days").GetArrayLength()));
            await Read("calendar-year", $"/api/admin/meal-calendar/2026-2027?from={date.AddDays(-30):yyyy-MM-dd}&to={date.AddDays(300):yyyy-MM-dd}", admin,
                b => Assert.Equal(331, b.GetProperty("days").GetArrayLength()));
            await Read("meal-days-date-page", $"/api/meal-days/workflow?date={date:yyyy-MM-dd}", admin,
                b => Assert.Equal(1, b.GetProperty("total").GetInt32()));
            clock.Set(day.CutoffAt.AddSeconds(1));
            results.Add(await MeasureAsync("settle-single-write", $"/api/meal-days/{day.Id}/settle", admin, factory, probe,
                b => Assert.Equal(count - count / 10, b.GetProperty("total").GetInt32()), write: true));
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
                Assert.Equal(count, await db.SettlementDecisions.CountAsync(x => x.PortionSettlement.MealDayId == day.Id));
                Assert.Equal(count - count / 10, await db.SettlementStudents.CountAsync(x => x.PortionSettlement.MealDayId == day.Id));
            }
            await using var metadataScope = factory.Services.CreateAsyncScope();
            var metadataDb = metadataScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            await metadataDb.Database.OpenConnectionAsync();
            var report = new { CapturedAtUtc = DateTimeOffset.UtcNow, Provider = "PostgreSQL", ServerVersion = metadataDb.Database.GetDbConnection().ServerVersion,
                Runtime = Environment.Version.ToString(), ActiveStudents = count, Classes = roomCount, Years = 3,
                Absences = count / 10, OverrideEvents = count / 2, HistorySettlementVersions = 3,
                ReadWarmups = 2, ReadSamples = 10, WriteSamples = 1,
                Notes = "TestServer in-process; synthetic data; instrumentation overhead included; commands include JWT user lookup but exclude transaction open/commit; command duration ends when reader opens (excludes full materialization); read EXPLAIN separately in read-only transaction after samples, root rows are query output rows not all scanned rows; write commands never replayed; POST measured once, its p50/p95 are not statistical estimates; read p95 has only 10 samples; no production latency claim; schema created from EF model, not migration replay.",
                Results = results };
            await File.WriteAllTextAsync(Path.Combine(output, $"postgres-{count}.json"), JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

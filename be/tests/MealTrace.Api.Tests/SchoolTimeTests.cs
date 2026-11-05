using System.Net;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features.Calendar;
using MealTrace.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class SchoolTimeTests
{
    private sealed class AdvanceAfterEnrollmentUpdate(TestClock clock, DateTimeOffset afterSave) : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("UPDATE \"Enrollments\"", StringComparison.Ordinal))
            { Enabled = false; clock.Set(afterSave); }
            return ValueTask.FromResult(result);
        }
    }
    private static DateOnly FutureSchoolDay => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(10);
    private static DateTimeOffset At(DateOnly day, int hour, int minute) => new(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(7));

    [Fact]
    public async Task ParentDirectoryUsesConfiguredSchoolDateInsteadOfMachineDate()
    {
        var clock = new TestClock(); clock.Set(At(FutureSchoolDay, 6, 0));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, "parent@test.local", seed.Password);
        var children = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
        Assert.Contains(children.EnumerateArray(), x => x.GetProperty("studentId").GetGuid() == seed.PresentId);
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.NoContent)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(1, HttpStatusCode.BadRequest)]
    public async Task SameDayTransferClosesExactlyAtSchoolCutoff(int seconds, HttpStatusCode expected)
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 7, 30).AddSeconds(seconds));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync(); Guid childId; Guid target;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var room = new SchoolClass { Name = "Clock transfer", SchoolYear = "2026-2027" }; target = room.Id;
            var child = new Student { FullName = "Clock child", ClassId = seed.ClassId,
                Enrollments = [new() { ClassId = seed.ClassId, StartDate = date.AddDays(-2), RecordedAt = clock.GetUtcNow().AddDays(-2) }] };
            childId = child.Id; db.Classes.Add(room); db.Students.Add(child); await db.SaveChangesAsync();
        }
        await Authorize(client, seed.AdminEmail, seed.Password);
        var result = await client.PostAsJsonAsync($"/api/admin/students/{childId}/enrollments",
            new { classId = target, effectiveDate = date, reason = "School cutoff", revision = 0 });
        Assert.Equal(expected, result.StatusCode);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/students");
        Assert.Equal(date.AddDays(seconds < 0 ? 0 : 1).ToString("yyyy-MM-dd"), page.GetProperty("earliestChangeDate").GetString());
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var records = await database.Enrollments.Where(x => x.StudentId == childId).OrderBy(x => x.StartDate).ToListAsync();
        Assert.Equal(seconds < 0 ? 2 : 1, records.Count);
        if (seconds < 0)
        {
            Assert.Equal(clock.GetUtcNow(), records[0].EndRecordedAt);
            Assert.Equal(clock.GetUtcNow(), records[1].RecordedAt);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParentChangeAtExactCutoffCannotRewriteDecisionBeforeCutoff(bool cancel)
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 6, 0));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); var cutoff = At(date, 7, 30);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.CutoffAt = cutoff;
            var absence = await db.MealAbsences.SingleAsync();
            if (cancel) absence.CancelledAt = cutoff;
            else absence.ReportedAt = cutoff;
            await db.SaveChangesAsync();
        }
        clock.Set(cutoff.AddSeconds(1));
        await Authorize(client, seed.AdminEmail, seed.Password);
        var decisions = await client.GetFromJsonAsync<JsonElement>($"/api/meal-days/{seed.DayId}/decisions");
        var child = decisions.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.AbsentId);
        Assert.Equal(!cancel, child.GetProperty("willEat").GetBoolean());
    }

    [Fact]
    public async Task ParentDirectorySwitchesEnrollmentAtSchoolMidnightWhileUtcDateStaysTheSame()
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 23, 59));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var enrollment = await db.Enrollments.SingleAsync(x => x.StudentId == seed.PresentId);
            enrollment.EndDate = date.AddDays(1); await db.SaveChangesAsync();
            db.Enrollments.Add(new() { StudentId = seed.PresentId, ClassId = seed.ForeignClassId,
                StartDate = date.AddDays(1), RecordedAt = clock.GetUtcNow().AddDays(-1) });
            await db.SaveChangesAsync();
        }
        await Authorize(client, "parent@test.local", seed.Password);
        foreach (var (now, expectedClass) in new[] { (At(date, 23, 59), seed.ClassId), (At(date.AddDays(1), 0, 1), seed.ForeignClassId) })
        {
            clock.Set(now);
            var children = await client.GetFromJsonAsync<JsonElement>("/api/parent/students");
            var child = children.EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == seed.PresentId);
            Assert.Equal(expectedClass, child.GetProperty("classId").GetGuid());
        }
    }

    [Fact]
    public async Task NewStudentAndAutomaticEnrollmentUseInjectedClock()
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 6, 0));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await factory.SeedUsersAsync(); await Authorize(client, seed.AdminEmail, seed.Password);
        var response = await client.PostAsJsonAsync("/api/students", new { fullName = "Clock registration", classId = seed.ClassId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var childId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var automatic = new Student { FullName = "Automatic enrollment", ClassId = seed.ClassId };
        db.Students.Add(automatic); await db.SaveChangesAsync();
        foreach (var id in new[] { childId, automatic.Id })
        {
            var enrollment = await db.Enrollments.SingleAsync(x => x.StudentId == id);
            Assert.Equal(date, enrollment.StartDate); Assert.Equal(clock.GetUtcNow(), enrollment.RecordedAt);
        }
    }

    [Fact]
    public async Task AbsenceCanEndOnLastSchoolDayButCannotExtendIntoNextYear()
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 6, 0));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.AcademicYears.FindAsync("2026-2027"))!.EndDate = date; await db.SaveChangesAsync();
        }
        await Authorize(client, "parent@test.local", seed.Password);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/parent/absences",
            new { studentId = seed.PresentId, fromDate = date, toDate = date.AddDays(1), reason = "Beyond school year" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/parent/absences",
            new { studentId = seed.PresentId, fromDate = date, toDate = date, reason = "Last school day" })).StatusCode);
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.Conflict)]
    [InlineData(0, HttpStatusCode.OK)]
    [InlineData(1, HttpStatusCode.OK)]
    public async Task SettlementOpensAtCutoffAndUsesOneTimestampForAllSnapshots(int seconds, HttpStatusCode expected)
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 6, 0));
        using var factory = new AuthTestFactory(clock: clock); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); var cutoff = SchoolTime.Cutoff(date);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            (await db.MealDays.FindAsync(seed.DayId))!.CutoffAt = cutoff; await db.SaveChangesAsync();
        }
        await Authorize(client, seed.AdminEmail, seed.Password); clock.Set(cutoff.AddSeconds(seconds));
        Assert.Equal(expected, (await client.PostAsync($"/api/meal-days/{seed.DayId}/settle", null)).StatusCode);
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var snapshots = await database.PortionSettlements.ToListAsync();
        if (seconds < 0) Assert.Empty(snapshots);
        else
        {
            Assert.Equal(2, snapshots.Count);
            Assert.All(snapshots, x => Assert.Equal(clock.GetUtcNow(), x.SettledAt));
            Assert.Equal(clock.GetUtcNow(), (await database.MealDays.FindAsync(seed.DayId))!.SettledAt);
        }
    }

    [Fact]
    public async Task TransferRollsBackClosedEnrollmentWhenCutoffPassesDuringFirstSave()
    {
        var date = FutureSchoolDay; var clock = new TestClock(); clock.Set(At(date, 7, 29));
        var probe = new AdvanceAfterEnrollmentUpdate(clock, At(date, 7, 30));
        using var factory = new AuthTestFactory(clock: clock, commands: probe); using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock); await Authorize(client, seed.AdminEmail, seed.Password);
        probe.Enabled = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/admin/students/{seed.PresentId}/enrollments",
            new { classId = seed.ForeignClassId, effectiveDate = date, reason = "Clock crosses during save", revision = 0 })).StatusCode);
        Assert.False(probe.Enabled); // Clock moved after the first UPDATE, not at initial validation.
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var child = await db.Students.Include(x => x.Enrollments).SingleAsync(x => x.Id == seed.PresentId);
        Assert.Equal(seed.ClassId, child.ClassId); Assert.Equal(0, child.Revision);
        var enrollment = Assert.Single(child.Enrollments);
        Assert.Null(enrollment.EndDate); Assert.Null(enrollment.EndRecordedAt);
    }

    [Fact]
    public void MealSpecificCutoffCalculatesCorrectTimestampsForBreakfastAndDaytimeMeals()
    {
        var date = new DateOnly(2026, 10, 25);

        // Breakfast: 21:00 previous day (2026-10-24 21:00:00 +07:00 -> 2026-10-24 14:00:00Z)
        var breakfastCutoff = SchoolTime.Cutoff(date, "Breakfast");
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero), breakfastCutoff);
        Assert.Equal(TimeSpan.Zero, breakfastCutoff.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 21, 0, 0, SchoolTime.Offset), breakfastCutoff.ToOffset(SchoolTime.Offset));

        // Vietnamese aliases for Breakfast
        Assert.Equal(breakfastCutoff, SchoolTime.Cutoff(date, "Sáng"));
        Assert.Equal(breakfastCutoff, SchoolTime.Cutoff(date, "Bữa sáng"));
        Assert.Equal(breakfastCutoff, SchoolTime.Cutoff(date, "breakfast"));

        // Lunch: 07:00 same day (2026-10-25 07:00:00 +07:00 -> 2026-10-25 00:00:00Z)
        var lunchCutoff = SchoolTime.Cutoff(date, "Lunch");
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), lunchCutoff);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 7, 0, 0, SchoolTime.Offset), lunchCutoff.ToOffset(SchoolTime.Offset));
        Assert.Equal(lunchCutoff, SchoolTime.Cutoff(date, "Trưa"));
        Assert.Equal(lunchCutoff, SchoolTime.Cutoff(date, "Bữa trưa"));

        // Snack: 07:00 same day (2026-10-25 07:00:00 +07:00 -> 2026-10-25 00:00:00Z)
        var snackCutoff = SchoolTime.Cutoff(date, "Snack");
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), snackCutoff);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 7, 0, 0, SchoolTime.Offset), snackCutoff.ToOffset(SchoolTime.Offset));
        Assert.Equal(snackCutoff, SchoolTime.Cutoff(date, "Xế"));
        Assert.Equal(snackCutoff, SchoolTime.Cutoff(date, "Bữa xế"));
        Assert.Equal(snackCutoff, SchoolTime.Cutoff(date, "Bữa phụ"));

        // Fallback when mealType is null or whitespace -> 07:30:00 +07:00 (2026-10-25 00:30:00Z)
        var fallbackNullCutoff = SchoolTime.Cutoff(date, null);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), fallbackNullCutoff);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 7, 30, 0, SchoolTime.Offset), fallbackNullCutoff.ToOffset(SchoolTime.Offset));
        Assert.Equal(fallbackNullCutoff, SchoolTime.Cutoff(date, ""));
        Assert.Equal(fallbackNullCutoff, SchoolTime.Cutoff(date, "   "));

        // Overload Cutoff(date) matches Cutoff(date, null)
        Assert.Equal(fallbackNullCutoff, SchoolTime.Cutoff(date));
    }

    [Fact]
    public void MealCalendarServiceCutoffDelegatesToSchoolTimeWithMealType()
    {
        var date = new DateOnly(2026, 11, 10);

        // Breakfast: 21:00 D-1 (2026-11-09 21:00:00 +07:00 -> 2026-11-09 14:00:00Z)
        var breakfastCutoff = MealCalendarService.Cutoff(date, "Breakfast");
        Assert.Equal(SchoolTime.Cutoff(date, "Breakfast"), breakfastCutoff);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 14, 0, 0, TimeSpan.Zero), breakfastCutoff);
        Assert.Equal(breakfastCutoff, MealCalendarService.Cutoff(date, "Sáng"));
        Assert.Equal(breakfastCutoff, MealCalendarService.Cutoff(date, "Bữa sáng"));

        // Lunch: 07:00 D (2026-11-10 07:00:00 +07:00 -> 2026-11-10 00:00:00Z)
        var lunchCutoff = MealCalendarService.Cutoff(date, "Lunch");
        Assert.Equal(SchoolTime.Cutoff(date, "Lunch"), lunchCutoff);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero), lunchCutoff);
        Assert.Equal(lunchCutoff, MealCalendarService.Cutoff(date, "Trưa"));
        Assert.Equal(lunchCutoff, MealCalendarService.Cutoff(date, "Bữa trưa"));

        // Fallback overload Cutoff(date) matches Cutoff(date, null) -> 07:30 +07:00
        Assert.Equal(MealCalendarService.Cutoff(date, null), MealCalendarService.Cutoff(date));
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 0, 30, 0, TimeSpan.Zero), MealCalendarService.Cutoff(date));
    }

    [Fact]
    public async Task CreateMealDayAsyncAssignsMealSpecificCutoffAt()
    {
        var dateD = FutureSchoolDay;
        while (dateD.DayOfWeek != DayOfWeek.Tuesday)
            dateD = dateD.AddDays(1);
        var dateDMinus1 = dateD.AddDays(-1);

        // At 20:00 D-1 (before Breakfast 21:00 cutoff and Lunch 07:00 cutoff)
        var clock = new TestClock();
        clock.Set(At(dateDMinus1, 20, 0));

        using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var year = (await db.AcademicYears.FindAsync("2026-2027"))!;
            year.StartDate = dateDMinus1.AddDays(-7);
            year.EndDate = dateD.AddDays(30);
            await db.SaveChangesAsync();
        }

        var scheduleResponse = await client.PutAsJsonAsync("/api/admin/meal-calendar/2026-2027/schedule", new
        {
            weekdays = new[] { 1, 2, 3, 4, 5 },
            mealTypes = new[] { "Breakfast", "Lunch" },
            expectedRevision = 0,
            reason = "Weekly schedule"
        });
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);

        // Create Breakfast for dateD
        var createBreakfast = await client.PostAsJsonAsync("/api/meal-days", new
        {
            date = dateD,
            mealType = "Breakfast",
            schoolYear = "2026-2027"
        });
        Assert.Equal(HttpStatusCode.Created, createBreakfast.StatusCode);
        var breakfastJson = await createBreakfast.Content.ReadFromJsonAsync<JsonElement>();
        var expectedBreakfastCutoff = SchoolTime.Cutoff(dateD, "Breakfast");
        Assert.Equal(expectedBreakfastCutoff, breakfastJson.GetProperty("cutoffAt").GetDateTimeOffset());

        // Create Lunch for dateD
        var createLunch = await client.PostAsJsonAsync("/api/meal-days", new
        {
            date = dateD,
            mealType = "Lunch",
            schoolYear = "2026-2027"
        });
        Assert.Equal(HttpStatusCode.Created, createLunch.StatusCode);
        var lunchJson = await createLunch.Content.ReadFromJsonAsync<JsonElement>();
        var expectedLunchCutoff = SchoolTime.Cutoff(dateD, "Lunch");
        Assert.Equal(expectedLunchCutoff, lunchJson.GetProperty("cutoffAt").GetDateTimeOffset());

        // Verify in DB
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var savedBreakfast = await verifyDb.MealDays.SingleAsync(x => x.Date == dateD && x.MealType == "Breakfast");
        var savedLunch = await verifyDb.MealDays.SingleAsync(x => x.Date == dateD && x.MealType == "Lunch");
        Assert.Equal(expectedBreakfastCutoff, savedBreakfast.CutoffAt);
        Assert.Equal(expectedLunchCutoff, savedLunch.CutoffAt);
    }

    [Fact]
    public async Task EveningDMinus1BlocksBreakfastWhileLunchCanBeCreatedAndBuildPlanDistinguishes()
    {
        var dateD = FutureSchoolDay;
        while (dateD.DayOfWeek != DayOfWeek.Tuesday)
            dateD = dateD.AddDays(1);
        var dateDMinus1 = dateD.AddDays(-1);

        // At 21:30 D-1 (30 minutes past Breakfast cutoff of 21:00 D-1, but before Lunch cutoff of 07:00 D)
        var clock = new TestClock();
        clock.Set(At(dateDMinus1, 21, 30));

        using var factory = new AuthTestFactory(clock: clock);
        using var client = factory.CreateClient();
        var seed = await SeedScenario(factory, clock);
        await Authorize(client, seed.AdminEmail, seed.Password);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var year = (await db.AcademicYears.FindAsync("2026-2027"))!;
            year.StartDate = dateDMinus1.AddDays(-7);
            year.EndDate = dateD.AddDays(30);
            await db.SaveChangesAsync();
        }

        var scheduleResponse = await client.PutAsJsonAsync("/api/admin/meal-calendar/2026-2027/schedule", new
        {
            weekdays = new[] { 1, 2, 3, 4, 5 },
            mealTypes = new[] { "Breakfast", "Lunch" },
            expectedRevision = 0,
            reason = "Weekly schedule"
        });
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);

        // MealCalendarService.BuildPlan via preview: Breakfast is LOCKED, Lunch is CREATE
        var previewResponse = await client.PostAsJsonAsync("/api/admin/meal-calendar/2026-2027/preview", new
        {
            from = dateD,
            to = dateD,
            expectedRevision = 1
        });
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = preview.GetProperty("items").EnumerateArray().ToList();

        var breakfastItem = items.Single(x => x.GetProperty("mealType").GetString() == "Breakfast");
        Assert.Equal("LOCKED", breakfastItem.GetProperty("action").GetString());

        var lunchItem = items.Single(x => x.GetProperty("mealType").GetString() == "Lunch");
        Assert.Equal("CREATE", lunchItem.GetProperty("action").GetString());

        // WorkflowService.CreateMealDayAsync: Creating Breakfast is blocked with 409 Conflict
        var createBreakfast = await client.PostAsJsonAsync("/api/meal-days", new
        {
            date = dateD,
            mealType = "Breakfast",
            schoolYear = "2026-2027"
        });
        Assert.Equal(HttpStatusCode.Conflict, createBreakfast.StatusCode);

        // Creating Lunch succeeds with 201 Created and CutoffAt = 07:00 D
        var createLunch = await client.PostAsJsonAsync("/api/meal-days", new
        {
            date = dateD,
            mealType = "Lunch",
            schoolYear = "2026-2027"
        });
        Assert.Equal(HttpStatusCode.Created, createLunch.StatusCode);
        var lunchJson = await createLunch.Content.ReadFromJsonAsync<JsonElement>();
        var expectedLunchCutoff = SchoolTime.Cutoff(dateD, "Lunch");
        Assert.Equal(expectedLunchCutoff, lunchJson.GetProperty("cutoffAt").GetDateTimeOffset());
    }
}

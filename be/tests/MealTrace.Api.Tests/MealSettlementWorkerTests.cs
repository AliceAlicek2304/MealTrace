using MealTrace.Api.Workers;
using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static MealTrace.Api.Tests.AuthenticationTests;
using static MealTrace.Api.Tests.MealExceptionTests;

namespace MealTrace.Api.Tests;

public sealed class MealSettlementWorkerTests
{
    private static DateOnly FutureSchoolDay => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(10);
    private static DateTimeOffset At(DateOnly day, int hour, int minute) =>
        new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(7)).ToUniversalTime();

    [Fact]
    public async Task Worker_SettlesMealDay_WhenCutoffReached()
    {
        var clock = new TestClock();
        clock.Set(At(FutureSchoolDay, 6, 0));
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);

        var worker = new MealSettlementWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MealSettlementWorker>.Instance,
            clock);

        var cutoff = At(FutureSchoolDay, 7, 0);
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(seed.DayId);
            day!.CutoffAt = cutoff;
            await db.SaveChangesAsync();
        }

        // Trước cutoff: worker quét không có bữa nào cần chốt
        var settledBefore = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(0, settledBefore);

        // Chạy qua cutoff 1 giây
        clock.Set(cutoff.AddSeconds(1));
        var settledAfter = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(1, settledAfter);

        // Kiểm tra database: bữa ăn đã được chốt và có snapshot với SettledBy = "system"
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.Include(d => d.Settlements).SingleAsync(d => d.Id == seed.DayId);
            Assert.NotNull(day.SettledAt);
            Assert.NotEmpty(day.Settlements);
            Assert.All(day.Settlements, s => Assert.Equal("system", s.SettledBy));
        }
    }

    [Fact]
    public async Task Worker_IsIdempotent_DoesNotDuplicateSnapshotsOnAlreadySettledMealDay()
    {
        var clock = new TestClock();
        clock.Set(At(FutureSchoolDay, 6, 0));
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);

        var worker = new MealSettlementWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MealSettlementWorker>.Instance,
            clock);

        var cutoff = At(FutureSchoolDay, 7, 0);
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(seed.DayId);
            day!.CutoffAt = cutoff;
            await db.SaveChangesAsync();
        }

        // Chốt lần đầu khi đến giờ cutoff
        clock.Set(cutoff.AddSeconds(1));
        var firstRun = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(1, firstRun);

        int initialSettlementCount;
        DateTimeOffset? initialSettledAt;
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.Include(d => d.Settlements).SingleAsync(d => d.Id == seed.DayId);
            initialSettledAt = day.SettledAt;
            initialSettlementCount = day.Settlements.Count;
            Assert.NotNull(initialSettledAt);
            Assert.True(initialSettlementCount > 0);
        }

        // Chạy lại worker trên bữa đã chốt
        var secondRun = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(0, secondRun);

        // Đảm bảo không sinh trùng snapshots và SettledAt không đổi
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.Include(d => d.Settlements).SingleAsync(d => d.Id == seed.DayId);
            Assert.Equal(initialSettledAt, day.SettledAt);
            Assert.Equal(initialSettlementCount, day.Settlements.Count);
        }
    }

    [Fact]
    public async Task Worker_DelayedRecovery_AppliesAbsencesBeforeCutoff_AndIgnoresAbsencesAfterCutoff()
    {
        var clock = new TestClock();
        clock.Set(At(FutureSchoolDay, 6, 0));
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);

        var cutoff = At(FutureSchoolDay, 7, 0);

        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(seed.DayId);
            day!.CutoffAt = cutoff;

            var parentUser = await db.Users.FirstAsync(u => u.Email == "parent@test.local");
            var lateAbsence = new MealAbsence
            {
                StudentId = seed.PresentId,
                ReportedByUserId = parentUser.Id,
                FromDate = FutureSchoolDay,
                ToDate = FutureSchoolDay,
                Reason = "Late absence after cutoff",
                ReportedAt = cutoff.AddMinutes(15)
            };
            db.MealAbsences.Add(lateAbsence);
            await db.SaveChangesAsync();
        }

        // Server khởi động trễ 30 phút sau Cutoff (07:30)
        clock.Set(At(FutureSchoolDay, 7, 30));

        var worker = new MealSettlementWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MealSettlementWorker>.Instance,
            clock);

        // Worker chốt bù (delayed recovery)
        var settled = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(1, settled);

        // Kiểm tra kết quả chốt
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(seed.DayId);
            Assert.NotNull(day!.SettledAt);

            var settlements = await db.PortionSettlements
                .Include(s => s.Students)
                .Where(s => s.MealDayId == seed.DayId && s.ClassId == seed.ClassId)
                .ToListAsync();

            var settlement = Assert.Single(settlements);
            Assert.Equal("system", settlement.SettledBy);

            // Học sinh absent có đơn trước cutoff -> vắng (không tính vào suất ăn).
            // Học sinh present có đơn sau cutoff -> bị từ chối theo BE2-BR05 -> vẫn được tính ăn!
            Assert.Equal(1, settlement.Count);
            var student = Assert.Single(settlement.Students);
            Assert.Equal(seed.PresentId, student.StudentId);
        }
    }

    [Fact]
    public async Task Worker_SkipsCancelledMealDay()
    {
        var clock = new TestClock();
        clock.Set(At(FutureSchoolDay, 6, 0));
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);

        var cutoff = At(FutureSchoolDay, 7, 0);
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(seed.DayId);
            day!.CutoffAt = cutoff;
            day.IsCancelled = true;
            day.CancellationReason = "Truong nghi dot xuat";
            await db.SaveChangesAsync();
        }

        clock.Set(cutoff.AddMinutes(10));

        var worker = new MealSettlementWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MealSettlementWorker>.Instance,
            clock);

        var settled = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(0, settled);

        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.Include(d => d.Settlements).SingleAsync(d => d.Id == seed.DayId);
            Assert.Null(day.SettledAt);
            Assert.Empty(day.Settlements);
        }
    }

    [Fact]
    public async Task Worker_SkipsMealDay_WhenCutoffNotReachedYet()
    {
        var clock = new TestClock();
        clock.Set(At(FutureSchoolDay, 6, 0));
        using var factory = new AuthTestFactory(clock: clock);
        var seed = await SeedScenario(factory, clock);

        var cutoff = At(FutureSchoolDay, 7, 0);
        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.FindAsync(seed.DayId);
            day!.CutoffAt = cutoff;
            await db.SaveChangesAsync();
        }

        // Hiện tại là 06:30, trước cutoff 07:00
        clock.Set(At(FutureSchoolDay, 6, 30));

        var worker = new MealSettlementWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MealSettlementWorker>.Instance,
            clock);

        var settled = await worker.ProcessPendingSettlementsAsync(CancellationToken.None);
        Assert.Equal(0, settled);

        using (var dbScope = factory.Services.CreateScope())
        {
            var db = dbScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var day = await db.MealDays.Include(d => d.Settlements).SingleAsync(d => d.Id == seed.DayId);
            Assert.Null(day.SettledAt);
            Assert.Empty(day.Settlements);
        }
    }
}

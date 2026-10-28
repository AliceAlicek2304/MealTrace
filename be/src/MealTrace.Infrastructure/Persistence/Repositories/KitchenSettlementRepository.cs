using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Domain.Time;

namespace MealTrace.Infrastructure.Persistence.Repositories;

public class KitchenSettlementRepository : IKitchenSettlementRepository
{
    private readonly MealTraceDbContext _db;
    private readonly TimeProvider _clock;

    public KitchenSettlementRepository(MealTraceDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<MealDay?> LockMealDayAsync(Guid id, CancellationToken ct) => _db.LockMealDayAsync(id, ct);

    public Task<int> CountSettledMealDaysAsync(DateOnly? date, CancellationToken ct) => PersistenceErrors.ExecuteAsync(() =>
        _db.MealDays.CountAsync(d => d.SettledAt != null && !d.IsCancelled && (!date.HasValue || d.Date == date.Value), ct));

    public async Task<List<MealDay>> ListSettledMealDaysAsync(DateOnly? date, int skip, int take, CancellationToken ct)
    {
        var query = _db.MealDays
            .Where(d => d.SettledAt != null && !d.IsCancelled);

        if (date.HasValue)
        {
            query = query.Where(d => d.Date == date.Value);
        }

        var today = SchoolTime.Today(_clock.GetUtcNow());
        return await query
            .OrderByDescending(d => d.Date == today)
            .ThenByDescending(d => d.Date)
            .ThenByDescending(d => d.CutoffAt)
            .ThenBy(d => d.Id)
            .Skip(skip).Take(take)
            .Include(d => d.Settlements)
            .Include(d => d.Acknowledgements)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<MealDay?> FindSettledMealDayWithDetailsAsync(Guid mealDayId, CancellationToken ct)
    {
        return await _db.MealDays
            .Include(d => d.Settlements)
                .ThenInclude(s => s.Students)
            .Include(d => d.Acknowledgements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == mealDayId, ct);
    }

    public async Task<string> GetActorNameAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Email, u.UserName })
            .FirstOrDefaultAsync(ct);

        return user?.FullName ?? user?.UserName ?? user?.Email ?? "Nhân viên bếp";
    }

    public void AddAcknowledgement(SettlementAcknowledgement acknowledgement)
    {
        _db.SettlementAcknowledgements.Add(acknowledgement);
    }

    public async Task<List<SettlementAcknowledgement>> ListAcknowledgementsAsync(Guid mealDayId, CancellationToken ct)
    {
        return await _db.SettlementAcknowledgements
            .Where(a => a.MealDayId == mealDayId)
            .OrderByDescending(a => a.AcknowledgedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}

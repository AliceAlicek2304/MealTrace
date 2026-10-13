using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

public class KitchenSettlementRepository : IKitchenSettlementRepository
{
    private readonly MealTraceDbContext _db;

    public KitchenSettlementRepository(MealTraceDbContext db)
    {
        _db = db;
    }

    public async Task<List<MealDay>> ListSettledMealDaysAsync(DateOnly? date, CancellationToken ct)
    {
        var query = _db.MealDays
            .Where(d => d.SettledAt != null);

        if (date.HasValue)
        {
            query = query.Where(d => d.Date == date.Value);
        }

        return await query
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.CutoffAt)
            .Include(d => d.Settlements)
                .ThenInclude(s => s.Students)
            .Include(d => d.Acknowledgements)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<MealDay?> FindSettledMealDayWithDetailsAsync(Guid mealDayId, CancellationToken ct)
    {
        return await _db.MealDays
            .Include(d => d.Settlements)
                .ThenInclude(s => s.Students)
            .Include(d => d.Acknowledgements)
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

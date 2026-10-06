using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class PortionRepository(MealTraceDbContext db) : IPortionRepository
{
    public async Task<List<LatestSettlementRow>> ListLatestSettlementsAsync(MealDay day, Guid[]? allowedClasses, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var source = db.PortionSettlements.AsNoTracking().Where(x => x.MealDayId == day.Id && x.ClassId != null);
            if (allowedClasses is not null)
                source = source.Where(x => allowedClasses.Contains(x.ClassId!.Value));
            return await source.Where(x => !source.Any(newer => newer.ClassId == x.ClassId && newer.Version > x.Version)).Select(x => new LatestSettlementRow
            {
                Id = x.Id,
                ClassId = x.ClassId!.Value,
                ClassName = x.ClassName ?? x.Class!.Name,
                SchoolYear = x.Class != null ? x.Class.SchoolYear : day.SchoolYear ?? "",
                Version = x.Version,
                Count = x.Count,
                OriginalCount = source.Where(original => original.ClassId == x.ClassId).OrderBy(original => original.Version).Select(original => original.Count).First()
            }).ToListAsync(ct);
        });
    }
    public async Task<List<SettlementStudentRow>> ListSettlementStudentsAsync(Guid[] ids, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.SettlementStudents.AsNoTracking().Where(x => ids.Contains(x.PortionSettlementId)).OrderBy(x => x.StudentId).Select(x => new SettlementStudentRow
            {
                PortionSettlementId = x.PortionSettlementId,
                StudentId = x.StudentId,
                StudentName = x.StudentName
            }).ToListAsync(ct);
        });
    }
}

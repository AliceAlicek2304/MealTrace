using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

internal sealed record ClassPortions(Guid ClassId, string ClassName, string SchoolYear,
    List<Guid> StudentIds, List<string> StudentNames, List<Guid> AbsentStudentIds, bool IsSettled,
    Guid? SettlementId = null, int Version = 0, int? OriginalCount = null, int? Count = null);

internal static class PortionService
{
    public static async Task<List<ClassPortions>> ReadAsync(MealTraceDbContext db, MealDay day, DateTimeOffset now,
        Guid[]? allowedClasses, CancellationToken ct)
    {
        if (allowedClasses is { Length: 0 }) return [];
        var source = db.PortionSettlements.AsNoTracking().Where(x => x.MealDayId == day.Id && x.ClassId != null);
        if (allowedClasses is not null) source = source.Where(x => allowedClasses.Contains(x.ClassId!.Value));
        var latest = await source.Where(x => !source.Any(newer => newer.ClassId == x.ClassId && newer.Version > x.Version))
            .Select(x => new { x.Id, ClassId = x.ClassId!.Value, ClassName = x.ClassName ?? x.Class!.Name,
                SchoolYear = x.Class != null ? x.Class.SchoolYear : day.SchoolYear ?? "", x.Version, x.Count,
                OriginalCount = source.Where(original => original.ClassId == x.ClassId).OrderBy(original => original.Version)
                    .Select(original => original.Count).First() }).ToListAsync(ct);
        var snapshots = new Dictionary<Guid, ClassPortions>();
        if (latest.Count > 0)
        {
            var ids = latest.Select(x => x.Id).ToArray();
            var students = await db.SettlementStudents.AsNoTracking().Where(x => ids.Contains(x.PortionSettlementId))
                .OrderBy(x => x.StudentId).Select(x => new { x.PortionSettlementId, x.StudentId, x.StudentName }).ToListAsync(ct);
            var roster = students.ToLookup(x => x.PortionSettlementId);
            foreach (var row in latest)
                snapshots.Add(row.ClassId, new(row.ClassId, row.ClassName, row.SchoolYear,
                    roster[row.Id].Select(x => x.StudentId).ToList(), roster[row.Id].Select(x => x.StudentName).ToList(), [], true,
                    row.Id, row.Version, row.OriginalCount, row.Count));
        }
        if (day.SettledAt is not null) return snapshots.Values.ToList();
        var decisions = await MealDecisionService.ReadAsync(db, day, now, allowedClasses, ct);
        return decisions.GroupBy(x => new { x.ClassId, x.ClassName, x.SchoolYear }).OrderBy(x => x.Key.ClassName).Select(group =>
        {
            if (snapshots.TryGetValue(group.Key.ClassId, out var snapshot))
                return snapshot with { SettlementId = null, Version = 0, OriginalCount = null, Count = null };
            var members = group.OrderBy(x => x.FullName).ThenBy(x => x.StudentId).ToList();
            var eating = members.Where(x => x.WillEat).ToList();
            return new ClassPortions(group.Key.ClassId, group.Key.ClassName, group.Key.SchoolYear,
                eating.Select(x => x.StudentId).ToList(), eating.Select(x => x.FullName).ToList(),
                members.Where(x => !x.WillEat).Select(x => x.StudentId).ToList(), false);
        }).ToList();
    }

    public static List<PortionSettlement> CreateSnapshots(MealDay day, List<MealDecision> decisions, string actor, DateTimeOffset settledAt) =>
        decisions.GroupBy(x => new { x.ClassId, x.ClassName }).OrderBy(x => x.Key.ClassName).Select(group =>
        {
            var eating = group.Where(x => x.WillEat).OrderBy(x => x.FullName).ThenBy(x => x.StudentId).ToList();
            return new PortionSettlement { MealDayId = day.Id, ClassId = group.Key.ClassId, ClassName = group.Key.ClassName,
                Count = eating.Count, CutoffAt = day.CutoffAt, SettledAt = settledAt, SettledBy = actor,
                Students = eating.Select(x => new SettlementStudent { StudentId = x.StudentId, StudentName = x.FullName }).ToList(),
                Decisions = group.Select(x => new SettlementDecision { StudentId = x.StudentId, StudentName = x.FullName,
                    StudentCode = x.StudentCode, WillEat = x.WillEat, EnrollmentId = x.EnrollmentId,
                    AbsenceId = x.AbsenceId, ExceptionId = x.LatestEventId, Source = x.Source }).ToList() };
        }).ToList();
}

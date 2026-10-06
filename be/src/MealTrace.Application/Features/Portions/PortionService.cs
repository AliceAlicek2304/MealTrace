using MealTrace.Application.Features.Meals;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Models.Persistence;
using MealTrace.Application.Dtos.Meals;
using MealTrace.Application.Dtos.Portions;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Portions;

public sealed class PortionService(IPortionRepository repository, MealDecisionService decisionService)
{
    public async Task<List<ClassPortions>> ReadAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, CancellationToken ct)
    {
        if (allowedClasses is { Length: 0 }) return [];
        var latest = await repository.ListLatestSettlementsAsync(day, allowedClasses, ct);
        var snapshots = new Dictionary<Guid, ClassPortions>();
        if (latest.Count > 0)
        {
            var ids = latest.Select(x => x.Id).ToArray();
            var students = await repository.ListSettlementStudentsAsync(ids, ct);
            var roster = students.ToLookup(x => x.PortionSettlementId);
            foreach (var row in latest)
                snapshots.Add(row.ClassId, new(row.ClassId, row.ClassName, row.SchoolYear,
                    roster[row.Id].Select(x => x.StudentId).ToList(), roster[row.Id].Select(x => x.StudentName).ToList(), [], true,
                    row.Id, row.Version, row.OriginalCount, row.Count, row.KitchenAdjustment));
        }
        if (day.SettledAt is not null) return snapshots.Values.ToList();
        var decisions = await decisionService.ReadAsync(day, now, allowedClasses, ct);
        return decisions.GroupBy(x => new PortionClassKey
        {
            ClassId = x.ClassId,
            ClassName = x.ClassName,
            SchoolYear = x.SchoolYear
        })
            .OrderBy(x => x.Key.ClassName)
            .Select(group =>
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
        decisions.GroupBy(x => new SettlementClassKey
        {
            ClassId = x.ClassId,
            ClassName = x.ClassName
        })
            .OrderBy(x => x.Key.ClassName)
            .Select(group =>
            {
                var eating = group.Where(x => x.WillEat).OrderBy(x => x.FullName).ThenBy(x => x.StudentId).ToList();
                return new PortionSettlement
                {
                    MealDayId = day.Id,
                    ClassId = group.Key.ClassId,
                    ClassName = group.Key.ClassName,
                    Count = eating.Count,
                    CutoffAt = day.CutoffAt,
                    SettledAt = settledAt,
                    SettledBy = actor,
                    Students = eating.Select(x => new SettlementStudent
                    {
                        StudentId = x.StudentId,
                        StudentName = x.FullName
                    }).ToList(),
                    Decisions = group.Select(x => new SettlementDecision
                    {
                        StudentId = x.StudentId,
                        StudentName = x.FullName,
                        StudentCode = x.StudentCode,
                        WillEat = x.WillEat,
                        EnrollmentId = x.EnrollmentId,
                        AbsenceId = x.AbsenceId,
                        ExceptionId = x.LatestEventId,
                        Source = x.Source
                    }).ToList()
                };
            }).ToList();
}

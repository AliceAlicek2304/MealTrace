using MealTrace.Application.Dtos.Meals;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class MealDecisionRepository(MealTraceDbContext db) : IMealDecisionRepository
{
    public async Task<List<MealEnrollmentMember>> ListEligibleMembersAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await Members(db, Eligible(db, day, now, allowedClasses)).ToListAsync(ct);
        });
    }
    public async Task<List<Room>> ListEligibleClassesAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var eligible = Eligible(db, day, now, allowedClasses);
            return await eligible.Select(x => new EligibleClassRow
            {
                ClassId = x.ClassId,
                Name = x.Class.Name
            }).Distinct().OrderBy(x => x.Name).ThenBy(x => x.ClassId).Select(x => new Room(x.ClassId, x.Name)).ToListAsync(ct);
        });
    }
    public async Task<int> CountEligibleMembersAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, Guid? classId, string? search, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var eligible = Eligible(db, day, now, allowedClasses);
            var filtered = classId.HasValue ? eligible.Where(x => x.ClassId == classId.Value) : eligible;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                // Contains treats SQL wildcard characters literally rather than interpolating a LIKE pattern.
                filtered = filtered.Where(x => x.Student.FullName.ToLower().Contains(term) || x.Student.StudentCode.ToLower().Contains(term));
            }
            return await filtered.CountAsync(ct);
        });
    }
    public async Task<List<MealEnrollmentMember>> SearchEligibleMembersAsync(MealDay day, DateTimeOffset now, Guid[]? allowedClasses, Guid? classId, string? search, int page, int pageSize, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var eligible = Eligible(db, day, now, allowedClasses);
            var filtered = classId.HasValue ? eligible.Where(x => x.ClassId == classId.Value) : eligible;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                // Contains treats SQL wildcard characters literally rather than interpolating a LIKE pattern.
                filtered = filtered.Where(x => x.Student.FullName.ToLower().Contains(term) || x.Student.StudentCode.ToLower().Contains(term));
            }
            return await Members(db, filtered.OrderBy(x => x.Class.Name).ThenBy(x => x.Student.FullName).ThenBy(x => x.StudentId).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        });
    }
    public async Task<List<MealDecisionEvidenceRow>> ListDecisionEvidenceAsync(MealDay day, DateTimeOffset asOf, List<MealEnrollmentMember> members, Guid[] ids, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var dated = db.MealAbsences.AsNoTracking().Where(x => x.FromDate <= day.Date && x.ToDate >= day.Date && x.ReportedAt <= asOf && x.ReportedAt < day.CutoffAt && (x.CancelledAt == null || x.CancelledAt > asOf || x.CancelledAt >= day.CutoffAt));
            IQueryable<MealAbsence>? relevant = null;
            foreach (var group in members.GroupBy(x => x.SchoolYear))
            {
                var code = group.Key;
                var yearIds = group.Select(x => x.StudentId).ToArray();
                var query = dated.Where(x => yearIds.Contains(x.StudentId) && (x.SchoolYear == null || x.SchoolYear == code));
                var year = group.First();
                if (year.YearStart is DateOnly start && year.YearEnd is DateOnly end)
                {
                    if (day.Date < start || day.Date > end)
                        continue;
                    query = query.Where(x => x.SchoolYear != null || (x.FromDate >= start && x.FromDate <= end));
                }

                relevant = relevant is null ? query : relevant.Concat(query);
            }
            var validEvents = db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == day.Id && x.RecordedAt <= asOf && x.RecordedAt < day.CutoffAt);
            // Unique (MealDayId, StudentId, Sequence) guarantees one latest valid event per child.
            var events = validEvents.Where(x => ids.Contains(x.StudentId) && !validEvents.Any(newer => newer.StudentId == x.StudentId && newer.Sequence > x.Sequence)).Select(x => new MealDecisionEvidenceRow
            {
                Kind = 1,
                StudentId = x.StudentId,
                Id = x.Id,
                RecordedAt = x.RecordedAt,
                WillEat = x.WillEat,
                Reason = x.Reason
            });
            var queryEvidence = relevant is null ? events : events.Concat(relevant.Select(x => new MealDecisionEvidenceRow
            {
                Kind = 0,
                StudentId = x.StudentId,
                Id = x.Id,
                RecordedAt = x.ReportedAt,
                WillEat = (bool?)null,
                Reason = (string?)null
            }));
            return await queryEvidence.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        });
    }
    private static IQueryable<Enrollment> Eligible(MealTraceDbContext db, MealDay day, DateTimeOffset now, Guid[]? allowedClasses)
    {
        var asOf = now < day.CutoffAt ? now : day.CutoffAt;
        var query = EnrollmentQueries.OnDate(db, day.Date).Where(x => !day.IsCancelled &&
            x.RecordedAt <= asOf && x.RecordedAt < day.CutoffAt &&
            (day.SchoolYear == null || x.Class.SchoolYear == day.SchoolYear));
        return allowedClasses is null ? query : query.Where(x => allowedClasses.Contains(x.ClassId));
    }

    private static IQueryable<MealEnrollmentMember> Members(MealTraceDbContext db, IQueryable<Enrollment> query) =>
        from member in query
        join year in db.AcademicYears.AsNoTracking() on member.Class.SchoolYear equals year.Code into years
        from year in years.DefaultIfEmpty()
        select new MealEnrollmentMember(member.StudentId, member.Student.StudentCode, member.Student.FullName, member.Id,
            member.ClassId, member.Class.Name, member.Class.SchoolYear,
            year == null ? null : year.StartDate, year == null ? null : year.EndDate);

}

using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;

namespace MealTrace.Application.Features;


internal static class MealDecisionService
{
    private sealed record Member(Guid StudentId, string StudentCode, string FullName, Guid EnrollmentId,
        Guid ClassId, string ClassName, string SchoolYear, DateOnly? YearStart, DateOnly? YearEnd);

    public static string Action(bool? willEat) => willEat switch { true => "EAT", false => "ABSENT", null => "DEFAULT" };

    private static IQueryable<Enrollment> Eligible(IMealTraceData db, MealDay day, DateTimeOffset now, Guid[]? allowedClasses)
    {
        var asOf = now < day.CutoffAt ? now : day.CutoffAt;
        var query = StudentAdministrationUseCases.OnDate(db, day.Date).Where(x => !day.IsCancelled &&
            x.RecordedAt <= asOf && x.RecordedAt < day.CutoffAt &&
            (day.SchoolYear == null || x.Class.SchoolYear == day.SchoolYear));
        return allowedClasses is null ? query : query.Where(x => allowedClasses.Contains(x.ClassId));
    }

    private static IQueryable<Member> Members(IMealTraceData db, IQueryable<Enrollment> query) =>
        from member in query
        join year in db.AcademicYears.AsNoTracking(db.Queries) on member.Class.SchoolYear equals year.Code into years
        from year in years.DefaultIfEmpty()
        select new Member(member.StudentId, member.Student.StudentCode, member.Student.FullName, member.Id,
            member.ClassId, member.Class.Name, member.Class.SchoolYear,
            year == null ? null : year.StartDate, year == null ? null : year.EndDate);

    public static async Task<List<MealDecision>> ReadAsync(IMealTraceData db, MealDay day, DateTimeOffset now,
        Guid[]? allowedClasses = null, CancellationToken ct = default)
    {
        if (day.IsCancelled || allowedClasses is { Length: 0 }) return [];
        var members = await Members(db, Eligible(db, day, now, allowedClasses)).ToListAsync(db.Queries, ct);
        return await ResolveAsync(db, day, now, members, ct);
    }

    // Page enrollment rows before loading evidence. Dropdown classes stay independent of filters/page.
    public static async Task<DecisionPage> ReadPageAsync(IMealTraceData db, MealDay day, DateTimeOffset now,
        Guid[]? allowedClasses, Guid? classId, string? search, int page, int pageSize, CancellationToken ct)
    {
        if (day.IsCancelled || allowedClasses is { Length: 0 }) return new([], 0, []);
        var eligible = Eligible(db, day, now, allowedClasses);
        var classes = await eligible.Select(x => new { x.ClassId, x.Class.Name }).Distinct()
            .OrderBy(x => x.Name).ThenBy(x => x.ClassId).Select(x => new Room(x.ClassId, x.Name)).ToListAsync(db.Queries, ct);
        var filtered = classId.HasValue ? eligible.Where(x => x.ClassId == classId.Value) : eligible;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            // Contains treats SQL wildcard characters literally rather than interpolating a LIKE pattern.
            filtered = filtered.Where(x => x.Student.FullName.ToLower().Contains(term) || x.Student.StudentCode.ToLower().Contains(term));
        }
        var total = await filtered.CountAsync(db.Queries, ct);
        var members = await Members(db, filtered.OrderBy(x => x.Class.Name).ThenBy(x => x.Student.FullName).ThenBy(x => x.StudentId)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(db.Queries, ct);
        return new(await ResolveAsync(db, day, now, members, ct), total, classes);
    }

    private static async Task<List<MealDecision>> ResolveAsync(IMealTraceData db, MealDay day, DateTimeOffset now,
        List<Member> members, CancellationToken ct)
    {
        if (members.Count == 0) return [];
        var asOf = now < day.CutoffAt ? now : day.CutoffAt;
        var ids = members.Select(x => x.StudentId).ToArray();
        var dated = db.MealAbsences.AsNoTracking(db.Queries).Where(x => x.FromDate <= day.Date && x.ToDate >= day.Date &&
            x.ReportedAt <= asOf && x.ReportedAt < day.CutoffAt &&
            (x.CancelledAt == null || x.CancelledAt > asOf || x.CancelledAt >= day.CutoffAt));
        IQueryable<MealAbsence>? relevant = null;
        foreach (var group in members.GroupBy(x => x.SchoolYear))
        {
            var code = group.Key; var yearIds = group.Select(x => x.StudentId).ToArray();
            var query = dated.Where(x => yearIds.Contains(x.StudentId) && (x.SchoolYear == null || x.SchoolYear == code));
            var year = group.First();
            if (year.YearStart is DateOnly start && year.YearEnd is DateOnly end)
            {
                if (day.Date < start || day.Date > end) continue;
                query = query.Where(x => x.SchoolYear != null || (x.FromDate >= start && x.FromDate <= end));
            }
            relevant = relevant is null ? query : relevant.Concat(query);
        }
        var validEvents = db.MealRegistrations.AsNoTracking(db.Queries).Where(x => x.MealDayId == day.Id &&
            x.RecordedAt <= asOf && x.RecordedAt < day.CutoffAt);
        // Unique (MealDayId, StudentId, Sequence) guarantees one latest valid event per child.
        var events = validEvents.Where(x => ids.Contains(x.StudentId) &&
                !validEvents.Any(newer => newer.StudentId == x.StudentId && newer.Sequence > x.Sequence))
            .Select(x => new { Kind = 1, x.StudentId, x.Id, x.RecordedAt, x.WillEat, x.Reason });
        var queryEvidence = relevant is null ? events : events.Concat(relevant.Select(x => new
        { Kind = 0, x.StudentId, x.Id, RecordedAt = x.ReportedAt, WillEat = (bool?)null, Reason = (string?)null }));
        var evidence = await queryEvidence.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(db.Queries, ct);
        var absenceByStudent = new Dictionary<Guid, Guid>();
        foreach (var absence in evidence.Where(x => x.Kind == 0)) absenceByStudent.TryAdd(absence.StudentId, absence.Id);
        var latest = evidence.Where(x => x.Kind == 1).ToDictionary(x => x.StudentId);
        return members.Select(x =>
        {
            Guid? absenceId = absenceByStudent.TryGetValue(x.StudentId, out var absence) ? absence : null;
            latest.TryGetValue(x.StudentId, out var exception);
            var willEat = exception?.WillEat ?? (absenceId is null);
            var source = exception?.WillEat switch { true => "STAFF_EAT", false => "STAFF_ABSENT", null => absenceId is null ? "DEFAULT" : "PARENT_ABSENCE" };
            return new MealDecision(x.StudentId, x.StudentCode, x.FullName, x.EnrollmentId, x.ClassId, x.ClassName, x.SchoolYear,
                willEat, source, absenceId is not null, absenceId, exception?.Id,
                exception is null ? null : Action(exception.WillEat), exception?.Reason);
        }).ToList();
    }
}

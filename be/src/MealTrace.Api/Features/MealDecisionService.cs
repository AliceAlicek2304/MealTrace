using MealTrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public sealed record MealDecision(Guid StudentId, string StudentCode, string FullName, Guid EnrollmentId,
    Guid ClassId, string ClassName, string SchoolYear, bool WillEat, string Source,
    bool ParentReportedAbsent, Guid? AbsenceId, Guid? LatestEventId, string? LatestAction, string? LatestReason);

internal static class MealDecisionService
{
    public static string Action(bool? willEat) => willEat switch { true => "EAT", false => "ABSENT", null => "DEFAULT" };

    public static async Task<List<MealDecision>> ReadAsync(MealTraceDbContext db, MealDay day, DateTimeOffset now, Guid[]? allowedClasses = null)
    {
        if (day.IsCancelled) return [];
        var asOf = now < day.CutoffAt ? now : day.CutoffAt;
        var query = StudentAdministrationEndpoints.OnDate(db, day.Date).Where(x => x.RecordedAt <= asOf &&
            (day.SchoolYear == null || x.Class.SchoolYear == day.SchoolYear));
        if (allowedClasses is not null) query = query.Where(x => allowedClasses.Contains(x.ClassId));
        var members = await query.Select(x => new { x.StudentId, x.Student.StudentCode, x.Student.FullName,
            EnrollmentId = x.Id, x.ClassId, ClassName = x.Class.Name, x.Class.SchoolYear }).ToListAsync();
        var ids = members.Select(x => x.StudentId).ToArray();
        var absences = await db.MealAbsences.AsNoTracking().Where(x => ids.Contains(x.StudentId) &&
            x.FromDate <= day.Date && x.ToDate >= day.Date && x.ReportedAt <= asOf && (x.CancelledAt == null || x.CancelledAt > asOf))
            .OrderByDescending(x => x.ReportedAt).ThenByDescending(x => x.Id).ToListAsync();
        var years = await db.AcademicYears.AsNoTracking().ToDictionaryAsync(x => x.Code);
        var absenceByStudent = absences.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.ToArray());
        var events = await db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == day.Id && ids.Contains(x.StudentId) && x.RecordedAt <= asOf)
            .OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync();
        var latest = events.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.First());
        return members.Select(x =>
        {
            years.TryGetValue(x.SchoolYear, out var year);
            absenceByStudent.TryGetValue(x.StudentId, out var candidates);
            var absence = candidates?.FirstOrDefault(a =>
                (a.SchoolYear == null || a.SchoolYear == x.SchoolYear) &&
                (year == null || (day.Date >= year.StartDate && day.Date <= year.EndDate &&
                    (a.SchoolYear != null || (a.FromDate >= year.StartDate && a.FromDate <= year.EndDate)))));
            latest.TryGetValue(x.StudentId, out var exception);
            var willEat = exception?.WillEat ?? (absence is null);
            var source = exception?.WillEat switch { true => "STAFF_EAT", false => "STAFF_ABSENT", null => absence is null ? "DEFAULT" : "PARENT_ABSENCE" };
            return new MealDecision(x.StudentId, x.StudentCode, x.FullName, x.EnrollmentId, x.ClassId, x.ClassName, x.SchoolYear,
                willEat, source, absence is not null, absence?.Id, exception?.Id,
                exception is null ? null : Action(exception.WillEat), exception?.Reason);
        }).ToList();
    }
}

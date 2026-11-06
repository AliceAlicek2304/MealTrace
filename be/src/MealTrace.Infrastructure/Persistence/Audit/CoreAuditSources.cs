using System.Text.Json;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Audit;

public sealed class CalendarAuditSource(MealTraceDbContext db) : EfAuditSource<MealCalendarAudit>
{
    public override string Name => "calendar";
    protected override bool TextEntityIdentity => true;
    protected override IQueryable<AuditProjection<MealCalendarAudit>> Events => db.MealCalendarAudits.AsNoTracking().Select(x => new AuditProjection<MealCalendarAudit>
    {
        Event = x, Id = x.Id, At = x.RecordedAt, ActorId = x.ActorId, ActorName = x.ActorName,
        Action = x.Kind == "SCHEDULE" ? "calendar.schedule" : x.Kind == "GENERATE" ? "calendar.generate" :
            x.Kind == "CLOSED" ? "calendar.close" : x.Kind == "OPEN" ? "calendar.open" :
            x.Kind == "DEFAULT" ? "calendar.restore" : x.Kind == "UPDATE" ? "calendar.update" : "calendar.unknown",
        EntityType = "MealCalendar", EntityText = x.SchoolYear, Reason = x.Reason
    });
    protected override JsonElement? Before(MealCalendarAudit row) => AuditPrivacy.Snapshot(row.BeforeJson);
    protected override JsonElement? After(MealCalendarAudit row) => AuditPrivacy.Snapshot(row.AfterJson);
}

public sealed class PasswordResetAuditSource(MealTraceDbContext db) : EfAuditSource<AccountPasswordResetAudit>
{
    public override string Name => "password-reset";
    protected override IQueryable<AuditProjection<AccountPasswordResetAudit>> Events => db.AccountPasswordResetAudits.AsNoTracking().Select(x => new AuditProjection<AccountPasswordResetAudit>
    {
        Event = x, Id = x.Id, At = x.PerformedAt, ActorId = x.PerformedByUserId,
        Action = "account.password_reset", EntityType = "Account", EntityId = x.UserId, Reason = x.Reason
    });
    protected override JsonElement? After(AccountPasswordResetAudit row) => null;
}

public sealed class EnrollmentAuditSource(MealTraceDbContext db, bool ending = false) : EfAuditSource<Enrollment>
{
    public override string Name => ending ? "enrollment-end" : "enrollment";
    protected override IQueryable<AuditProjection<Enrollment>> Events => db.Enrollments.AsNoTracking()
        .Where(x => !ending || x.EndRecordedAt != null).Select(x => new AuditProjection<Enrollment>
        {
            Event = x, Id = x.Id, At = ending ? x.EndRecordedAt!.Value : x.RecordedAt,
            ActorId = ending ? x.EndedByUserId : x.RecordedByUserId,
            Action = ending ? "enrollment.end" : "enrollment.start", EntityType = "Student", EntityId = x.StudentId,
            Reason = ending ? x.EndReason : x.Reason
        });
    protected override JsonElement? After(Enrollment row) => ending
        ? Json(new { enrollmentId = row.Id, row.StudentId, row.ClassId, row.EndDate })
        : Json(new { enrollmentId = row.Id, row.StudentId, row.ClassId, row.StartDate });
}

public sealed class MealExceptionAuditSource(MealTraceDbContext db) : EfAuditSource<MealRegistration>
{
    public override string Name => "meal-exception";
    protected override IQueryable<AuditProjection<MealRegistration>> Events => db.MealRegistrations.AsNoTracking().Select(x => new AuditProjection<MealRegistration>
    {
        Event = x, Id = x.Id, At = x.RecordedAt, ActorId = x.RecordedByUserId, ActorName = x.RecordedByName,
        Action = x.WillEat == null ? "meal.exception.restore" : "meal.exception.set",
        EntityType = "Student", EntityId = x.StudentId, Reason = x.Reason
    });
    protected override JsonElement? After(MealRegistration row) => Json(new
        { row.StudentId, row.MealDayId, row.WillEat, row.Sequence, row.SupersedesId });
}

public sealed class AbsenceAuditSource(MealTraceDbContext db, bool cancelled = false) : EfAuditSource<MealAbsence>
{
    public override string Name => cancelled ? "absence-cancel" : "absence";
    protected override IQueryable<AuditProjection<MealAbsence>> Events => db.MealAbsences.AsNoTracking()
        .Where(x => !cancelled || x.CancelledAt != null).Select(x => new AuditProjection<MealAbsence>
        {
            Event = x, Id = x.Id, At = cancelled ? x.CancelledAt!.Value : x.ReportedAt,
            ActorId = cancelled ? null : x.ReportedByUserId, Reason = cancelled ? null : x.Reason,
            Action = cancelled ? "absence.cancel" : "absence.create", EntityType = "Student", EntityId = x.StudentId
        });
    protected override JsonElement? After(MealAbsence row) => Json(new { row.StudentId, row.FromDate, row.ToDate, row.SchoolYear });
}

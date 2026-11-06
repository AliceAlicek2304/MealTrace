using System.Text.Json;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Audit;

public sealed class AmendmentAuditSource(MealTraceDbContext db) : EfAuditSource<PortionAmendment>
{
    public override string Name => "amendment";
    protected override IQueryable<AuditProjection<PortionAmendment>> Events => db.PortionAmendments.AsNoTracking().Select(x => new AuditProjection<PortionAmendment>
    {
        Event = x, Id = x.Id, At = x.RequestedAt, ActorId = x.RequestedBy, ActorName = x.RequestedByName,
        Action = "portion.amend.request", EntityType = "PortionAmendment", EntityId = x.Id, Reason = x.Reason
    });
    protected override IQueryable<AuditProjection<PortionAmendment>> FilterEntity(IQueryable<AuditProjection<PortionAmendment>> query, AuditFilter filter)
    {
        if (filter.EntityType != "Student") return base.FilterEntity(query, filter);
        if (!Guid.TryParse(filter.EntityId, out var student)) return query.Where(x => false);
        return query.Where(x => !x.Event.IsQuantityOnly && (x.Event.StudentId == student || x.Event.Students.Any(s => s.StudentId == student)));
    }
    protected override JsonElement? After(PortionAmendment row) => Json(new
    {
        row.BaseSettlementId, row.Quantity, row.IsQuantityOnly, row.WillEat,
        studentId = !row.IsQuantityOnly && row.StudentId != Guid.Empty ? row.StudentId : (Guid?)null,
        row.EnrollmentId,
        students = row.Students.Count == 0 ? null : row.Students.OrderBy(s => s.StudentId).Select(s => new { s.StudentId, s.EnrollmentId }).ToArray()
    });
}

public sealed class AmendmentResolutionAuditSource(MealTraceDbContext db) : EfAuditSource<PortionAmendmentResolution>
{
    public override string Name => "amendment-resolution";
    protected override IQueryable<AuditProjection<PortionAmendmentResolution>> Events => db.PortionAmendmentResolutions.AsNoTracking().Select(x => new AuditProjection<PortionAmendmentResolution>
    {
        Event = x, Id = x.AmendmentId, At = x.ReviewedAt, ActorId = x.ReviewedBy, ActorName = x.ReviewedByName,
        Action = "portion.amend.resolve", EntityType = "PortionAmendment", EntityId = x.AmendmentId, Reason = x.Reason
    });
    protected override IQueryable<AuditProjection<PortionAmendmentResolution>> FilterEntity(IQueryable<AuditProjection<PortionAmendmentResolution>> query, AuditFilter filter)
    {
        if (filter.EntityType != "Student") return base.FilterEntity(query, filter);
        if (!Guid.TryParse(filter.EntityId, out var student)) return query.Where(x => false);
        return query.Where(x => !x.Event.Amendment.IsQuantityOnly && (x.Event.Amendment.StudentId == student || x.Event.Amendment.Students.Any(s => s.StudentId == student)));
    }
    protected override JsonElement? After(PortionAmendmentResolution row) => Json(new
        { row.AmendmentId, row.Approved, row.AppliedSettlementId });
}

public sealed class SettlementAuditSource(MealTraceDbContext db) : EfAuditSource<PortionSettlement>
{
    public override string Name => "settlement";
    protected override AuditActor? Actor(AuditProjection<PortionSettlement> row) =>
        Guid.TryParse(row.Event.SettledBy, out var id) && id != Guid.Empty ? new AuditActor(id, null) : null;
    protected override IQueryable<AuditProjection<PortionSettlement>> FilterActor(IQueryable<AuditProjection<PortionSettlement>> query, Guid actor)
    {
        var text = actor.ToString("D");
        return query.Where(x => x.Event.SettledBy.ToLower() == text);
    }
    // SettledBy is legacy text; only a valid stored GUID is an actor identity.
    // Parsing happens after materialization; actor filters use canonical source text.
    protected override IQueryable<AuditProjection<PortionSettlement>> Events => db.PortionSettlements.AsNoTracking()
        .Include(x => x.Decisions).Include(x => x.Students).AsSplitQuery().Select(x => new AuditProjection<PortionSettlement>
    {
        Event = x, Id = x.Id, At = x.SettledAt, Action = "portion.settle",
        EntityType = "PortionSettlement", EntityId = x.Id, Reason = x.Reason
    });
    protected override IQueryable<AuditProjection<PortionSettlement>> FilterEntity(IQueryable<AuditProjection<PortionSettlement>> query, AuditFilter filter)
    {
        if (filter.EntityType != "Student") return base.FilterEntity(query, filter);
        if (!Guid.TryParse(filter.EntityId, out var student)) return query.Where(x => false);
        return query.Where(x => x.Event.Decisions.Any(s => s.StudentId == student) || x.Event.Students.Any(s => s.StudentId == student));
    }
    protected override JsonElement? After(PortionSettlement row) => Json(new
    {
        row.MealDayId, row.ClassId, row.Count, row.KitchenAdjustment, row.Version, row.SupersedesId, row.CutoffAt,
        decisions = row.Decisions.Count == 0 ? null : row.Decisions.OrderBy(d => d.StudentId).Select(d => new
            { d.StudentId, d.WillEat, d.EnrollmentId, d.AbsenceId, d.ExceptionId, d.AmendmentId, d.Source }).ToArray(),
        students = row.Students.Count == 0 ? null : row.Students.OrderBy(s => s.StudentId).Select(s => new { s.StudentId }).ToArray()
    });
}

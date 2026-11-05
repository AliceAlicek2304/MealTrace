using System.Text.Json;
using MealTrace.Application.Features.Audit;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Audit;

public sealed class ParentLinkAuditSource(MealTraceDbContext db, string stage = "request") : EfAuditSource<ParentLinkRequest>
{
    public override string Name => stage == "request" ? "parent-link" : "parent-link-" + stage;
    protected override IQueryable<AuditProjection<ParentLinkRequest>> Events => db.ParentLinkRequests.AsNoTracking()
        .Where(x => stage == "request" || (stage == "revoke" ? x.RevokedAt != null : x.ReviewedAt != null))
        .Select(x => new AuditProjection<ParentLinkRequest>
        {
            Event = x, Id = x.Id, At = stage == "request" ? x.RequestedAt : stage == "revoke" ? x.RevokedAt!.Value : x.ReviewedAt!.Value,
            ActorId = stage == "request" ? x.ParentId : stage == "revoke" ? x.RevokedByUserId : x.Status == "CANCELLED" ? x.ParentId : x.ReviewedByUserId,
            Action = stage == "request" ? "parent.link.request" : stage == "revoke" ? "parent.link.revoke" :
                x.Status == "CANCELLED" ? "parent.link.cancel" : x.Status == "REJECTED" ? "parent.link.reject" :
                x.Status == "APPROVED" || x.Status == "REVOKED" ? "parent.link.approve" : "parent.link.review",
            EntityType = "ParentLinkRequest", EntityId = x.Id,
            Reason = stage == "request" ? null : stage == "revoke" ? x.RevocationReason : x.ReviewReason
        });
    protected override IQueryable<AuditProjection<ParentLinkRequest>> FilterEntity(IQueryable<AuditProjection<ParentLinkRequest>> query, AuditFilter filter)
    {
        if (filter.EntityType != "Student") return base.FilterEntity(query, filter);
        if (stage == "request") return query.Where(x => false);
        return Guid.TryParse(filter.EntityId, out var id) ? query.Where(x => x.Event.StudentId == id) : query.Where(x => false);
    }
    // Student matching can change during review; a request event must not claim the
    // final matched child was known at creation. No current status in old events.
    protected override JsonElement? After(ParentLinkRequest row) => stage == "request"
        ? Json(new { row.ParentId }) : stage == "revoke"
        ? Json(new { row.StudentId, status = "REVOKED" }) : Json(new
            { row.StudentId, status = row.Status == "REVOKED" ? "APPROVED" : row.Status });
}

public sealed class StudentImportAuditSource(MealTraceDbContext db) : EfAuditSource<StudentImportBatch>
{
    public override string Name => "student-import";
    protected override IQueryable<AuditProjection<StudentImportBatch>> Events => db.StudentImportBatches.AsNoTracking().Select(x => new AuditProjection<StudentImportBatch>
    {
        Event = x, Id = x.Id, At = x.ImportedAt, ActorId = x.ImportedByUserId,
        Action = "student.import", EntityType = "StudentImportBatch", EntityId = x.Id
    });
    protected override IQueryable<AuditProjection<StudentImportBatch>> FilterEntity(IQueryable<AuditProjection<StudentImportBatch>> query, AuditFilter filter)
    {
        if (filter.EntityType != "Student") return base.FilterEntity(query, filter);
        return Guid.TryParse(filter.EntityId, out var id) ? query.Where(x => x.Event.Items.Any(i => i.StudentId == id)) : query.Where(x => false);
    }
    protected override JsonElement? After(StudentImportBatch row) => Json(new { row.ClassId, row.SchoolYear, row.StartDate, row.Created });
}

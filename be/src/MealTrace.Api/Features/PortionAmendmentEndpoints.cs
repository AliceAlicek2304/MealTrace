using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public static class PortionAmendmentEndpoints
{
    public sealed record RequestInput(Guid ClassId, Guid StudentId, Guid BaseSettlementId, bool WillEat, string Reason);
    public sealed record ReviewInput(bool Approve, string Reason);
    private static Guid Actor(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    private static Task<bool> CanRead(MealTraceDbContext db, ClaimsPrincipal principal, Guid classId) =>
        principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.KitchenStaff)
            ? Task.FromResult(true)
            : db.TeacherAssignments.AnyAsync(x => x.UserId == Actor(principal) && x.ClassId == classId);
    private static async Task<string> ActorName(MealTraceDbContext db, ClaimsPrincipal principal) =>
        await db.Users.Where(x => x.Id == Actor(principal)).Select(x => x.FullName).SingleAsync();
    private static Task<PortionSettlement?> Latest(MealTraceDbContext db, Guid dayId, Guid classId) =>
        db.PortionSettlements.Where(x => x.MealDayId == dayId && x.ClassId == classId)
            .OrderByDescending(x => x.Version).Include(x => x.Students).Include(x => x.Decisions).AsSplitQuery().FirstOrDefaultAsync();
    private static async Task<MealDay?> LockDay(MealTraceDbContext db, Guid id) => db.Database.IsNpgsql()
        ? (await db.MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).SingleOrDefault()
        : await db.MealDays.FindAsync(id);
    private static Task<bool> EatingElsewhere(MealTraceDbContext db, Guid dayId, Guid classId, Guid studentId) =>
        db.SettlementStudents.AnyAsync(x => x.StudentId == studentId && x.PortionSettlement.MealDayId == dayId &&
            x.PortionSettlement.ClassId != null && x.PortionSettlement.ClassId != classId &&
            !db.PortionSettlements.Any(newer => newer.MealDayId == dayId && newer.ClassId == x.PortionSettlement.ClassId &&
                newer.Version > x.PortionSettlement.Version));

    public static IEndpointRouteBuilder MapPortionAmendmentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/meal-days/{dayId:guid}/amendments").WithTags("Portion amendments")
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));
        api.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (Exception ex) when (DatabaseConflicts.IsConflict(ex))
            { return Results.Conflict(new { message = "Dữ liệu vừa thay đổi. Tải lại bản suất và đối chiếu lại yêu cầu." }); }
        });

        api.MapGet("", async (Guid dayId, Guid classId, string? q, int? page, int? candidatePage,
            ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            if (!await CanRead(db, principal, classId)) return Results.Forbid();
            var day = await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dayId);
            if (day is null) return Results.NotFound();
            var current = await Latest(db, dayId, classId);
            if (current is null || day.SettledAt is null) return Results.Conflict(new { message = "Lớp chưa có bản chốt suất." });
            var root = await db.PortionSettlements.AsNoTracking().Where(x => x.MealDayId == dayId && x.ClassId == classId)
                .OrderBy(x => x.Version).Include(x => x.Students).Include(x => x.Decisions).AsSplitQuery().FirstAsync();
            var members = StudentAdministrationEndpoints.OnDate(db, day.Date).Where(x => x.ClassId == classId);
            var candidatesQuery = db.Students.AsNoTracking().Where(x => members.Any(e => e.StudentId == x.Id) ||
                db.SettlementStudents.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id) ||
                db.SettlementDecisions.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id));
            var term = q?.Trim();
            if (!string.IsNullOrEmpty(term))
                candidatesQuery = candidatesQuery.Where(x => x.FullName.ToLower().Contains(term.ToLower()) || x.StudentCode.ToLower().Contains(term.ToLower()));
            var candidateNumber = Math.Clamp(candidatePage ?? 1, 1, 100000);
            var candidateTotal = await candidatesQuery.CountAsync();
            var candidates = await candidatesQuery.OrderBy(x => x.FullName).ThenBy(x => x.Id)
                .Skip((candidateNumber - 1) * 25).Take(25).Select(x => new { x.Id, x.FullName, x.StudentCode }).ToListAsync();
            var requests = db.PortionAmendments.AsNoTracking().Where(x => x.BaseSettlement.MealDayId == dayId && x.BaseSettlement.ClassId == classId);
            var number = Math.Clamp(page ?? 1, 1, 100000); var total = await requests.CountAsync();
            var items = await requests.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Skip((number - 1) * 25).Take(25)
                .Select(x => new { x.Id, x.StudentId, x.StudentName, x.StudentCode, x.BaseSettlementId,
                    BaseVersion = x.BaseSettlement.Version, x.EnrollmentId, x.WasEating, x.WillEat, x.Reason, x.RequestedByName, x.RequestedAt,
                    Status = x.Resolution == null ? "PENDING" : x.Resolution.Approved ? "APPROVED" : "REJECTED",
                    ReviewReason = x.Resolution == null ? null : x.Resolution.Reason,
                    ReviewedByName = x.Resolution == null ? null : x.Resolution.ReviewedByName,
                    ReviewedAt = x.Resolution == null ? (DateTimeOffset?)null : x.Resolution.ReviewedAt,
                    AppliedSettlementId = x.Resolution == null ? null : x.Resolution.AppliedSettlementId }).ToListAsync();
            return Results.Ok(new
            {
                classId, ClassName = current.ClassName, HasCompleteRoster = current.Count == current.Students.Count,
                CanRequest = !day.IsCancelled && current.Count == current.Students.Count &&
                    (principal.IsInRole(RoleNames.Admin) || (principal.IsInRole(RoleNames.Teacher) &&
                        await db.TeacherAssignments.AnyAsync(x => x.UserId == Actor(principal) && x.ClassId == classId))),
                Original = Snapshot(root), Current = Snapshot(current), HasOriginalSources = root.Decisions.Count > 0,
                Added = current.Students.Where(x => root.Students.All(s => s.StudentId != x.StudentId)).Select(x => new { x.StudentId, x.StudentName }),
                Removed = root.Students.Where(x => current.Students.All(s => s.StudentId != x.StudentId)).Select(x => new { x.StudentId, x.StudentName }),
                Candidates = candidates.Select(x =>
                {
                    var frozen = current.Decisions.FirstOrDefault(d => d.StudentId == x.Id);
                    var included = current.Students.FirstOrDefault(s => s.StudentId == x.Id);
                    return new { StudentId = x.Id, StudentName = frozen?.StudentName ?? included?.StudentName ?? x.FullName,
                        StudentCode = frozen?.StudentCode ?? x.StudentCode, WillEat = included is not null,
                        Source = frozen?.Source ?? "LEGACY_OR_CURRENT_ENROLLMENT", frozen?.EnrollmentId, frozen?.AbsenceId, frozen?.ExceptionId };
                }), candidateTotal, candidatePage = candidateNumber, items, total, page = number, pageSize = 25
            });
        });

        api.MapPost("", async (Guid dayId, RequestInput input, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            if (!ValidReason(input.Reason)) return Results.BadRequest(new { message = "Cần lý do 1–500 ký tự." });
            // Kitchen access never grants permission to submit a request outside the teacher's class.
            if (!principal.IsInRole(RoleNames.Admin) && !await db.TeacherAssignments.AnyAsync(x => x.UserId == Actor(principal) && x.ClassId == input.ClassId))
                return Results.Forbid();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var day = await LockDay(db, dayId);
            if (day is null) return Results.NotFound();
            if (day.IsCancelled || day.SettledAt is null || clock.GetUtcNow() < day.CutoffAt)
                return Results.Conflict(new { message = "Chỉ điều chỉnh phiên đang hoạt động sau khi đã chốt." });
            var current = await Latest(db, dayId, input.ClassId);
            if (current is null) return Results.BadRequest(new { message = "Lớp chưa có bản chốt." });
            if (current.Count != current.Students.Count)
                return Results.Conflict(new { message = "Bản cũ thiếu danh sách trẻ để đối chiếu tổng suất; chưa thể điều chỉnh theo trẻ." });
            if (current.Id != input.BaseSettlementId) return Results.Conflict(new { message = "Bản suất đã thay đổi. Tải lại rồi đối chiếu yêu cầu mới." });
            var frozen = current.Decisions.FirstOrDefault(x => x.StudentId == input.StudentId);
            var included = current.Students.FirstOrDefault(x => x.StudentId == input.StudentId);
            var enrollment = await StudentAdministrationEndpoints.OnDate(db, day.Date).AsNoTracking()
                .Where(x => x.ClassId == input.ClassId && x.StudentId == input.StudentId).Include(x => x.Student).FirstOrDefaultAsync();
            if (frozen is null && included is null && enrollment is null)
                return Results.BadRequest(new { message = "Trẻ không thuộc lớp tại ngày ăn hoặc bản suất nguồn." });
            var wasEating = included is not null;
            if (wasEating == input.WillEat) return Results.BadRequest(new { message = "Yêu cầu không thay đổi số suất của trẻ." });
            if (input.WillEat && await EatingElsewhere(db, dayId, input.ClassId, input.StudentId))
                return Results.Conflict(new { message = "Trẻ đang có suất ở lớp khác trong cùng phiên. Đối chiếu và điều chỉnh lớp nguồn trước." });
            if (await db.PortionAmendments.AnyAsync(x => x.BaseSettlementId == current.Id && x.StudentId == input.StudentId && x.Resolution == null))
                return Results.Conflict(new { message = "Trẻ đã có yêu cầu đang chờ xử lý trên bản suất này." });
            var request = new PortionAmendment { BaseSettlementId = current.Id, StudentId = input.StudentId,
                StudentName = frozen?.StudentName ?? included?.StudentName ?? enrollment!.Student.FullName,
                StudentCode = frozen?.StudentCode ?? (enrollment?.Student.StudentCode ?? await db.Students.Where(x => x.Id == input.StudentId).Select(x => x.StudentCode).SingleAsync()),
                EnrollmentId = frozen?.EnrollmentId ?? enrollment?.Id, WasEating = wasEating, WillEat = input.WillEat,
                Reason = input.Reason.Trim(), RequestedBy = Actor(principal), RequestedByName = await ActorName(db, principal), RequestedAt = clock.GetUtcNow() };
            db.PortionAmendments.Add(request); await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Results.Ok(new { request.Id });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapGet("/{requestId:guid}", async (Guid dayId, Guid requestId, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var request = await db.PortionAmendments.AsNoTracking().Include(x => x.BaseSettlement).ThenInclude(x => x.Students)
                .Include(x => x.BaseSettlement).ThenInclude(x => x.Decisions)
                .Include(x => x.Resolution).ThenInclude(x => x!.AppliedSettlement).ThenInclude(x => x!.Students)
                .Include(x => x.Resolution).ThenInclude(x => x!.AppliedSettlement).ThenInclude(x => x!.Decisions)
                .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == requestId && x.BaseSettlement.MealDayId == dayId);
            if (request is null) return Results.NotFound();
            if (request.BaseSettlement.ClassId is not Guid classId || !await CanRead(db, principal, classId)) return Results.Forbid();
            return Results.Ok(new { Before = Snapshot(request.BaseSettlement),
                After = request.Resolution?.AppliedSettlement is { } applied ? Snapshot(applied) : null });
        });

        api.MapPost("/{requestId:guid}/review", async (Guid dayId, Guid requestId, ReviewInput input,
            ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            if (!ValidReason(input.Reason)) return Results.BadRequest(new { message = "Cần lý do duyệt/từ chối 1–500 ký tự." });
            await using var transaction = await db.Database.BeginTransactionAsync();
            var day = await LockDay(db, dayId);
            if (day is null) return Results.NotFound();
            var request = await db.PortionAmendments.Include(x => x.BaseSettlement).Include(x => x.Resolution)
                .FirstOrDefaultAsync(x => x.Id == requestId && x.BaseSettlement.MealDayId == dayId);
            if (request is null) return Results.NotFound();
            if (request.Resolution is not null) return Results.Conflict(new { message = "Yêu cầu đã được xử lý." });
            var reviewer = await ActorName(db, principal); PortionSettlement? applied = null;
            if (input.Approve)
            {
                if (day.IsCancelled || day.SettledAt is null || clock.GetUtcNow() < day.CutoffAt)
                    return Results.Conflict(new { message = "Phiên không đủ điều kiện áp dụng điều chỉnh." });
                var current = await Latest(db, dayId, request.BaseSettlement.ClassId!.Value);
                if (current is null || current.Id != request.BaseSettlementId)
                    return Results.Conflict(new { message = "Bản nguồn đã lỗi thời. Từ chối và gửi yêu cầu mới sau khi đối chiếu." });
                if (current.Count != current.Students.Count)
                    return Results.Conflict(new { message = "Bản nguồn thiếu danh sách trẻ để đối chiếu; chưa thể duyệt điều chỉnh." });
                if (request.WillEat && await EatingElsewhere(db, dayId, current.ClassId!.Value, request.StudentId))
                    return Results.Conflict(new { message = "Trẻ vừa có suất ở lớp khác; cần đối chiếu trước khi duyệt." });
                applied = new PortionSettlement { MealDayId = dayId, ClassId = current.ClassId, ClassName = current.ClassName,
                    CutoffAt = current.CutoffAt, Version = current.Version + 1, SupersedesId = current.Id,
                    SettledAt = clock.GetUtcNow(), SettledBy = Actor(principal).ToString(), Reason = input.Reason.Trim(),
                    Students = current.Students.Where(x => x.StudentId != request.StudentId).Select(x => new SettlementStudent
                    { StudentId = x.StudentId, StudentName = x.StudentName }).ToList(),
                    Decisions = current.Decisions.Where(x => x.StudentId != request.StudentId).Select(CloneDecision).ToList() };
                if (request.WillEat) applied.Students.Add(new SettlementStudent { StudentId = request.StudentId, StudentName = request.StudentName });
                var source = current.Decisions.FirstOrDefault(x => x.StudentId == request.StudentId);
                applied.Decisions.Add(new SettlementDecision { StudentId = request.StudentId, StudentName = request.StudentName,
                    StudentCode = request.StudentCode, WillEat = request.WillEat, EnrollmentId = request.EnrollmentId,
                    AbsenceId = source?.AbsenceId, ExceptionId = source?.ExceptionId, AmendmentId = request.Id, Source = "APPROVED_AMENDMENT" });
                applied.Count = applied.Students.Count; db.PortionSettlements.Add(applied);
            }
            db.PortionAmendmentResolutions.Add(new PortionAmendmentResolution { AmendmentId = requestId, Approved = input.Approve,
                AppliedSettlement = applied, Reason = input.Reason.Trim(), ReviewedBy = Actor(principal), ReviewedByName = reviewer, ReviewedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Results.Ok(new { status = input.Approve ? "APPROVED" : "REJECTED", settlementId = applied?.Id, version = applied?.Version });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        return app;
    }

    private static object Snapshot(PortionSettlement x) => new { x.Id, x.Version, x.Count, x.SettledAt, x.SettledBy, x.Reason, x.SupersedesId,
        Students = x.Students.Select(s => new { s.StudentId, s.StudentName }),
        Decisions = x.Decisions.Select(s => new { s.StudentId, s.StudentName, s.StudentCode, s.WillEat, s.Source, s.EnrollmentId, s.AbsenceId, s.ExceptionId, s.AmendmentId }) };
    private static SettlementDecision CloneDecision(SettlementDecision x) => new()
    { StudentId = x.StudentId, StudentName = x.StudentName, StudentCode = x.StudentCode, WillEat = x.WillEat,
        EnrollmentId = x.EnrollmentId, AbsenceId = x.AbsenceId, ExceptionId = x.ExceptionId, AmendmentId = x.AmendmentId, Source = x.Source };
}

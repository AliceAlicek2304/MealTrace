using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Portions;
using System.Security.Claims;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features;
public static class PortionAmendmentUseCases
{
    private static Guid Actor(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirst("sub")?.Value!);
    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    private static Task<bool> CanRead(IMealTraceData db, ClaimsPrincipal principal, Guid classId) => principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.KitchenStaff) ? Task.FromResult(true) : db.TeacherAssignments.AnyAsync(db.Queries, x => x.UserId == Actor(principal) && x.ClassId == classId);
    private static async Task<string> ActorName(IMealTraceData db, ClaimsPrincipal principal) => await db.Users.Where(x => x.Id == Actor(principal)).Select(x => x.FullName).SingleAsync(db.Queries);
    private static Task<PortionSettlement?> Latest(IMealTraceData db, Guid dayId, Guid classId) => db.PortionSettlements.Where(x => x.MealDayId == dayId && x.ClassId == classId).OrderByDescending(x => x.Version).Include(db.Queries, "Students").Include(db.Queries, "Decisions").AsSplitQuery(db.Queries).FirstOrDefaultAsync(db.Queries);
    private static async Task<MealDay?> LockDay(IMealTraceData db, Guid id) => await db.LockMealDayAsync(id);
    private static Task<bool> EatingElsewhere(IMealTraceData db, Guid dayId, Guid classId, Guid studentId) => db.SettlementStudents.AnyAsync(db.Queries, x => x.StudentId == studentId && x.PortionSettlement.MealDayId == dayId && x.PortionSettlement.ClassId != null && x.PortionSettlement.ClassId != classId && !db.PortionSettlements.Any(newer => newer.MealDayId == dayId && newer.ClassId == x.PortionSettlement.ClassId && newer.Version > x.PortionSettlement.Version));
    public static async Task<UseCaseResult> GetClassAmendmentsAsync(Guid dayId, Guid classId, string? q, int? page, int? candidatePage, ClaimsPrincipal principal, IMealTraceData db)
    {
        if (!await CanRead(db, principal, classId))
            return UseCaseResult.Forbid();
        var day = await db.MealDays.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == dayId);
        if (day is null)
            return UseCaseResult.NotFound();
        var current = await Latest(db, dayId, classId);
        if (current is null || day.SettledAt is null)
            return UseCaseResult.Conflict(new MessageResponse("Lớp chưa có bản chốt suất."));
        var root = await db.PortionSettlements.AsNoTracking(db.Queries).Where(x => x.MealDayId == dayId && x.ClassId == classId).OrderBy(x => x.Version).Include(db.Queries, "Students").Include(db.Queries, "Decisions").AsSplitQuery(db.Queries).FirstAsync(db.Queries);
        var members = StudentAdministrationUseCases.OnDate(db, day.Date).Where(x => x.ClassId == classId);
        var candidatesQuery = db.Students.AsNoTracking(db.Queries).Where(x => members.Any(e => e.StudentId == x.Id) || db.SettlementStudents.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id) || db.SettlementDecisions.Any(s => s.PortionSettlementId == current.Id && s.StudentId == x.Id));
        var term = q?.Trim();
        if (!string.IsNullOrEmpty(term))
            candidatesQuery = candidatesQuery.Where(x => x.FullName.ToLower().Contains(term.ToLower()) || x.StudentCode.ToLower().Contains(term.ToLower()));
        var candidateNumber = Math.Clamp(candidatePage ?? 1, 1, 100000);
        var candidateTotal = await candidatesQuery.CountAsync(db.Queries);
        var candidates = await candidatesQuery.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((candidateNumber - 1) * 25).Take(25).Select(x => new AmendmentCandidateRow
        {
            Id = x.Id,
            FullName = x.FullName,
            StudentCode = x.StudentCode
        }).ToListAsync(db.Queries);
        var requests = db.PortionAmendments.AsNoTracking(db.Queries).Where(x => x.BaseSettlement.MealDayId == dayId && x.BaseSettlement.ClassId == classId);
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var total = await requests.CountAsync(db.Queries);
        var items = await requests.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id).Skip((number - 1) * 25).Take(25).Select(x => new PortionAmendmentSummary
        {
            Id = x.Id,
            StudentId = x.StudentId,
            StudentName = x.StudentName,
            StudentCode = x.StudentCode,
            BaseSettlementId = x.BaseSettlementId,
            BaseVersion = x.BaseSettlement.Version,
            EnrollmentId = x.EnrollmentId,
            WasEating = x.WasEating,
            WillEat = x.WillEat,
            Reason = x.Reason,
            RequestedByName = x.RequestedByName,
            RequestedAt = x.RequestedAt,
            Status = x.Resolution == null ? "PENDING" : x.Resolution.Approved ? "APPROVED" : "REJECTED",
            ReviewReason = x.Resolution == null ? null : x.Resolution.Reason,
            ReviewedByName = x.Resolution == null ? null : x.Resolution.ReviewedByName,
            ReviewedAt = x.Resolution == null ? (DateTimeOffset?)null : x.Resolution.ReviewedAt,
            AppliedSettlementId = x.Resolution == null ? null : x.Resolution.AppliedSettlementId
        }).ToListAsync(db.Queries);
        return UseCaseResult.Ok(new ClassAmendmentsResponse
        {
            ClassId = classId,
            ClassName = current.ClassName,
            HasCompleteRoster = current.Count == current.Students.Count,
            CanRequest = !day.IsCancelled && current.Count == current.Students.Count && (principal.IsInRole(RoleNames.Admin) || (principal.IsInRole(RoleNames.Teacher) && await db.TeacherAssignments.AnyAsync(db.Queries, x => x.UserId == Actor(principal) && x.ClassId == classId))),
            Original = Snapshot(root),
            Current = Snapshot(current),
            HasOriginalSources = root.Decisions.Count > 0,
            Added = current.Students.Where(x => root.Students.All(s => s.StudentId != x.StudentId)).Select(x => new SettlementStudentSummary
            {
                StudentId = x.StudentId,
                StudentName = x.StudentName
            }),
            Removed = root.Students.Where(x => current.Students.All(s => s.StudentId != x.StudentId)).Select(x => new SettlementStudentSummary
            {
                StudentId = x.StudentId,
                StudentName = x.StudentName
            }),
            Candidates = candidates.Select(x =>
                   {
                       var frozen = current.Decisions.FirstOrDefault(d => d.StudentId == x.Id);
                       var included = current.Students.FirstOrDefault(s => s.StudentId == x.Id);
                       return new AmendmentCandidate
                       {

                           StudentId = x.Id,

                           StudentName = frozen?.StudentName ?? included?.StudentName ?? x.FullName,

                           StudentCode = frozen?.StudentCode ?? x.StudentCode,

                           WillEat = included is not null,

                           Source = frozen?.Source ?? "LEGACY_OR_CURRENT_ENROLLMENT",

                           EnrollmentId = frozen?.EnrollmentId,

                           AbsenceId = frozen?.AbsenceId,

                           ExceptionId = frozen?.ExceptionId

                       };
                   }),
            CandidateTotal = candidateTotal,
            CandidatePage = candidateNumber,
            Items = items,
            Total = total,
            Page = number,
            PageSize = 25
        });
    }

    public static async Task<UseCaseResult> RequestAmendmentAsync(Guid dayId, RequestInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        if (!ValidReason(input.Reason))
            return UseCaseResult.BadRequest(new MessageResponse("Cần lý do 1–500 ký tự."));
        // Kitchen access never grants permission to submit a request outside the teacher's class.
        if (!principal.IsInRole(RoleNames.Admin) && !await db.TeacherAssignments.AnyAsync(db.Queries, x => x.UserId == Actor(principal) && x.ClassId == input.ClassId))
            return UseCaseResult.Forbid();
        await using var transaction = await db.BeginTransactionAsync();
        var day = await LockDay(db, dayId);
        if (day is null)
            return UseCaseResult.NotFound();
        if (day.IsCancelled || day.SettledAt is null || clock.GetUtcNow() < day.CutoffAt)
            return UseCaseResult.Conflict(new MessageResponse("Chỉ điều chỉnh phiên đang hoạt động sau khi đã chốt."));
        var current = await Latest(db, dayId, input.ClassId);
        if (current is null)
            return UseCaseResult.BadRequest(new MessageResponse("Lớp chưa có bản chốt."));
        if (current.Count != current.Students.Count)
            return UseCaseResult.Conflict(new MessageResponse("Bản cũ thiếu danh sách trẻ để đối chiếu tổng suất; chưa thể điều chỉnh theo trẻ."));
        if (current.Id != input.BaseSettlementId)
            return UseCaseResult.Conflict(new MessageResponse("Bản suất đã thay đổi. Tải lại rồi đối chiếu yêu cầu mới."));
        var frozen = current.Decisions.FirstOrDefault(x => x.StudentId == input.StudentId);
        var included = current.Students.FirstOrDefault(x => x.StudentId == input.StudentId);
        var enrollment = await StudentAdministrationUseCases.OnDate(db, day.Date).AsNoTracking(db.Queries).Where(x => x.ClassId == input.ClassId && x.StudentId == input.StudentId).Include(db.Queries, "Student").FirstOrDefaultAsync(db.Queries);
        if (frozen is null && included is null && enrollment is null)
            return UseCaseResult.BadRequest(new MessageResponse("Trẻ không thuộc lớp tại ngày ăn hoặc bản suất nguồn."));
        var wasEating = included is not null;
        if (wasEating == input.WillEat)
            return UseCaseResult.BadRequest(new MessageResponse("Yêu cầu không thay đổi số suất của trẻ."));
        if (input.WillEat && await EatingElsewhere(db, dayId, input.ClassId, input.StudentId))
            return UseCaseResult.Conflict(new MessageResponse("Trẻ đang có suất ở lớp khác trong cùng phiên. Đối chiếu và điều chỉnh lớp nguồn trước."));
        if (await db.PortionAmendments.AnyAsync(db.Queries, x => x.BaseSettlementId == current.Id && x.StudentId == input.StudentId && x.Resolution == null))
            return UseCaseResult.Conflict(new MessageResponse("Trẻ đã có yêu cầu đang chờ xử lý trên bản suất này."));
        var request = new PortionAmendment
        {

            BaseSettlementId = current.Id,

            StudentId = input.StudentId,

            StudentName = frozen?.StudentName ?? included?.StudentName ?? enrollment!.Student.FullName,

            StudentCode = frozen?.StudentCode ?? (enrollment?.Student.StudentCode ?? await db.Students.Where(x => x.Id == input.StudentId).Select(x => x.StudentCode).SingleAsync(db.Queries)),

            EnrollmentId = frozen?.EnrollmentId ?? enrollment?.Id,

            WasEating = wasEating,

            WillEat = input.WillEat,

            Reason = input.Reason.Trim(),

            RequestedBy = Actor(principal),

            RequestedByName = await ActorName(db, principal),

            RequestedAt = clock.GetUtcNow()

        };
        db.PortionAmendments.Add(request);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Ok(new AmendmentCreatedResponse { Id = request.Id });
    }

    public static async Task<UseCaseResult> GetAmendmentAsync(Guid dayId, Guid requestId, ClaimsPrincipal principal, IMealTraceData db)
    {
        var request = await db.PortionAmendments.AsNoTracking(db.Queries).Include(db.Queries, "BaseSettlement.Students").Include(db.Queries, "BaseSettlement.Decisions").Include(db.Queries, "Resolution.AppliedSettlement.Students").Include(db.Queries, "Resolution.AppliedSettlement.Decisions").AsSplitQuery(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == requestId && x.BaseSettlement.MealDayId == dayId);
        if (request is null)
            return UseCaseResult.NotFound();
        if (request.BaseSettlement.ClassId is not Guid classId || !await CanRead(db, principal, classId))
            return UseCaseResult.Forbid();
        return UseCaseResult.Ok(new AmendmentComparisonResponse
        {
            Before = Snapshot(request.BaseSettlement),
            After = request.Resolution?.AppliedSettlement is { } applied ? Snapshot(applied) : null
        });
    }

    public static async Task<UseCaseResult> ReviewAmendmentAsync(Guid dayId, Guid requestId, ReviewInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        if (!ValidReason(input.Reason))
            return UseCaseResult.BadRequest(new MessageResponse("Cần lý do duyệt/từ chối 1–500 ký tự."));
        await using var transaction = await db.BeginTransactionAsync();
        var day = await LockDay(db, dayId);
        if (day is null)
            return UseCaseResult.NotFound();
        var request = await db.PortionAmendments.Include(db.Queries, "BaseSettlement").Include(db.Queries, "Resolution").FirstOrDefaultAsync(db.Queries, x => x.Id == requestId && x.BaseSettlement.MealDayId == dayId);
        if (request is null)
            return UseCaseResult.NotFound();
        if (request.Resolution is not null)
            return UseCaseResult.Conflict(new MessageResponse("Yêu cầu đã được xử lý."));
        var reviewer = await ActorName(db, principal);
        PortionSettlement? applied = null;
        if (input.Approve)
        {
            if (day.IsCancelled || day.SettledAt is null || clock.GetUtcNow() < day.CutoffAt)
                return UseCaseResult.Conflict(new MessageResponse("Phiên không đủ điều kiện áp dụng điều chỉnh."));
            var current = await Latest(db, dayId, request.BaseSettlement.ClassId!.Value);
            if (current is null || current.Id != request.BaseSettlementId)
                return UseCaseResult.Conflict(new MessageResponse("Bản nguồn đã lỗi thời. Từ chối và gửi yêu cầu mới sau khi đối chiếu."));
            if (current.Count != current.Students.Count)
                return UseCaseResult.Conflict(new MessageResponse("Bản nguồn thiếu danh sách trẻ để đối chiếu; chưa thể duyệt điều chỉnh."));
            if (request.WillEat && await EatingElsewhere(db, dayId, current.ClassId!.Value, request.StudentId))
                return UseCaseResult.Conflict(new MessageResponse("Trẻ vừa có suất ở lớp khác; cần đối chiếu trước khi duyệt."));
            applied = new PortionSettlement
            {

                MealDayId = dayId,

                ClassId = current.ClassId,

                ClassName = current.ClassName,

                CutoffAt = current.CutoffAt,

                Version = current.Version + 1,

                SupersedesId = current.Id,

                SettledAt = clock.GetUtcNow(),

                SettledBy = Actor(principal).ToString(),

                Reason = input.Reason.Trim(),

                Students = current.Students.Where(x => x.StudentId != request.StudentId).Select(x => new SettlementStudent
                {
                    StudentId = x.StudentId,
                    StudentName = x.StudentName
                }).ToList(),

                Decisions = current.Decisions.Where(x => x.StudentId != request.StudentId).Select(CloneDecision).ToList()

            };
            if (request.WillEat)
                applied.Students.Add(new SettlementStudent
                {
                    StudentId = request.StudentId,
                    StudentName = request.StudentName
                });
            var source = current.Decisions.FirstOrDefault(x => x.StudentId == request.StudentId);
            applied.Decisions.Add(new SettlementDecision
            {
                StudentId = request.StudentId,
                StudentName = request.StudentName,
                StudentCode = request.StudentCode,
                WillEat = request.WillEat,
                EnrollmentId = request.EnrollmentId,
                AbsenceId = source?.AbsenceId,
                ExceptionId = source?.ExceptionId,
                AmendmentId = request.Id,
                Source = "APPROVED_AMENDMENT"
            });
            applied.Count = applied.Students.Count;
            db.PortionSettlements.Add(applied);
        }

        var reviewedAt = clock.GetUtcNow();
        if (applied is not null)
            applied.SettledAt = reviewedAt;
        db.PortionAmendmentResolutions.Add(new PortionAmendmentResolution
        {
            AmendmentId = requestId,
            Approved = input.Approve,
            AppliedSettlement = applied,
            Reason = input.Reason.Trim(),
            ReviewedBy = Actor(principal),
            ReviewedByName = reviewer,
            ReviewedAt = reviewedAt
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Ok(new AmendmentReviewedResponse
        {
            Status = input.Approve ? "APPROVED" : "REJECTED",
            SettlementId = applied?.Id,
            Version = applied?.Version
        });
    }

    private static SettlementSnapshot Snapshot(PortionSettlement x) => new SettlementSnapshot
    {

        Id = x.Id,

        Version = x.Version,

        Count = x.Count,

        SettledAt = x.SettledAt,

        SettledBy = x.SettledBy,

        Reason = x.Reason,

        SupersedesId = x.SupersedesId,

        Students = x.Students.Select(s => new SettlementStudentSummary
        {
            StudentId = s.StudentId,
            StudentName = s.StudentName
        }),

        Decisions = x.Decisions.Select(s => new SettlementDecisionSummary
        {
            StudentId = s.StudentId,
            StudentName = s.StudentName,
            StudentCode = s.StudentCode,
            WillEat = s.WillEat,
            Source = s.Source,
            EnrollmentId = s.EnrollmentId,
            AbsenceId = s.AbsenceId,
            ExceptionId = s.ExceptionId,
            AmendmentId = s.AmendmentId
        })

    };
    private static SettlementDecision CloneDecision(SettlementDecision x) => new()
    {

        StudentId = x.StudentId,

        StudentName = x.StudentName,

        StudentCode = x.StudentCode,

        WillEat = x.WillEat,

        EnrollmentId = x.EnrollmentId,

        AbsenceId = x.AbsenceId,

        ExceptionId = x.ExceptionId,

        AmendmentId = x.AmendmentId,

        Source = x.Source

    };
}

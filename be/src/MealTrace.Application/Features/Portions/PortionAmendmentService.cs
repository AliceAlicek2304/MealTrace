using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Portions;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Portions;
public sealed class PortionAmendmentService(IPortionAmendmentRepository repository, ICurrentActor currentActor, TimeProvider clock, IUnitOfWork unitOfWork)
{
    private Guid Actor() => currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    private Task<bool> CanRead(Guid classId) => currentActor.IsInRole(RoleNames.Admin) || currentActor.IsInRole(RoleNames.KitchenStaff) ? Task.FromResult(true) : repository.HasTeacherAssignmentAsync(classId, Actor());
    private async Task<string> ActorName() => await repository.GetActorNameAsync(Actor());
    private Task<PortionSettlement?> Latest(Guid dayId, Guid classId) => repository.FindLatestSettlementAsync(dayId, classId);
    private async Task<MealDay?> LockDay(Guid id) => await repository.LockMealDayAsync(id);
    public async Task<Result<ClassAmendmentsResponse>> GetClassAmendmentsAsync(Guid dayId, Guid classId, string? q, int? page, int? candidatePage)
    {
        if (!await CanRead(classId))
            return Result.Forbidden();
        var day = await repository.FindMealDayAsync(dayId);
        if (day is null)
            return Result.NotFound();
        var current = await Latest(dayId, classId);
        if (current is null || day.SettledAt is null)
            return Result.Conflict("Lớp chưa có bản chốt suất.");
        var root = await repository.GetOriginalSettlementAsync(dayId, classId);
        var term = q?.Trim();
        var candidateNumber = Math.Clamp(candidatePage ?? 1, 1, 100000);
        var candidateTotal = await repository.CountAmendmentCandidatesAsync(day, classId, current, term);
        var candidates = await repository.SearchAmendmentCandidatesAsync(day, classId, current, term, candidateNumber);
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var total = await repository.CountAmendmentsAsync(dayId, classId);
        var items = await repository.ListAmendmentsAsync(dayId, classId, number);
        return Result.Success(new ClassAmendmentsResponse
        {
            ClassId = classId,
            ClassName = current.ClassName,
            HasCompleteRoster = current.Count - current.KitchenAdjustment == current.Students.Count,
            CanRequest = !day.IsCancelled && current.Count - current.KitchenAdjustment == current.Students.Count && (currentActor.IsInRole(RoleNames.Admin) || (currentActor.IsInRole(RoleNames.Teacher) && await repository.HasTeacherAssignmentAsync(classId, Actor()))),
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

    public async Task<Result<AmendmentCreatedResponse>> RequestAmendmentAsync(Guid dayId, RequestInput input)
    {
        if (!ValidReason(input.Reason))
            return Result.Invalid("Cần lý do 1–500 ký tự.");
        // Kitchen access never grants permission to submit a request outside the teacher's class.
        if (!currentActor.IsInRole(RoleNames.Admin) && !await repository.TeacherCanRequestAmendmentAsync(input, Actor()))
            return Result.Forbidden();
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var day = await LockDay(dayId);
        if (day is null)
            return Result.NotFound();
        if (day.IsCancelled || day.SettledAt is null || clock.GetUtcNow() < day.CutoffAt)
            return Result.Conflict("Chỉ điều chỉnh phiên đang hoạt động sau khi đã chốt.");
        var current = await Latest(dayId, input.ClassId);
        if (current is null)
            return Result.Invalid("Lớp chưa có bản chốt.");
        if (current.Count - current.KitchenAdjustment != current.Students.Count)
            return Result.Conflict("Bản cũ thiếu danh sách trẻ để đối chiếu tổng suất; chưa thể điều chỉnh theo trẻ.");
        if (current.Id != input.BaseSettlementId)
            return Result.Conflict("Bản suất đã thay đổi. Tải lại rồi đối chiếu yêu cầu mới.");
        var ids = input.StudentIds ?? (input.StudentId == Guid.Empty ? [] : new[] { input.StudentId });
        if (ids.Length > 200 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length
            || (input.StudentIds is not null && input.StudentId != Guid.Empty))
            return Result.Invalid("Chọn tối đa 200 trẻ khác nhau; không kết hợp hai cách chọn trẻ.");
        var quantityOnly = ids.Length == 0;
        var quantity = quantityOnly ? input.Quantity ?? 0 : ids.Length;
        if (quantity is < 1 or > 200 || (!quantityOnly && input.Quantity is not null && input.Quantity != quantity))
            return Result.Invalid("Số suất phải từ 1–200 và khớp số trẻ đã chọn.");
        if (!input.WillEat && current.Count < quantity)
            return Result.Invalid("Số suất giảm vượt số suất đang gửi bếp.");
        var selected = new List<PortionAmendmentStudent>();
        if (!quantityOnly)
        {
            if (await repository.HasPendingStudentsAsync(current.Id, ids))
                return Result.Conflict("Có trẻ đã có yêu cầu đang chờ xử lý trên bản suất này.");
            if (input.WillEat && await repository.HasAnyPortionElsewhereAsync(dayId, input.ClassId, ids))
                return Result.Conflict("Có trẻ đang có suất ở lớp khác trong cùng phiên.");
            var enrollments = await repository.FindCandidateEnrollmentsAsync(day, input.ClassId, ids);
            var students = (await repository.FindStudentsAsync(ids)).ToDictionary(x => x.Id);
            foreach (var id in ids)
            {
                var frozen = current.Decisions.FirstOrDefault(x => x.StudentId == id);
                var included = current.Students.FirstOrDefault(x => x.StudentId == id);
                var enrollment = enrollments.FirstOrDefault(x => x.StudentId == id);
                if (frozen is null && included is null && enrollment is null)
                    return Result.Invalid("Có trẻ không thuộc lớp tại ngày ăn hoặc bản suất nguồn.");
                if ((included is not null) == input.WillEat)
                    return Result.Invalid("Có trẻ không thay đổi trạng thái suất. Chọn trẻ phù hợp với tăng/giảm.");
                selected.Add(new PortionAmendmentStudent
                {
                    StudentId = id,
                    StudentName = frozen?.StudentName ?? included?.StudentName ?? enrollment!.Student.FullName,
                    StudentCode = frozen?.StudentCode ?? enrollment?.Student.StudentCode
                        ?? (students.TryGetValue(id, out var student) ? student.StudentCode : ""),
                    EnrollmentId = frozen?.EnrollmentId ?? enrollment?.Id
                });
            }
        }
        var first = selected.FirstOrDefault();
        var request = new PortionAmendment
        {
            BaseSettlementId = current.Id,
            StudentId = first?.StudentId ?? Guid.Empty,
            StudentName = first?.StudentName ?? "Điều chỉnh số lượng bếp",
            StudentCode = first?.StudentCode ?? "",
            EnrollmentId = first?.EnrollmentId,
            WasEating = !input.WillEat,
            WillEat = input.WillEat,
            Quantity = quantity,
            IsQuantityOnly = quantityOnly,
            Students = selected,
            Reason = input.Reason.Trim(),
            RequestedBy = Actor(),
            RequestedByName = await ActorName(),
            RequestedAt = clock.GetUtcNow()
        };
        repository.AddPortionAmendment(request);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new AmendmentCreatedResponse { Id = request.Id });
    }

    public async Task<Result<AmendmentComparisonResponse>> GetAmendmentAsync(Guid dayId, Guid requestId)
    {
        var request = await repository.FindAmendmentDetailsAsync(requestId, dayId);
        if (request is null)
            return Result.NotFound();
        if (request.BaseSettlement.ClassId is not Guid classId || !await CanRead(classId))
            return Result.Forbidden();
        return Result.Success(new AmendmentComparisonResponse
        {
            Before = Snapshot(request.BaseSettlement),
            After = request.Resolution?.AppliedSettlement is { } applied ? Snapshot(applied) : null
        });
    }

    public async Task<Result<AmendmentReviewedResponse>> ReviewAmendmentAsync(Guid dayId, Guid requestId, ReviewInput input)
    {
        if (!ValidReason(input.Reason))
            return Result.Invalid("Cần lý do duyệt/từ chối 1–500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var day = await LockDay(dayId);
        if (day is null)
            return Result.NotFound();
        var request = await repository.FindTrackedAmendmentAsync(requestId, dayId);
        if (request is null)
            return Result.NotFound();
        if (request.Resolution is not null)
            return Result.Conflict("Yêu cầu đã được xử lý.");
        var reviewer = await ActorName();
        PortionSettlement? applied = null;
        if (input.Approve)
        {
            if (day.IsCancelled || day.SettledAt is null || clock.GetUtcNow() < day.CutoffAt)
                return Result.Conflict("Phiên không đủ điều kiện áp dụng điều chỉnh.");
            var current = await Latest(dayId, request.BaseSettlement.ClassId!.Value);
            if (current is null || current.Id != request.BaseSettlementId)
                return Result.Conflict("Bản nguồn đã lỗi thời. Từ chối và gửi yêu cầu mới sau khi đối chiếu.");
            if (current.Count - current.KitchenAdjustment != current.Students.Count)
                return Result.Conflict("Bản nguồn thiếu danh sách trẻ để đối chiếu; chưa thể duyệt điều chỉnh.");
            var selected = request.Students.Count > 0 ? request.Students : request.IsQuantityOnly ? [] :
                new List<PortionAmendmentStudent> { new() { StudentId = request.StudentId,
                    StudentName = request.StudentName, StudentCode = request.StudentCode, EnrollmentId = request.EnrollmentId } };
            var ids = selected.Select(x => x.StudentId).ToArray();
            if (request.WillEat && !request.IsQuantityOnly
                && await repository.HasAnyPortionElsewhereAsync(dayId, current.ClassId!.Value, ids))
                return Result.Conflict("Có trẻ vừa có suất ở lớp khác; cần đối chiếu trước khi duyệt.");
            var delta = request.WillEat ? request.Quantity : -request.Quantity;
            if (current.Count + delta < 0)
                return Result.Conflict("Số suất sau điều chỉnh không được âm.");
            applied = new PortionSettlement
            {
                MealDayId = dayId,
                ClassId = current.ClassId,
                ClassName = current.ClassName,
                CutoffAt = current.CutoffAt,
                Version = current.Version + 1,
                SupersedesId = current.Id,
                SettledAt = clock.GetUtcNow(),
                SettledBy = Actor().ToString(),
                Reason = input.Reason.Trim(),
                KitchenAdjustment = current.KitchenAdjustment + (request.IsQuantityOnly ? delta : 0),
                Students = current.Students.Where(x => !ids.Contains(x.StudentId)).Select(x => new SettlementStudent
                { StudentId = x.StudentId, StudentName = x.StudentName }).ToList(),
                Decisions = current.Decisions.Where(x => !ids.Contains(x.StudentId)).Select(CloneDecision).ToList()
            };
            foreach (var child in selected)
            {
                if (request.WillEat)
                    applied.Students.Add(new SettlementStudent { StudentId = child.StudentId, StudentName = child.StudentName });
                var source = current.Decisions.FirstOrDefault(x => x.StudentId == child.StudentId);
                applied.Decisions.Add(new SettlementDecision
                {
                    StudentId = child.StudentId, StudentName = child.StudentName, StudentCode = child.StudentCode,
                    WillEat = request.WillEat, EnrollmentId = child.EnrollmentId,
                    AbsenceId = source?.AbsenceId, ExceptionId = source?.ExceptionId,
                    AmendmentId = request.Id, Source = "APPROVED_AMENDMENT"
                });
            }
            applied.Count = current.Count + delta;
            repository.AddPortionSettlement(applied);
        }

        var reviewedAt = clock.GetUtcNow();
        if (applied is not null)
            applied.SettledAt = reviewedAt;
        repository.AddPortionAmendmentResolution(new PortionAmendmentResolution
        {
            AmendmentId = requestId,
            Approved = input.Approve,
            AppliedSettlement = applied,
            Reason = input.Reason.Trim(),
            ReviewedBy = Actor(),
            ReviewedByName = reviewer,
            ReviewedAt = reviewedAt
        });
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new AmendmentReviewedResponse
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
        KitchenAdjustment = x.KitchenAdjustment,

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

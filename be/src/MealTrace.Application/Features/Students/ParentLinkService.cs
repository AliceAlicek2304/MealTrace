using System.Data;
using System.Text;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Students;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;

namespace MealTrace.Application.Features.Students;

public sealed class ParentLinkService(IParentLinkRepository repository, IIdentityService users, ICurrentActor actor, IUnitOfWork unitOfWork, TimeProvider clock)
{
    private bool Reviewer => actor.IsInRole(RoleNames.Admin) || actor.IsInRole(RoleNames.Teacher);
    private DateOnly Today => SchoolTime.Today(clock.GetUtcNow());
    private static string NameKey(string name) => string.Join(' ', name.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    public async Task<Result<ParentLinkRequestPage>> ListAsync(bool review, string? status, int? page, int? pageSize, Guid? classId = null, string? schoolYear = null)
    {
        if (actor.UserId is null || (review ? !Reviewer : !actor.IsInRole(RoleNames.Parent))) return Result.Forbidden();
        if (string.IsNullOrWhiteSpace(status)) status = null;
        if (status is not null && status is not ("PENDING" or "APPROVED" or "REJECTED" or "CANCELLED" or "REVOKED")) return Result.Invalid("Trạng thái không hợp lệ.");
        return Result.Success(await repository.ListAsync(review ? null : actor.UserId, review && !actor.IsInRole(RoleNames.Admin) ? actor.UserId : null,
            Today, status, Math.Clamp(page ?? 1, 1, 100000), Math.Clamp(pageSize ?? 25, 1, 100), review ? classId : null, review ? schoolYear : null));
    }

    public async Task<Result<Unit>> CreateAsync(CreateParentLinkRequest input)
    {
        if (!actor.IsInRole(RoleNames.Parent) || actor.UserId is not Guid parentId) return Result.Forbidden();
        var code = input.StudentCode?.Trim(); var name = input.StudentName?.Trim();
        var relationship = input.Relationship?.Trim(); var note = input.Note?.Trim() ?? "";
        if ((string.IsNullOrWhiteSpace(code) && !input.ClassId.HasValue) || code?.Length > 40 || string.IsNullOrWhiteSpace(name) || name.Length > 150 ||
            relationship is not ("FATHER" or "MOTHER" or "GUARDIAN") || note.Length > 500 || name.Any(char.IsControl))
            return Result.Invalid("Cần chọn lớp, nhập họ tên trẻ, quan hệ và ghi chú tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        Student? student;
        if (input.ClassId.HasValue)
        {
            var classId = input.ClassId!.Value;
            if (classId == Guid.Empty) return Result.Invalid("Vui lòng chọn lớp.");
            if (!string.IsNullOrWhiteSpace(code))
            {
                student = await repository.FindStudentByCodeAsync(code);
                if (student is not null && await repository.CurrentClassAsync(student.Id, Today) != classId) student = null;
            }
            else
            {
                var candidates = await repository.FindClassStudentsByNameAsync(classId, Today, string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
                if (candidates.Count > 1) return Result.Invalid("Có trẻ trùng họ tên trong lớp. Vui lòng liên hệ giáo viên để đối chiếu và liên kết đúng trẻ.");
                student = candidates.SingleOrDefault();
            }
        }
        else student = await repository.FindStudentByCodeAsync(code!);
        if (student is null || !student.IsActive || NameKey(student.FullName) != NameKey(name))
            return Result.Invalid("Không đối chiếu được lớp và họ tên trẻ. Vui lòng kiểm tra với nhà trường.");
        if (await repository.LinkedAsync(parentId, student.Id)) return Result.Conflict("Bạn đã được liên kết với trẻ này.");
        if (await repository.PendingAsync(parentId, student.Id)) return Result.Conflict("Đã có yêu cầu đang chờ duyệt cho trẻ này.");
        if (await repository.PendingCountAsync(parentId) >= 10) return Result.Conflict("Bạn đang có 10 yêu cầu chờ duyệt. Vui lòng liên hệ nhà trường.");
        repository.Add(new ParentLinkRequest
        {
            ParentId = parentId,
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            Relationship = relationship,
            Note = note,
            RequestedAt = clock.GetUtcNow()
        });
        await unitOfWork.SaveChangesAsync(); await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<Unit>> CancelAsync(Guid id, CancelParentLinkRequest input)
    {
        if (!actor.IsInRole(RoleNames.Parent) || actor.UserId is not Guid parentId) return Result.Forbidden();
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await repository.FindAsync(id);
        if (request is null || request.ParentId != parentId) return Result.NotFound();
        if (request.Status != "PENDING" || request.Revision != input.Revision) return Conflict();
        request.Status = "CANCELLED"; request.Revision++; request.ReviewedAt = clock.GetUtcNow();
        await unitOfWork.SaveChangesAsync(); await transaction.CommitAsync(); return Result.Success(Unit.Value);
    }

    public async Task<Result<Unit>> ReviewAsync(Guid id, ReviewParentLinkRequest input)
    {
        if (!Reviewer || actor.UserId is not Guid reviewerId) return Result.Forbidden();
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return Result.Invalid("Cần ghi kết quả đối chiếu/lý do quyết định, tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await ReviewOneAsync(id, input.Revision, input.Approve, reason, reviewerId);
        if (!result.IsSuccess) return result;
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<List<ParentLinkClass>>> ClassesAsync(bool review = true)
    {
        if (actor.UserId is null || (review ? !Reviewer : !actor.IsInRole(RoleNames.Parent))) return Result.Forbidden();
        return Result.Success(await repository.ClassesAsync(review && !actor.IsInRole(RoleNames.Admin) ? actor.UserId : null));
    }

    public async Task<Result<Unit>> BulkReviewAsync(BulkReviewParentLinks input)
    {
        if (!Reviewer || actor.UserId is not Guid reviewerId) return Result.Forbidden();
        var reason = input.Reason?.Trim();
        if (input.ClassId == Guid.Empty || (!input.ClassId.HasValue && string.IsNullOrWhiteSpace(input.SchoolYear)) || input.SchoolYear?.Length > 20 || input.Items is null || input.Items.Count is < 1 or > 100 ||
            input.Items.Any(x => x.Id == Guid.Empty || x.Revision < 0) || input.Items.Select(x => x.Id).Distinct().Count() != input.Items.Count ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return Result.Invalid("Chọn năm học hoặc lớp, từ 1 đến 100 yêu cầu và ghi kết quả đối chiếu, tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        HashSet<Guid>? yearClasses = null;
        if (!string.IsNullOrWhiteSpace(input.SchoolYear))
            yearClasses = (await repository.ClassesAsync(null)).Where(x => x.SchoolYear == input.SchoolYear).Select(x => x.Id).ToHashSet();
        foreach (var item in input.Items.OrderBy(x => x.Id))
        {
            var result = await ReviewOneAsync(item.Id, item.Revision, input.Approve, reason, reviewerId, input.ClassId, yearClasses);
            if (!result.IsSuccess) return result;
        }
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<Unit>> RevokeAsync(Guid id, RevokeParentLink input)
    {
        if (!Reviewer || actor.UserId is not Guid reviewerId) return Result.Forbidden();
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return Result.Invalid("Cần lý do thu hồi, tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await repository.FindAsync(id);
        if (request is null || (!actor.IsInRole(RoleNames.Admin) && !await repository.TeacherCanManageAsync(reviewerId, request.StudentId, Today))) return Result.NotFound();
        if (request.Status != "APPROVED" || request.Revision != input.Revision) return Conflict();
        await repository.UnlinkAsync(request.ParentId, request.StudentId);
        request.Status = "REVOKED";
        request.Revision++;
        request.RevokedAt = clock.GetUtcNow();
        request.RevokedByUserId = reviewerId;
        request.RevocationReason = reason;
        var parent = await users.FindByIdAsync(request.ParentId.ToString());
        if (parent is not null && !(await users.UpdateSecurityStampAsync(parent)).Succeeded)
            return Result.Conflict("Không thể thu hồi phiên đăng nhập cũ. Vui lòng thử lại.");
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    private async Task<Result<Unit>> ReviewOneAsync(Guid id, int revision, bool approve, string reason, Guid reviewerId, Guid? classId = null, HashSet<Guid>? yearClasses = null)
    {
        var request = await repository.FindAsync(id);
        if (request is not null && classId.HasValue && await repository.CurrentClassAsync(request.StudentId, Today) != classId) return Result.Conflict("Danh sách lớp đã thay đổi. Vui lòng tải lại.");
        if (request is null || (!actor.IsInRole(RoleNames.Admin) && !await repository.TeacherCanManageAsync(reviewerId, request.StudentId, Today))) return Result.NotFound();
        if (yearClasses is not null && (await repository.CurrentClassAsync(request.StudentId, Today) is not Guid currentClass || !yearClasses.Contains(currentClass)))
            return Result.Conflict("Danh sách năm học đã thay đổi. Vui lòng tải lại.");
        if (request.ParentId == reviewerId) return Result.Forbidden();
        if (request.Status != "PENDING" || request.Revision != revision) return Conflict();
        if (approve)
        {
            var parent = await users.FindByIdAsync(request.ParentId.ToString());
            var student = await repository.FindStudentAsync(request.StudentId);
            if (parent is null || !parent.IsActive || !await users.IsInRoleAsync(parent, RoleNames.Parent) || student is null || !student.IsActive)
                return Result.Conflict("Tài khoản phụ huynh hoặc trẻ không còn hợp lệ. Vui lòng kiểm tra lại.");
            if (!await repository.LinkedAsync(request.ParentId, request.StudentId)) repository.Link(request.ParentId, request.StudentId);
        }
        request.Status = approve ? "APPROVED" : "REJECTED"; request.Revision++;
        request.ReviewedAt = clock.GetUtcNow(); request.ReviewedByUserId = reviewerId; request.ReviewReason = reason;
        return Result.Success(Unit.Value);
    }
    private static Result<Unit> Conflict() => Result.Conflict("Yêu cầu đã được xử lý hoặc thay đổi. Vui lòng tải lại.");
}

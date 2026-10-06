using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Meals;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Meals;
public sealed class MealExceptionUseCases(IMealExceptionRepository repository, MealDecisionService decisionService, ICurrentActor currentActor, TimeProvider clock, IUnitOfWork unitOfWork)
{
    // WillEat preserves the old payload for a first override. New clients use Action and ExpectedEventId.
    public async Task<Result<MealDecisionsResponse>> GetDecisionsAsync(Guid id, Guid? classId, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var day = await repository.FindMealDayAsync(id, ct);
        if (day is null)
            return Result.NotFound();
        var allowed = await AllowedClasses(ct);
        if (classId.HasValue && allowed is not null && !allowed.Contains(classId.Value))
            return Result.Forbidden();
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var now = clock.GetUtcNow();
        var result = await decisionService.ReadPageAsync(day, now, allowed, classId, search, number, size, ct);
        return Result.Success(new MealDecisionsResponse
        {
            Id = day.Id,
            Date = day.Date,
            CutoffAt = day.CutoffAt,
            IsSettled = day.SettledAt != null,
            IsCancelled = day.IsCancelled,
            CancellationReason = day.CancellationReason,
            CanEdit = !day.IsCancelled && day.SettledAt == null && now < day.CutoffAt,
            AsOf = now < day.CutoffAt ? now : day.CutoffAt,
            Classes = result.Classes,
            Items = result.Items,
            Total = result.Total,
            Page = number,
            PageSize = size
        });
    }

    public async Task<Result<MealExceptionHistoryResponse>> GetStudentHistoryAsync(Guid id, Guid studentId, int? page, int? pageSize)
    {
        var day = await repository.FindHistoryMealDayAsync(id);
        if (day is null)
            return Result.NotFound();
        var member = await Member(day, studentId, clock.GetUtcNow());
        if (member is null)
            return Result.NotFound();
        if (day.SchoolYear is not null && member.Class.SchoolYear != day.SchoolYear)
            return Result.Invalid("Trẻ không thuộc niên khóa của phiên ăn.");
        if (!await CanEditClass(member.ClassId))
            return Result.Forbidden();
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var total = await repository.CountStudentExceptionsAsync(id, studentId);
        var records = await repository.ListStudentExceptionsAsync(id, studentId, number, size);
        return Result.Success(new MealExceptionHistoryResponse
        {
            Items = records.Select(x => new MealExceptionHistoryItem
            {
                Id = x.Id,
                Action = MealDecisionService.Action(x.WillEat),
                Reason = x.Reason,
                RecordedAt = x.RecordedAt,
                RecordedByUserId = x.RecordedByUserId,
                ActorName = x.RecordedByName,
                IsLegacy = x.RecordedByUserId == null,
                SupersedesId = x.SupersedesId,
                Sequence = x.Sequence
            }),
            Total = total,
            Page = number,
            PageSize = size
        });
    }

    public async Task<Result<MealExceptionCreatedResponse>> RecordExceptionAsync(Guid id, ExceptionInput input)
    {
        var action = input.Action?.Trim().ToUpperInvariant();
        if (action is null && input.WillEat.HasValue)
            action = MealDecisionService.Action(input.WillEat);
        if (action is not ("EAT" or "ABSENT" or "DEFAULT") || (input.WillEat.HasValue && action != MealDecisionService.Action(input.WillEat)) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return Result.Invalid("Chọn có suất/vắng/khôi phục mặc định và nhập lý do tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        // Serializes overrides and settlement on the same day. Recheck the clock after waiting for locks.
        var day = await repository.LockMealDayAsync(id);
        if (day is null)
            return Result.NotFound();
        if (day.IsCancelled || day.SettledAt is not null || clock.GetUtcNow() >= day.CutoffAt)
            return Closed();
        await repository.LockStudentAsync(input.StudentId);
        var member = await Member(day, input.StudentId, clock.GetUtcNow());
        if (member is null)
            return Result.NotFound();
        if (day.SchoolYear is not null && member.Class.SchoolYear != day.SchoolYear)
            return Result.Invalid("Trẻ không thuộc niên khóa của phiên ăn.");
        if (!await CanEditClass(member.ClassId))
            return Result.Forbidden();
        var latest = await repository.FindLatestExceptionAsync(id, input);
        if (latest?.Id != input.ExpectedEventId)
            return Stale();
        var now = clock.GetUtcNow();
        if (now >= day.CutoffAt)
            return Closed();
        var actor = await repository.GetActorNameAsync(UserId());
        var record = new MealRegistration
        {
            MealDayId = id,

            StudentId = input.StudentId,

            WillEat = action == "DEFAULT" ? null : action == "EAT",

            Reason = input.Reason.Trim(),

            RecordedByUserId = UserId(),

            RecordedByName = actor,

            RecordedAt = now,

            SupersedesId = latest?.Id,

            Sequence = checked((latest?.Sequence ?? 0) + 1)
        };
        // The authoritative acceptance time is sampled immediately before persisting.
        record.RecordedAt = clock.GetUtcNow();
        if (record.RecordedAt >= day.CutoffAt)
            return Closed();
        day.DecisionRevision++;
        repository.AddMealRegistration(record);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new MealExceptionCreatedResponse
        {
            Id = record.Id,
            Action = action,
            RecordedAt = record.RecordedAt,
            SupersedesId = record.SupersedesId,
            Sequence = record.Sequence
        });
    }

    private Guid UserId() => currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
    private async Task<Guid[]?> AllowedClasses(CancellationToken ct = default) => currentActor.IsInRole(RoleNames.Admin) ? null : await repository.ListAssignedClassIdsAsync(ct, UserId());
    private async Task<bool> CanEditClass(Guid classId) => currentActor.IsInRole(RoleNames.Admin) || await repository.HasTeacherAssignmentAsync(classId, UserId());
    private Task<Enrollment?> Member(MealDay day, Guid studentId, DateTimeOffset now) => repository.FindEnrollmentAtCutoffAsync(day, studentId, now);
    private static Failure Closed() => Result.Conflict("Đã qua giờ chốt hoặc phiên đã chốt; không thể đổi số suất dự kiến.");
    public static Failure Stale() => Result.Conflict("Ngoại lệ đã được cập nhật. Hãy tải lại danh sách và mở lại form trước khi lưu.");
}

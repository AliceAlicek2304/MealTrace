using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Meals;
using System.Security.Claims;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features;
public static class MealExceptionUseCases
{
    // WillEat preserves the old payload for a first override. New clients use Action and ExpectedEventId.
    public static async Task<UseCaseResult> GetDecisionsAsync(Guid id, Guid? classId, string? search, int? page, int? pageSize, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock, CancellationToken ct)
    {
        var day = await db.MealDays.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == id, ct);
        if (day is null)
            return UseCaseResult.NotFound();
        var allowed = await AllowedClasses(db, principal, ct);
        if (classId.HasValue && allowed is not null && !allowed.Contains(classId.Value))
            return UseCaseResult.Forbid();
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var now = clock.GetUtcNow();
        var result = await MealDecisionService.ReadPageAsync(db, day, now, allowed, classId, search, number, size, ct);
        return UseCaseResult.Ok(new MealDecisionsResponse
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

    public static async Task<UseCaseResult> GetStudentHistoryAsync(Guid id, Guid studentId, int? page, int? pageSize, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        var day = await db.MealDays.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == id);
        if (day is null)
            return UseCaseResult.NotFound();
        var member = await Member(db, day, studentId, clock.GetUtcNow());
        if (member is null)
            return UseCaseResult.NotFound();
        if (day.SchoolYear is not null && member.Class.SchoolYear != day.SchoolYear)
            return UseCaseResult.BadRequest(new MessageResponse("Trẻ không thuộc niên khóa của phiên ăn."));
        if (!await CanEditClass(db, principal, member.ClassId))
            return UseCaseResult.Forbid();
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.MealRegistrations.AsNoTracking(db.Queries).Where(x => x.MealDayId == id && x.StudentId == studentId);
        var total = await query.CountAsync(db.Queries);
        var records = await query.OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Skip((number - 1) * size).Take(size).ToListAsync(db.Queries);
        return UseCaseResult.Ok(new MealExceptionHistoryResponse
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

    public static async Task<UseCaseResult> RecordExceptionAsync(Guid id, ExceptionInput input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        var action = input.Action?.Trim().ToUpperInvariant();
        if (action is null && input.WillEat.HasValue)
            action = MealDecisionService.Action(input.WillEat);
        if (action is not ("EAT" or "ABSENT" or "DEFAULT") || (input.WillEat.HasValue && action != MealDecisionService.Action(input.WillEat)) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return UseCaseResult.BadRequest(new MessageResponse("Chọn có suất/vắng/khôi phục mặc định và nhập lý do tối đa 500 ký tự."));
        await using var transaction = await db.BeginTransactionAsync();
        // Serializes overrides and settlement on the same day. Recheck the clock after waiting for locks.
        var day = await db.LockMealDayAsync(id);
        if (day is null)
            return UseCaseResult.NotFound();
        if (day.IsCancelled || day.SettledAt is not null || clock.GetUtcNow() >= day.CutoffAt)
            return Closed();
        await db.LockStudentAsync(input.StudentId);
        var member = await Member(db, day, input.StudentId, clock.GetUtcNow());
        if (member is null)
            return UseCaseResult.NotFound();
        if (day.SchoolYear is not null && member.Class.SchoolYear != day.SchoolYear)
            return UseCaseResult.BadRequest(new MessageResponse("Trẻ không thuộc niên khóa của phiên ăn."));
        if (!await CanEditClass(db, principal, member.ClassId))
            return UseCaseResult.Forbid();
        var latest = await db.MealRegistrations.AsNoTracking(db.Queries).Where(x => x.MealDayId == id && x.StudentId == input.StudentId).OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(db.Queries);
        if (latest?.Id != input.ExpectedEventId)
            return Stale();
        var now = clock.GetUtcNow();
        if (now >= day.CutoffAt)
            return Closed();
        var actor = await db.Users.AsNoTracking(db.Queries).Where(x => x.Id == UserId(principal)).Select(x => x.FullName).SingleAsync(db.Queries);
        var record = new MealRegistration
        {

            MealDayId = id,

            StudentId = input.StudentId,

            WillEat = action == "DEFAULT" ? null : action == "EAT",

            Reason = input.Reason.Trim(),

            RecordedByUserId = UserId(principal),

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
        db.MealRegistrations.Add(record);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Ok(new MealExceptionCreatedResponse
        {
            Id = record.Id,
            Action = action,
            RecordedAt = record.RecordedAt,
            SupersedesId = record.SupersedesId,
            Sequence = record.Sequence
        });
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirst("sub")?.Value!);
    private static async Task<Guid[]?> AllowedClasses(IMealTraceData db, ClaimsPrincipal principal, CancellationToken ct = default) => principal.IsInRole(RoleNames.Admin) ? null : await db.TeacherAssignments.AsNoTracking(db.Queries).Where(x => x.UserId == UserId(principal)).Select(x => x.ClassId).ToArrayAsync(db.Queries, ct);
    private static async Task<bool> CanEditClass(IMealTraceData db, ClaimsPrincipal principal, Guid classId) => principal.IsInRole(RoleNames.Admin) || await db.TeacherAssignments.AnyAsync(db.Queries, x => x.UserId == UserId(principal) && x.ClassId == classId);
    private static Task<Enrollment?> Member(IMealTraceData db, MealDay day, Guid studentId, DateTimeOffset now) => StudentAdministrationUseCases.OnDate(db, day.Date).Include(db.Queries, "Class").FirstOrDefaultAsync(db.Queries, x => x.StudentId == studentId && x.RecordedAt <= (now < day.CutoffAt ? now : day.CutoffAt));
    private static UseCaseResult Closed() => UseCaseResult.Conflict(new MessageResponse("Đã qua giờ chốt hoặc phiên đã chốt; không thể đổi số suất dự kiến."));
    public static UseCaseResult Stale() => UseCaseResult.Conflict(new MessageResponse("Ngoại lệ đã được cập nhật. Hãy tải lại danh sách và mở lại form trước khi lưu."));
}

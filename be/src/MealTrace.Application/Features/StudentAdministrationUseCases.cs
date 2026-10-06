using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Students;
using System.Security.Claims;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;

namespace MealTrace.Application.Features;
public static class StudentAdministrationUseCases
{
    public static IQueryable<Enrollment> OnDate(IMealTraceData db, DateOnly date) => db.Enrollments.AsNoTracking(db.Queries).Where(x => x.StartDate <= date && (x.EndDate == null || x.EndDate > date));
    public static async Task<UseCaseResult> SearchClassesAsync(string? search, int? page, int? pageSize, IMealTraceData db, TimeProvider clock)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var query = db.Classes.AsNoTracking(db.Queries);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
        }

        var date = SchoolTime.Today(clock.GetUtcNow());
        var total = await query.CountAsync(db.Queries);
        var items = await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size).Select(x => new ClassSummary
        {
            Id = x.Id,
            Name = x.Name,
            SchoolYear = x.SchoolYear,
            StudentCount = db.Enrollments.Count(e => e.ClassId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date))
        }).ToListAsync(db.Queries);
        return UseCaseResult.Ok(new ClassListResponse
        {
            Items = items,
            Total = total,
            Page = number,
            PageSize = size
        });
    }

    public static async Task<UseCaseResult> EditClassAsync(Guid id, EditClass input, IMealTraceData db)
    {
        var room = await db.Classes.FindAsync(id);
        if (room is null)
            return UseCaseResult.NotFound();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return UseCaseResult.BadRequest(new MessageResponse("Tên lớp không hợp lệ."));
        if (await db.Classes.AnyAsync(db.Queries, x => x.Id != id && x.SchoolYear == room.SchoolYear && x.Name == name))
            return UseCaseResult.Conflict(new MessageResponse("Lớp đã tồn tại trong niên khóa."));
        room.Name = name;
        await db.SaveChangesAsync();
        return UseCaseResult.NoContent();
    }

    public static async Task<UseCaseResult> SearchStudentsAsync(Guid? classId, string? search, string? status, int? page, int? pageSize, IMealTraceData db, TimeProvider clock)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var now = clock.GetUtcNow();
        var date = SchoolTime.Today(now);
        var query = db.Students.AsNoTracking(db.Queries);
        if (classId.HasValue)
            query = query.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)) || (!db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)) && x.ClassId == classId));
        if (status == "ACTIVE")
            query = query.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
        else if (status == "INACTIVE")
            query = query.Where(x => !db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
        }

        var total = await query.CountAsync(db.Queries);
        var rows = await query.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size).Select(x => new StudentSummaryRow
        {
            Id = x.Id,
            StudentCode = x.StudentCode,
            FullName = x.FullName,
            Revision = x.Revision,
            IsActive = db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)),
            ClassId = db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)).Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? x.ClassId,
            ClassName = db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)).Select(e => e.Class.Name).FirstOrDefault() ?? x.Class.Name
        }).ToListAsync(db.Queries);
        var ids = rows.Select(x => x.Id).ToArray();
        var parents = await db.ParentStudents.AsNoTracking(db.Queries).Where(x => ids.Contains(x.StudentId)).Join(db.Users, link => link.UserId, user => user.Id, (link, user) => new ParentStudentRow
        {
            StudentId = link.StudentId,
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        }).ToListAsync(db.Queries);
        return UseCaseResult.Ok(new StudentListResponse
        {
            Items = rows.Select(x => new StudentSummary
            {
                Id = x.Id,
                StudentCode = x.StudentCode,
                FullName = x.FullName,
                Revision = x.Revision,
                IsActive = x.IsActive,
                ClassId = x.ClassId,
                ClassName = x.ClassName,
                Parents = parents.Where(p => p.StudentId == x.Id).Select(p => new ParentSummary
                {
                    Id = p.Id,
                    FullName = p.FullName,
                    Email = p.Email,
                    PhoneNumber = p.PhoneNumber
                })
            }),
            Total = total,
            Page = number,
            PageSize = size,
            EarliestChangeDate = SchoolTime.EarliestEnrollmentDate(now)
        });
    }

    public static async Task<UseCaseResult> EditStudentAsync(Guid id, EditStudent input, IMealTraceData db)
    {
        var student = await db.Students.FindAsync(id);
        if (student is null)
            return UseCaseResult.NotFound();
        if (student.Revision != input.Revision)
            return Conflict();
        var name = input.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return UseCaseResult.BadRequest(new MessageResponse("Họ tên trẻ không hợp lệ."));
        student.FullName = name;
        student.Revision++;
        await db.SaveChangesAsync();
        return UseCaseResult.NoContent();
    }

    public static async Task<UseCaseResult> GetEnrollmentHistoryAsync(Guid id, IMealTraceData db)
    {
        if (!await db.Students.AnyAsync(db.Queries, x => x.Id == id))
            return UseCaseResult.NotFound();
        return UseCaseResult.Ok(await db.Enrollments.AsNoTracking(db.Queries).Where(x => x.StudentId == id).OrderBy(x => x.StartDate).Select(x => new EnrollmentHistoryItem
        {
            Id = x.Id,
            ClassId = x.ClassId,
            ClassName = x.Class.Name,
            SchoolYear = x.Class.SchoolYear,
            StartDate = x.StartDate,
            EndDate = x.EndDate,
            Reason = x.Reason,
            EndReason = x.EndReason,
            RecordedAt = x.RecordedAt,
            RecordedByUserId = x.RecordedByUserId,
            EndRecordedAt = x.EndRecordedAt,
            EndedByUserId = x.EndedByUserId
        }).ToListAsync(db.Queries));
    }

    public static async Task<UseCaseResult> ChangeEnrollmentAsync(Guid id, ChangeEnrollment input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return UseCaseResult.BadRequest(new MessageResponse("Cần lý do và ngày hiệu lực từ ngày cho phép. Sau 07:30, thay đổi áp dụng từ ngày mai."));
        await using var transaction = await db.BeginTransactionAsync();
        // Same student lock as absence/exception writes. Read revision and enrollment after waiting.
        await db.LockStudentAsync(id);
        var student = await db.Students.Include(db.Queries, "Enrollments").FirstOrDefaultAsync(db.Queries, x => x.Id == id);
        if (student is null)
            return UseCaseResult.NotFound();
        if (student.Revision != input.Revision)
            return Conflict();
        var latest = student.Enrollments.OrderByDescending(x => x.StartDate).FirstOrDefault();
        if (latest is not null && (input.EffectiveDate <= latest.StartDate || latest.EndDate > input.EffectiveDate))
            return UseCaseResult.Conflict(new MessageResponse("Ngày hiệu lực phải sau ngày bắt đầu lần ghi danh gần nhất và không chồng lịch sử."));
        if (input.ClassId.HasValue)
        {
            if (!await db.Classes.AnyAsync(db.Queries, x => x.Id == input.ClassId))
                return UseCaseResult.BadRequest(new MessageResponse("Lớp không tồn tại."));
            if (latest is { EndDate: null } && latest.ClassId == input.ClassId)
                return UseCaseResult.BadRequest(new MessageResponse("Trẻ đã được ghi danh vào lớp này."));
        }
        else if (latest is null || latest.EndDate is not null)
            return UseCaseResult.Conflict(new MessageResponse("Trẻ không có ghi danh đang mở để ngừng học."));
        var acceptedAt = clock.GetUtcNow();
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(acceptedAt))
            return EnrollmentTimeExpired();
        var actor = Guid.Parse(principal.FindFirst("sub")?.Value!);
        var reason = input.Reason.Trim();
        if (latest is { EndDate: null })
        {
            latest.EndDate = input.EffectiveDate;
            latest.EndReason = reason;
            latest.EndedByUserId = actor;
            latest.EndRecordedAt = acceptedAt;
        }

        student.Revision++;
        // Save the closed enrollment before inserting another open one (partial unique index).
        await db.SaveChangesAsync();
        // A blocked UPDATE may have crossed cutoff. Roll back the whole transfer, including the closed enrollment.
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()))
            return EnrollmentTimeExpired();
        if (input.ClassId.HasValue)
        {
            db.Enrollments.Add(new Enrollment
            {
                StudentId = id,
                ClassId = input.ClassId.Value,
                StartDate = input.EffectiveDate,
                Reason = reason,
                RecordedByUserId = actor,
                RecordedAt = acceptedAt
            });
            student.ClassId = input.ClassId.Value;
        }

        // Legacy flag means an open or future enrollment exists. Current status is always date-derived.
        student.IsActive = input.ClassId.HasValue || input.EffectiveDate > SchoolTime.Today(acceptedAt);
        await db.SaveChangesAsync();
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()))
            return EnrollmentTimeExpired();
        await transaction.CommitAsync();
        return UseCaseResult.NoContent();
    }

    public static UseCaseResult Conflict() => UseCaseResult.Conflict(new MessageResponse("Hồ sơ đã được người khác cập nhật. Hãy đóng cửa sổ và tải lại trước khi sửa."));
    private static UseCaseResult EnrollmentTimeExpired() => UseCaseResult.Conflict(new MessageResponse("Đã qua giờ chốt hoặc sang ngày mới trong lúc lưu. Tải lại ngày hiệu lực cho phép trước khi sửa."));
}

using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Students;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Time;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Students;
public sealed class StudentAdministrationService(IStudentAdministrationRepository repository, TimeProvider clock, ICurrentActor currentActor, IUnitOfWork unitOfWork)
{
    public async Task<Result<ClassListResponse>> SearchClassesAsync(string? search, int? page, int? pageSize)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 20, 1, 100);

        var date = SchoolTime.Today(clock.GetUtcNow());
        var total = await repository.CountClassesAsync(search);
        var items = await repository.SearchClassesAsync(search, number, size, date);
        return Result.Success(new ClassListResponse
        {
            Items = items,
            Total = total,
            Page = number,
            PageSize = size
        });
    }

    public async Task<Result<Unit>> EditClassAsync(Guid id, EditClass input)
    {
        var room = await repository.FindTrackedClassAsync(id);
        if (room is null)
            return Result.NotFound();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Invalid("Tên lớp không hợp lệ.");
        if (await repository.ClassNameUsedByOtherClassAsync(id, room, name))
            return Result.Conflict("Lớp đã tồn tại trong niên khóa.");
        room.Name = name;
        await unitOfWork.SaveChangesAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<StudentListResponse>> SearchStudentsAsync(Guid? classId, string? search, string? status, int? page, int? pageSize, string? parentStatus = null)
    {
        if (!currentActor.IsInRole(RoleNames.Admin) && (!currentActor.IsInRole(RoleNames.Teacher) || currentActor.UserId is null))
            return Result.Forbidden();
        var teacherId = currentActor.IsInRole(RoleNames.Admin) ? null : currentActor.UserId;
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var now = clock.GetUtcNow();
        var date = SchoolTime.Today(clock.GetUtcNow());

        var total = await repository.CountStudentsAsync(classId, date, status, search, teacherId, parentStatus);
        var rows = await repository.SearchStudentsAsync(classId, date, status, search, number, size, teacherId, parentStatus);
        var ids = rows.Select(x => x.Id).ToArray();
        var parents = await repository.ListStudentParentsAsync(ids);
        return Result.Success(new StudentListResponse
        {
            Items = rows.Select(x => new StudentSummary
            {
                Id = x.Id,
                StudentCode = x.StudentCode,
                FullName = x.FullName,
                DateOfBirth = x.DateOfBirth,
                Gender = x.Gender,
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

    public async Task<Result<Unit>> EditStudentAsync(Guid id, EditStudent input)
    {
        var student = await repository.FindTrackedStudentAsync(id);
        if (student is null)
            return Result.NotFound();
        if (student.Revision != input.Revision)
            return Conflict();
        var name = input.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return Result.Invalid("Họ tên trẻ không hợp lệ.");
        if (input.DateOfBirth > SchoolTime.Today(clock.GetUtcNow()) || input.Gender is not (null or "MALE" or "FEMALE" or "OTHER"))
            return Result.Invalid("Ngày sinh hoặc giới tính không hợp lệ.");
        student.FullName = name;
        if (input.UpdateProfile || input.DateOfBirth.HasValue) student.DateOfBirth = input.DateOfBirth;
        if (input.UpdateProfile || input.Gender is not null) student.Gender = input.Gender;
        student.Revision++;
        await unitOfWork.SaveChangesAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<List<EnrollmentHistoryItem>>> GetEnrollmentHistoryAsync(Guid id)
    {
        if (!await repository.StudentExistsAsync(id))
            return Result.NotFound();
        return Result.Success(await repository.ListEnrollmentHistoryAsync(id));
    }

    public async Task<Result<Unit>> ChangeEnrollmentAsync(Guid id, ChangeEnrollment input)
    {
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return Result.Invalid("Cần lý do và ngày hiệu lực từ ngày cho phép. Sau 07:30, thay đổi áp dụng từ ngày mai.");
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        // Same student lock as absence/exception writes. Read revision and enrollment after waiting.
        await repository.LockStudentAsync(id);
        var student = await repository.FindStudentWithEnrollmentsAsync(id);
        if (student is null)
            return Result.NotFound();
        if (student.Revision != input.Revision)
            return Conflict();
        var latest = student.Enrollments.OrderByDescending(x => x.StartDate).FirstOrDefault();
        if (latest is not null && (input.EffectiveDate <= latest.StartDate || latest.EndDate > input.EffectiveDate))
            return Result.Conflict("Ngày hiệu lực phải sau ngày bắt đầu lần ghi danh gần nhất và không chồng lịch sử.");
        if (input.ClassId.HasValue)
        {
            if (!await repository.ClassExistsAsync(input))
                return Result.Invalid("Lớp không tồn tại.");
            if (latest is { EndDate: null } && latest.ClassId == input.ClassId)
                return Result.Invalid("Trẻ đã được ghi danh vào lớp này.");
        }
        else if (latest is null || latest.EndDate is not null)
            return Result.Conflict("Trẻ không có ghi danh đang mở để ngừng học.");
        var acceptedAt = clock.GetUtcNow();
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(acceptedAt))
            return EnrollmentTimeExpired();
        var actor = currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
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
        await unitOfWork.SaveChangesAsync();
        // A blocked UPDATE may have crossed cutoff. Roll back the whole transfer, including the closed enrollment.
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()))
            return EnrollmentTimeExpired();
        if (input.ClassId.HasValue)
        {
            repository.AddEnrollment(new Enrollment
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
        student.IsActive = input.ClassId.HasValue || input.EffectiveDate > SchoolTime.Today(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync();
        if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()))
            return EnrollmentTimeExpired();
        await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    public static Failure Conflict() => Result.Conflict("Hồ sơ đã được người khác cập nhật. Hãy đóng cửa sổ và tải lại trước khi sửa.");
    private static Failure EnrollmentTimeExpired() => Result.Conflict("Đã qua giờ chốt hoặc sang ngày mới trong lúc lưu. Tải lại ngày hiệu lực cho phép trước khi sửa.");
}

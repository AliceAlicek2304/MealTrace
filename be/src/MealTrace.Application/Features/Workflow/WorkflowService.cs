using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Models.Persistence;
using MealTrace.Application.Features.Portions;
using MealTrace.Application.Features.Meals;
using MealTrace.Application.Features.Calendar;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Dtos.Workflow;
using System.Data;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Application.Features.Workflow;
public sealed class WorkflowService(IWorkflowRepository repository, MealCalendarService calendar, MealDecisionService decisionService, PortionService portions, ICurrentActor currentActor, TimeProvider clock, IIdentityService users, IUnitOfWork unitOfWork, MealTrace.Application.Features.Notifications.ParentRegistrationNotificationService registrationNotifications)
{
    public async Task<Result<List<AcademicYearResponse>>> ListAcademicYearsAsync()
    {
        return Result.Success(await repository.ListAcademicYearsAsync());
    }

    public async Task<Result<IEnumerable<AcademicYearConfiguration>>> ListYearConfigurationAsync()
    {
        var codes = await repository.ListSchoolYearCodesAsync();
        var years = await repository.GetAcademicYearsByCodeAsync();
        return Result.Success(codes.Select(code => new AcademicYearConfiguration
        {
            Code = code,
            StartDate = years.TryGetValue(code, out var year) ? (DateOnly?)year.StartDate : null,
            EndDate = years.TryGetValue(code, out year) ? (DateOnly?)year.EndDate : null
        }));
    }

    public async Task<Result<NextAcademicYearResponse>> PreviewNextYearAsync(string code)
    {
        var source = await repository.FindAcademicYearAsync(code);
        var nextCode = NextYearCode(code);
        if (source is null)
            return Result.NotFound();
        if (nextCode is null || source.StartDate.Year >= 9999 || source.EndDate.Year >= 9999)
            return Result.Invalid("Không thể tạo năm học tiếp theo từ niên khóa này.");
        return Result.Success(new NextAcademicYearResponse
        {
            Code = nextCode,
            StartDate = source.StartDate.AddYears(1),
            EndDate = source.EndDate.AddYears(1),
            SourceYearCode = code,
            IsConfigured = await repository.AcademicYearExistsAsync(nextCode)
        });
    }

    public async Task<Result<Unit>> ConfigureYearAsync(string code, YearDates input)
    {
        if (NextYearCode(code) is null || input.EndDate < input.StartDate)
            return Result.Invalid("Mã năm học phải dạng 2026-2027 và khoảng ngày hợp lệ.");
        if (input.SourceYearCode is not null && (NextYearCode(input.SourceYearCode) != code || !await repository.SourceAcademicYearExistsAsync(input)))
            return Result.Invalid("Năm học nguồn chưa thiết lập hoặc không phải năm liền trước.");
        var year = await repository.FindTrackedAcademicYearAsync(code);
        if (year is not null)
            return Result.Conflict("Mốc năm học đã thiết lập. Thay đổi lịch năm học cần quy trình đối chiếu dữ liệu riêng.");
        repository.AddAcademicYear(new AcademicYear
        {
            Code = code,
            StartDate = input.StartDate,
            EndDate = input.EndDate
        });
        await unitOfWork.SaveChangesAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<List<ClassSummary>>> ListClassesAsync()
    {

        var date = SchoolTime.Today(clock.GetUtcNow());
        return Result.Success(await repository.ListAccessibleClassesAsync(date, currentActor.IsInRole(RoleNames.Admin), currentActor.IsInRole(RoleNames.Teacher), CurrentUserId()));
    }

    public async Task<Result<ClassCreatedResponse>> CreateClassAsync(CreateClass input)
    {
        var name = input.Name?.Trim();
        var year = input.SchoolYear?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(year) || !System.Text.RegularExpressions.Regex.IsMatch(year, @"^\d{4}-\d{4}$") || int.Parse(year[5..]) != int.Parse(year[..4]) + 1)
            return Result.Invalid("Tên lớp hoặc niên khóa không hợp lệ.");
        if (await repository.ClassNameExistsAsync(name, year))
            return Result.Conflict("Lớp đã tồn tại trong niên khóa.");
        var room = new SchoolClass
        {
            Name = name,

            SchoolYear = year
        };
        repository.AddSchoolClass(room);
        await unitOfWork.SaveChangesAsync();
        return Result.Success(new ClassCreatedResponse
        {
            Id = room.Id,
            Name = room.Name,
            SchoolYear = room.SchoolYear
        });
    }

    public async Task<Result<IEnumerable<ClassStudentSummary>>> ListClassStudentsAsync(Guid classId)
    {
        if (!await CanReadClass(classId))
            return Result.Forbidden();
        var students = await repository.ListClassStudentsAsync(classId, clock.GetUtcNow());
        var ids = students.Select(x => x.Id).ToArray();
        var parents = await repository.ListStudentParentsAsync(ids);
        return Result.Success(students.Select(x => new ClassStudentSummary
        {
            Id = x.Id,
            StudentCode = x.StudentCode,
            FullName = x.FullName,
            ClassId = x.ClassId,
            Parents = parents.Where(p => p.StudentId == x.Id).Select(p => new ParentSummary
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email,
                PhoneNumber = p.PhoneNumber
            }).ToArray()
        }));
    }

    public async Task<Result<StudentCreatedResponse>> CreateStudentAsync(CreateStudent input)
    {
        if (!currentActor.IsInRole(RoleNames.Admin) && (!currentActor.IsInRole(RoleNames.Teacher) || !await CanReadClass(input.ClassId))) return Result.Forbidden();
        var name = input.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || !await repository.ClassExistsAsync(input))
            return Result.Invalid("Tên trẻ hoặc lớp không hợp lệ.");
        if (input.DateOfBirth > SchoolTime.Today(clock.GetUtcNow()) || input.Gender is not (null or "MALE" or "FEMALE" or "OTHER"))
            return Result.Invalid("Ngày sinh hoặc giới tính không hợp lệ.");
        var student = new Student
        {
            FullName = name,
            DateOfBirth = input.DateOfBirth,
            Gender = input.Gender,
            ClassId = input.ClassId
        };
        if (!string.IsNullOrWhiteSpace(input.StudentCode))
        {
            var code = input.StudentCode.Trim().ToUpperInvariant();
            if (code == "HS-DEMO-0001")
                return Result.Invalid("Mã HS-DEMO-0001 dành riêng cho dữ liệu demo.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Z0-9][A-Z0-9-]{2,39}$"))
                return Result.Invalid("Mã trẻ gồm 3–40 ký tự chữ không dấu, số hoặc dấu gạch ngang.");
            student.StudentCode = code;
        }

        if (await repository.StudentCodeExistsAsync(student))
            return Result.Conflict("Mã trẻ đã tồn tại.");
        var now = clock.GetUtcNow();
        var today = SchoolTime.Today(clock.GetUtcNow());
        var start = input.StartDate ?? today;
        if (start < today)
            return Result.Invalid("Ghi danh mới không được lùi ngày về quá khứ.");
        student.Enrollments.Add(new Enrollment
        {
            ClassId = input.ClassId,
            StartDate = start,
            RecordedByUserId = CurrentUserId(),
            RecordedAt = now
        });
        repository.AddStudent(student);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            return Result.Conflict("Mã trẻ đã tồn tại.");
        }

        return Result.Success(new StudentCreatedResponse
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            ClassId = student.ClassId,
            Revision = student.Revision
        });
    }

    public async Task<Result<ParentLinkedResponse>> LinkParentAsync(Guid studentId, LinkParent input)
    {
        if (!currentActor.IsInRole(RoleNames.Admin) && !currentActor.IsInRole(RoleNames.Teacher)) return Result.Forbidden();
        var student = await repository.FindStudentForParentLinkAsync(studentId);
        if (student is null)
            return Result.NotFound();
        if (!await CanReadClass(student.ClassId)) return Result.Forbidden();
        var email = input.Email?.Trim().ToLowerInvariant();
        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(input.PhoneNumber) && phone is null)
            return Result.Invalid("SĐT phụ huynh không hợp lệ.");
        if (phone is null && (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail) || !parsedEmail.Address.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return Result.Invalid("Email phụ huynh không hợp lệ.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var parent = phone is not null ? await repository.FindParentByPhoneAsync(phone) : await users.FindByEmailAsync(email!);
        var created = parent is null;
        string? temporaryPassword = null;
        if (parent is null)
        {
            var name = input.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
                return Result.Invalid("Cần họ tên để tạo tài khoản phụ huynh mới.");
            parent = new IdentityAccount
            {
                Id = Guid.NewGuid(),

                FullName = name,

                UserName = phone ?? email,

                Email = phone is null ? email : null,

                PhoneNumber = phone,

                EmailConfirmed = false,

                IsActive = true,

            };
            temporaryPassword = TemporaryPassword.Generate();
            var result = await users.CreateAsync(parent, temporaryPassword);
            if (!result.Succeeded)
                return Result.Invalid(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
        else if (!parent.IsActive)
            return Result.Conflict("Tài khoản phụ huynh đang bị khóa. Hãy mở khóa trước khi liên kết.");
        if (await repository.ParentLinkExistsAsync(parent, studentId))
            return Result.Conflict("Phụ huynh đã được liên kết với trẻ này.");
        if (!await users.IsInRoleAsync(parent, RoleNames.Parent))
        {
            if (!created && !currentActor.IsInRole(RoleNames.Admin))
                return Result.Conflict("Tài khoản hiện có chưa phải phụ huynh. Cần Admin đối chiếu và cấp quyền.");
            var roleResult = await users.AddToRoleAsync(parent, RoleNames.Parent);
            if (!roleResult.Succeeded)
                return Result.Invalid("Không thể cấp vai trò phụ huynh.");
            if (!created)
            {
                var stampResult = await users.UpdateSecurityStampAsync(parent);
                if (!stampResult.Succeeded)
                    return Result.Invalid("Không thể cập nhật quyền truy cập.");
            }
        }

        repository.AddParentStudent(new ParentStudent
        {
            UserId = parent.Id,
            StudentId = studentId
        });
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new ParentLinkedResponse
        {
            ParentId = parent.Id,
            FullName = parent.FullName,
            Email = parent.Email,
            PhoneNumber = parent.PhoneNumber,
            StudentId = studentId,
            Created = created,
            TemporaryPassword = temporaryPassword,
            Notification = input.SendRegistrationNotification ? await registrationNotifications.SendAsync(parent.PhoneNumber, student.FullName, temporaryPassword) : null
        });
    }

    public async Task<Result<List<ParentChildSummary>>> ListParentStudentsAsync()
    {
        return Result.Success(await repository.ListParentChildrenAsync(clock.GetUtcNow(), CurrentUserId()));
    }

    public async Task<Result<AbsenceCreatedResponse>> ReportAbsenceAsync(ReportAbsence input)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        // Lock a shared parent row so two guardians cannot create overlapping absence intervals.
        await repository.LockStudentAsync(input.StudentId);
        var userId = CurrentUserId();
        var today = SchoolTime.Today(clock.GetUtcNow());
        if (!await repository.ParentCanAccessStudentAsync(userId, input) || !await repository.HasActiveEnrollmentAsync(today, input))
            return Result.Forbidden();
        if (input.FromDate < today || input.ToDate < input.FromDate || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return Result.Invalid("Khoảng không ăn phải từ hôm nay, ngày kết thúc hợp lệ và có lý do.");
        var year = await AbsenceYear(input.StudentId, input.FromDate, input.ToDate);
        if (year is null)
            return Result.Invalid("Khoảng không ăn phải nằm trong năm học đã được nhà trường thiết lập và có ghi danh tại ngày bắt đầu.");
        if (await repository.HasOverlappingAbsenceAsync(input))
            return Result.Conflict("Khoảng ngày vắng bị trùng với báo vắng đang hiệu lực.");
        var acceptedAt = clock.GetUtcNow();
        if (input.FromDate < SchoolTime.Today(clock.GetUtcNow()))
            return Result.Conflict("Đã sang ngày mới trong lúc lưu. Tải lại khoảng không ăn.");
        var absence = new MealAbsence
        {
            StudentId = input.StudentId,

            ReportedByUserId = userId,

            FromDate = input.FromDate,

            ToDate = input.ToDate,

            Reason = input.Reason.Trim(),

            ReportedAt = acceptedAt,

            SchoolYear = year.Code,

        };
        repository.AddMealAbsence(absence);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new AbsenceCreatedResponse
        {
            Id = absence.Id,
            StudentId = absence.StudentId,
            FromDate = absence.FromDate,
            ToDate = absence.ToDate,
            Reason = absence.Reason,
            ReportedAt = absence.ReportedAt
        });
    }

    public async Task<Result<AbsenceReplacedResponse>> ReplaceAbsenceAsync(Guid id, ReportAbsence input)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var userId = CurrentUserId();
        var original = await repository.FindReportedAbsenceAsync(id, userId);
        if (original is null)
            return Result.NotFound();
        await repository.LockStudentAsync(original.StudentId);
        var absence = await repository.GetTrackedAbsenceAsync(id);
        if (!await repository.HasGuardianLinkAsync(userId, absence))
            return Result.Forbidden();
        if (absence.CancelledAt is not null)
            return Result.Conflict("Đăng ký đã hủy hoặc được cập nhật. Hãy tải lại danh sách.");
        var today = SchoolTime.Today(clock.GetUtcNow());
        if (input.StudentId != absence.StudentId || input.ToDate < today || input.ToDate < input.FromDate || (input.FromDate < today && input.FromDate != absence.FromDate) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return Result.Invalid("Giữ ngày bắt đầu cũ hoặc chọn từ hôm nay; ngày kết thúc từ hôm nay và có lý do. Muốn ăn lại ngay hãy hủy đăng ký.");
        var year = await AbsenceYear(input.StudentId, input.FromDate, input.ToDate);
        if (year is null || (absence.SchoolYear is not null && absence.SchoolYear != year.Code))
            return Result.Invalid("Khoảng không ăn phải nằm trong cùng năm học đã được nhà trường thiết lập.");
        if (await repository.HasOtherOverlappingAbsenceAsync(id, absence, input))
            return Result.Conflict("Khoảng ngày trùng với đăng ký đang hiệu lực.");
        var now = clock.GetUtcNow();
        var acceptedToday = SchoolTime.Today(clock.GetUtcNow());
        if (input.ToDate < acceptedToday || (input.FromDate < acceptedToday && input.FromDate != absence.FromDate))
            return Result.Conflict("Đã sang ngày mới trong lúc lưu. Tải lại khoảng không ăn.");
        absence.CancelledAt = now;
        var replacement = new MealAbsence
        {
            StudentId = absence.StudentId,

            ReportedByUserId = userId,

            FromDate = input.FromDate,

            ToDate = input.ToDate,

            Reason = input.Reason.Trim(),

            ReportedAt = now,

            SchoolYear = year.Code
        };
        repository.AddMealAbsence(replacement);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new AbsenceReplacedResponse
        {
            Id = replacement.Id,
            ReplacedId = id
        });
    }

    public async Task<Result<AbsenceListResponse>> SearchAbsencesAsync(Guid? studentId, string? status, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        search = search?.Trim();
        if (search?.Length > 200 || (status is not null && status is not ("ACTIVE" or "EXPIRED" or "UPCOMING" or "CANCELLED")))
            return Result.Invalid("Bộ lọc không hợp lệ; từ khóa tối đa 200 ký tự.");
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var filter = new AbsenceListFilter(CurrentUserId(), studentId, status, search, SchoolTime.Today(clock.GetUtcNow()));
        var total = await repository.CountReportedAbsencesAsync(filter, ct);
        var items = await repository.SearchReportedAbsencesAsync(filter, number, size, ct);
        return Result.Success(new AbsenceListResponse(items, total, number, size, await repository.ListAbsenceStudentOptionsAsync(filter.UserId, ct)));
    }

    public async Task<Result<List<AbsenceSummary>>> ListAbsencesAsync()
    {
        return Result.Success(await repository.ListReportedAbsencesAsync(CurrentUserId(), CurrentUserId()));
    }

    public async Task<Result<Unit>> CancelAbsenceAsync(Guid id)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var original = await repository.FindAbsenceForCancellationAsync(id, CurrentUserId());
        if (original is null)
            return Result.NotFound();
        await repository.LockStudentAsync(original.StudentId);
        var absence = await repository.GetAbsenceForCancellationAsync(id);
        if (!await repository.HasGuardianLinkForCancellationAsync(absence, CurrentUserId()))
            return Result.Forbidden();
        if (absence.CancelledAt is not null)
            return Result.Conflict("Báo vắng đã được hủy.");
        absence.CancelledAt = clock.GetUtcNow();
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<MealDayCreatedResponse>> CreateMealDayAsync(CreateMealDay input)
    {
        var mealType = input.MealType?.Trim();
        var schoolYear = input.SchoolYear?.Trim();
        if (string.IsNullOrWhiteSpace(mealType) || mealType.Length > 60 || string.IsNullOrWhiteSpace(schoolYear) || !await repository.SchoolYearHasClassesAsync(schoolYear))
            return Result.Invalid("Tên phiên ăn hoặc niên khóa không hợp lệ.");
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await calendar.LockYear(schoolYear);
        if (!await calendar.Allows(schoolYear, input.Date, mealType))
            return Result.Invalid("Ngày/phiên không có trong lịch bữa ăn. Thiết lập lịch tuần hoặc ngày đặc biệt trước.");
        if (MealCalendarService.Cutoff(input.Date) <= clock.GetUtcNow())
            return Result.Conflict("Đã qua giờ chốt; không tạo phiên mới cho ngày này.");
        if (await repository.MealSessionExistsAsync(input, mealType))
            return Result.Conflict("Phiên ăn này đã tồn tại.");
        var day = new MealDay
        {
            Date = input.Date,

            MealType = mealType,

            SchoolYear = schoolYear,

            CutoffAt = SchoolTime.Cutoff(input.Date),

        };
        repository.AddMealDay(day);
        if (day.CutoffAt <= clock.GetUtcNow())
            return Result.Conflict("Đã qua giờ chốt; không tạo phiên mới.");
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new MealDayCreatedResponse
        {
            Id = day.Id,
            Date = day.Date,
            MealType = day.MealType,
            SchoolYear = day.SchoolYear,
            CutoffAt = day.CutoffAt
        });
    }

    public async Task<Result<WorkflowMealDayListResponse>> ListWorkflowDaysAsync(DateOnly? date, int? page)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var total = await repository.CountMealDaysAsync(date);
        var items = await repository.ListMealDaysAsync(date, number);
        return Result.Success(new WorkflowMealDayListResponse
        {
            Items = items,
            Total = total,
            Page = number,
            PageSize = 25
        });
    }

    public async Task<Result<MealPortionsResponse>> GetPortionsAsync(Guid id, CancellationToken ct)
    {
        var day = await repository.FindMealDayAsync(id, ct);
        if (day is null)
            return Result.NotFound();
        Guid[]? allowed = null;
        if (currentActor.IsInRole(RoleNames.Teacher) && !currentActor.IsInRole(RoleNames.Admin) && !currentActor.IsInRole(RoleNames.KitchenStaff))
        {
            allowed = await repository.ListAssignedClassIdsAsync(ct, CurrentUserId());
        }

        var preview = await portions.ReadAsync(day, clock.GetUtcNow(), allowed, ct);
        return Result.Success(new MealPortionsResponse
        {
            Id = day.Id,
            Date = day.Date,
            MealType = day.MealType,
            CutoffAt = day.CutoffAt,
            IsCancelled = day.IsCancelled,
            CancellationReason = day.CancellationReason,
            IsSettled = day.SettledAt != null,
            Classes = preview
        });
    }

    public async Task<Result<PortionsSettledResponse>> SettlePortionsAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var day = await repository.LockMealDayAsync(id, ct);
        if (day is null)
            return Result.NotFound();
        if (day.IsCancelled)
            return Result.Conflict("Phiên đã hủy theo lịch bữa ăn; không thể chốt.");
        if (clock.GetUtcNow() < day.CutoffAt)
            return Result.Conflict("Chưa đến giờ chốt suất.");
        if (day.SettledAt is not null || await repository.HasSettlementAsync(id, ct))
            return Result.Conflict("Phiên ăn đã có bản chốt.");
        var decisions = await decisionService.ReadAsync(day, clock.GetUtcNow(), ct: ct);
        if (decisions.Count == 0)
            return Result.Invalid("Niên khóa chưa có lớp với trẻ đang hoạt động.");
        var actor = CurrentUserId().ToString();
        var settledAt = clock.GetUtcNow();
        var snapshots = PortionService.CreateSnapshots(day, decisions, actor, settledAt);
        repository.AddPortionSettlements(snapshots);
        day.SettledAt = settledAt;
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException)
        {
            return Result.Conflict("Phiên ăn vừa được chốt bởi yêu cầu khác. Hãy tải lại danh sách.");
        }

        return Result.Success(new PortionsSettledResponse
        {
            Id = day.Id,
            Classes = snapshots.Count,
            Total = snapshots.Sum(x => x.Count)
        });
    }

    private static string? NextYearCode(string code)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[0-9]{4}-[0-9]{4}$"))
            return null;
        var start = int.Parse(code[..4]);
        var end = int.Parse(code[5..]);
        return start >= 1 && end == start + 1 && end < 9999 ? $"{end:D4}-{end + 1:D4}" : null;
    }

    private async Task<AcademicYear?> AbsenceYear(Guid studentId, DateOnly from, DateOnly to)
    {
        var code = await repository.FindEnrollmentYearCodeAsync(from, studentId);
        return code is null ? null : await repository.FindAcademicYearContainingPeriodAsync(code, from, to);
    }

    private Guid CurrentUserId() => currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
    private async Task<bool> CanReadClass(Guid classId) => currentActor.IsInRole(RoleNames.Admin) || await repository.HasTeacherAssignmentAsync(classId, CurrentUserId());
}

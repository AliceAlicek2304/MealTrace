using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Dtos.Portions;
using MealTrace.Application.Dtos.Meals;
using MealTrace.Application.Dtos.Workflow;
using System.Data;
using System.Security.Claims;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using MealTrace.Domain.Time;

namespace MealTrace.Application.Features;
public static class WorkflowUseCases
{
    public static async Task<UseCaseResult> ListAcademicYearsAsync(IMealTraceData db)
    {
        return UseCaseResult.Ok(await db.AcademicYears.AsNoTracking(db.Queries).OrderBy(x => x.Code).Select(x => new AcademicYearResponse(x.Code, x.StartDate, x.EndDate)).ToListAsync(db.Queries));
    }

    public static async Task<UseCaseResult> ListYearConfigurationAsync(IMealTraceData db)
    {
        var codes = await db.Classes.Select(x => x.SchoolYear).Union(db.AcademicYears.Select(x => x.Code)).OrderBy(x => x).ToListAsync(db.Queries);
        var years = await db.AcademicYears.ToDictionaryAsync(db.Queries, x => x.Code);
        return UseCaseResult.Ok(codes.Select(code => new AcademicYearConfiguration
        {
            Code = code,
            StartDate = years.TryGetValue(code, out var year) ? (DateOnly?)year.StartDate : null,
            EndDate = years.TryGetValue(code, out year) ? (DateOnly?)year.EndDate : null
        }));
    }

    public static async Task<UseCaseResult> PreviewNextYearAsync(string code, IMealTraceData db)
    {
        var source = await db.AcademicYears.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Code == code);
        var nextCode = NextYearCode(code);
        if (source is null)
            return UseCaseResult.NotFound();
        if (nextCode is null || source.StartDate.Year >= 9999 || source.EndDate.Year >= 9999)
            return UseCaseResult.BadRequest(new MessageResponse("Không thể tạo năm học tiếp theo từ niên khóa này."));
        return UseCaseResult.Ok(new NextAcademicYearResponse
        {
            Code = nextCode,
            StartDate = source.StartDate.AddYears(1),
            EndDate = source.EndDate.AddYears(1),
            SourceYearCode = code,
            IsConfigured = await db.AcademicYears.AnyAsync(db.Queries, x => x.Code == nextCode)
        });
    }

    public static async Task<UseCaseResult> ConfigureYearAsync(string code, YearDates input, IMealTraceData db)
    {
        if (NextYearCode(code) is null || input.EndDate < input.StartDate)
            return UseCaseResult.BadRequest(new MessageResponse("Mã năm học phải dạng 2026-2027 và khoảng ngày hợp lệ."));
        if (input.SourceYearCode is not null && (NextYearCode(input.SourceYearCode) != code || !await db.AcademicYears.AnyAsync(db.Queries, x => x.Code == input.SourceYearCode)))
            return UseCaseResult.BadRequest(new MessageResponse("Năm học nguồn chưa thiết lập hoặc không phải năm liền trước."));
        var year = await db.AcademicYears.FindAsync(code);
        if (year is not null)
            return UseCaseResult.Conflict(new MessageResponse("Mốc năm học đã thiết lập. Thay đổi lịch năm học cần quy trình đối chiếu dữ liệu riêng."));
        db.AcademicYears.Add(new AcademicYear
        {
            Code = code,
            StartDate = input.StartDate,
            EndDate = input.EndDate
        });
        await db.SaveChangesAsync();
        return UseCaseResult.NoContent();
    }

    public static async Task<UseCaseResult> ListClassesAsync(ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        var query = db.Classes.AsNoTracking(db.Queries).AsQueryable();
        if (!principal.IsInRole(RoleNames.Admin) && principal.IsInRole(RoleNames.Teacher))
        {
            var userId = CurrentUserId(principal);
            query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == userId && a.ClassId == x.Id));
        }

        var date = SchoolTime.Today(clock.GetUtcNow());
        return UseCaseResult.Ok(await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).Select(x => new ClassSummary
        {
            Id = x.Id,
            Name = x.Name,
            SchoolYear = x.SchoolYear,
            StudentCount = db.Enrollments.Count(s => s.ClassId == x.Id && s.StartDate <= date && (s.EndDate == null || s.EndDate > date))
        }).ToListAsync(db.Queries));
    }

    public static async Task<UseCaseResult> CreateClassAsync(CreateClass input, IMealTraceData db)
    {
        var name = input.Name?.Trim();
        var year = input.SchoolYear?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(year) || !System.Text.RegularExpressions.Regex.IsMatch(year, @"^\d{4}-\d{4}$") || int.Parse(year[5..]) != int.Parse(year[..4]) + 1)
            return UseCaseResult.BadRequest(new MessageResponse("Tên lớp hoặc niên khóa không hợp lệ."));
        if (await db.Classes.AnyAsync(db.Queries, x => x.Name == name && x.SchoolYear == year))
            return UseCaseResult.Conflict(new MessageResponse("Lớp đã tồn tại trong niên khóa."));
        var room = new SchoolClass
        {

            Name = name,

            SchoolYear = year

        };
        db.Classes.Add(room);
        await db.SaveChangesAsync();
        return UseCaseResult.Created($"/api/classes/{room.Id}", new ClassCreatedResponse
        {
            Id = room.Id,
            Name = room.Name,
            SchoolYear = room.SchoolYear
        });
    }

    public static async Task<UseCaseResult> ListClassStudentsAsync(Guid classId, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        if (!await CanReadClass(db, principal, classId))
            return UseCaseResult.Forbid();
        var students = await StudentAdministrationUseCases.OnDate(db, SchoolTime.Today(clock.GetUtcNow())).Where(x => x.ClassId == classId).OrderBy(x => x.Student.FullName).Select(x => new ClassStudentRow
        {
            Id = x.StudentId,
            StudentCode = x.Student.StudentCode,
            FullName = x.Student.FullName,
            ClassId = x.ClassId
        }).ToListAsync(db.Queries);
        var ids = students.Select(x => x.Id).ToArray();
        var parents = await db.ParentStudents.AsNoTracking(db.Queries).Where(x => ids.Contains(x.StudentId)).Join(db.Users, link => link.UserId, user => user.Id, (link, user) => new ParentStudentRow
        {
            StudentId = link.StudentId,
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        }).ToListAsync(db.Queries);
        return UseCaseResult.Ok(students.Select(x => new ClassStudentSummary
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

    public static async Task<UseCaseResult> CreateStudentAsync(CreateStudent input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        var name = input.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || !await db.Classes.AnyAsync(db.Queries, x => x.Id == input.ClassId))
            return UseCaseResult.BadRequest(new MessageResponse("Tên trẻ hoặc lớp không hợp lệ."));
        var student = new Student
        {

            FullName = name,

            ClassId = input.ClassId

        };
        if (!string.IsNullOrWhiteSpace(input.StudentCode))
        {
            var code = input.StudentCode.Trim().ToUpperInvariant();
            if (code == "HS-DEMO-0001")
                return UseCaseResult.BadRequest(new MessageResponse("Mã HS-DEMO-0001 dành riêng cho dữ liệu demo."));
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Z0-9][A-Z0-9-]{2,39}$"))
                return UseCaseResult.BadRequest(new MessageResponse("Mã trẻ gồm 3–40 ký tự chữ không dấu, số hoặc dấu gạch ngang."));
            student.StudentCode = code;
        }

        if (await db.Students.AnyAsync(db.Queries, x => x.StudentCode == student.StudentCode))
            return UseCaseResult.Conflict(new MessageResponse("Mã trẻ đã tồn tại."));
        var now = clock.GetUtcNow();
        var today = SchoolTime.Today(now);
        var start = input.StartDate ?? today;
        if (start < today)
            return UseCaseResult.BadRequest(new MessageResponse("Ghi danh mới không được lùi ngày về quá khứ."));
        student.Enrollments.Add(new Enrollment
        {
            ClassId = input.ClassId,
            StartDate = start,
            RecordedByUserId = CurrentUserId(principal),
            RecordedAt = now
        });
        db.Students.Add(student);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (db.IsUniqueViolation(ex))
        {
            return UseCaseResult.Conflict(new MessageResponse("Mã trẻ đã tồn tại."));
        }

        return UseCaseResult.Created($"/api/students/{student.Id}", new StudentCreatedResponse
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            ClassId = student.ClassId,
            Revision = student.Revision
        });
    }

    public static async Task<UseCaseResult> LinkParentAsync(Guid studentId, LinkParent input, IMealTraceData db, IIdentityService users)
    {
        if (!await db.Students.AnyAsync(db.Queries, x => x.Id == studentId))
            return UseCaseResult.NotFound();
        var email = input.Email?.Trim().ToLowerInvariant();
        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(input.PhoneNumber) && phone is null)
            return UseCaseResult.BadRequest(new MessageResponse("SĐT phụ huynh không hợp lệ."));
        if (phone is null && (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail) || !parsedEmail.Address.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return UseCaseResult.BadRequest(new MessageResponse("Email phụ huynh không hợp lệ."));
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable);
        var parent = phone is not null ? await db.Users.SingleOrDefaultAsync(db.Queries, x => x.PhoneNumber == phone) : await users.FindByEmailAsync(email!);
        var created = parent is null;
        string? temporaryPassword = null;
        if (parent is null)
        {
            var name = input.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
                return UseCaseResult.BadRequest(new MessageResponse("Cần họ tên để tạo tài khoản phụ huynh mới."));
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
                return UseCaseResult.BadRequest(new MessageResponse(string.Join("; ", result.Errors.Select(x => x.Description))));
        }
        else if (!parent.IsActive)
            return UseCaseResult.Conflict(new MessageResponse("Tài khoản phụ huynh đang bị khóa. Hãy mở khóa trước khi liên kết."));
        if (await db.ParentStudents.AnyAsync(db.Queries, x => x.UserId == parent.Id && x.StudentId == studentId))
            return UseCaseResult.Conflict(new MessageResponse("Phụ huynh đã được liên kết với trẻ này."));
        if (!await users.IsInRoleAsync(parent, RoleNames.Parent))
        {
            var roleResult = await users.AddToRoleAsync(parent, RoleNames.Parent);
            if (!roleResult.Succeeded)
                return UseCaseResult.BadRequest(new MessageResponse("Không thể cấp vai trò phụ huynh."));
            if (!created)
            {
                var stampResult = await users.UpdateSecurityStampAsync(parent);
                if (!stampResult.Succeeded)
                    return UseCaseResult.BadRequest(new MessageResponse("Không thể cập nhật quyền truy cập."));
            }
        }

        db.ParentStudents.Add(new ParentStudent
        {
            UserId = parent.Id,
            StudentId = studentId
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Ok(new ParentLinkedResponse
        {
            ParentId = parent.Id,
            FullName = parent.FullName,
            Email = parent.Email,
            PhoneNumber = parent.PhoneNumber,
            StudentId = studentId,
            Created = created,
            TemporaryPassword = temporaryPassword
        });
    }

    public static async Task<UseCaseResult> ListParentStudentsAsync(ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        return UseCaseResult.Ok(await StudentAdministrationUseCases.OnDate(db, SchoolTime.Today(clock.GetUtcNow())).Where(x => db.ParentStudents.Any(p => p.UserId == CurrentUserId(principal) && p.StudentId == x.StudentId)).OrderBy(x => x.Student.FullName).Select(x => new ParentChildSummary
        {
            StudentId = x.StudentId,
            StudentCode = x.Student.StudentCode,
            FullName = x.Student.FullName,
            ClassId = x.ClassId,
            ClassName = x.Class.Name,
            SchoolYear = x.Class.SchoolYear,
            YearStartDate = db.AcademicYears.Where(y => y.Code == x.Class.SchoolYear).Select(y => (DateOnly?)y.StartDate).FirstOrDefault(),
            YearEndDate = db.AcademicYears.Where(y => y.Code == x.Class.SchoolYear).Select(y => (DateOnly?)y.EndDate).FirstOrDefault()
        }).ToListAsync(db.Queries));
    }

    public static async Task<UseCaseResult> ReportAbsenceAsync(ReportAbsence input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        await using var transaction = await db.BeginTransactionAsync();
        // Lock a shared parent row so two guardians cannot create overlapping absence intervals.
        await db.LockStudentAsync(input.StudentId);
        var userId = CurrentUserId(principal);
        var today = SchoolTime.Today(clock.GetUtcNow());
        if (!await db.ParentStudents.AnyAsync(db.Queries, x => x.UserId == userId && x.StudentId == input.StudentId) || !await StudentAdministrationUseCases.OnDate(db, today).AnyAsync(db.Queries, x => x.StudentId == input.StudentId))
            return UseCaseResult.Forbid();
        if (input.FromDate < today || input.ToDate < input.FromDate || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return UseCaseResult.BadRequest(new MessageResponse("Khoảng không ăn phải từ hôm nay, ngày kết thúc hợp lệ và có lý do."));
        var year = await AbsenceYear(db, input.StudentId, input.FromDate, input.ToDate);
        if (year is null)
            return UseCaseResult.BadRequest(new MessageResponse("Khoảng không ăn phải nằm trong năm học đã được nhà trường thiết lập và có ghi danh tại ngày bắt đầu."));
        if (await db.MealAbsences.AnyAsync(db.Queries, x => x.StudentId == input.StudentId && x.CancelledAt == null && x.FromDate <= input.ToDate && x.ToDate >= input.FromDate))
            return UseCaseResult.Conflict(new MessageResponse("Khoảng ngày vắng bị trùng với báo vắng đang hiệu lực."));
        var acceptedAt = clock.GetUtcNow();
        if (input.FromDate < SchoolTime.Today(acceptedAt))
            return UseCaseResult.Conflict(new MessageResponse("Đã sang ngày mới trong lúc lưu. Tải lại khoảng không ăn."));
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
        db.MealAbsences.Add(absence);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Created($"/api/parent/absences/{absence.Id}", new AbsenceCreatedResponse
        {
            Id = absence.Id,
            StudentId = absence.StudentId,
            FromDate = absence.FromDate,
            ToDate = absence.ToDate,
            Reason = absence.Reason,
            ReportedAt = absence.ReportedAt
        });
    }

    public static async Task<UseCaseResult> ReplaceAbsenceAsync(Guid id, ReportAbsence input, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        await using var transaction = await db.BeginTransactionAsync();
        var userId = CurrentUserId(principal);
        var original = await db.MealAbsences.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == id && x.ReportedByUserId == userId);
        if (original is null)
            return UseCaseResult.NotFound();
        await db.LockStudentAsync(original.StudentId);
        var absence = await db.MealAbsences.SingleAsync(db.Queries, x => x.Id == id);
        if (!await db.ParentStudents.AnyAsync(db.Queries, x => x.UserId == userId && x.StudentId == absence.StudentId))
            return UseCaseResult.Forbid();
        if (absence.CancelledAt is not null)
            return UseCaseResult.Conflict(new MessageResponse("Đăng ký đã hủy hoặc được cập nhật. Hãy tải lại danh sách."));
        var today = SchoolTime.Today(clock.GetUtcNow());
        if (input.StudentId != absence.StudentId || input.ToDate < today || input.ToDate < input.FromDate || (input.FromDate < today && input.FromDate != absence.FromDate) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
            return UseCaseResult.BadRequest(new MessageResponse("Giữ ngày bắt đầu cũ hoặc chọn từ hôm nay; ngày kết thúc từ hôm nay và có lý do. Muốn ăn lại ngay hãy hủy đăng ký."));
        var year = await AbsenceYear(db, input.StudentId, input.FromDate, input.ToDate);
        if (year is null || (absence.SchoolYear is not null && absence.SchoolYear != year.Code))
            return UseCaseResult.BadRequest(new MessageResponse("Khoảng không ăn phải nằm trong cùng năm học đã được nhà trường thiết lập."));
        if (await db.MealAbsences.AnyAsync(db.Queries, x => x.Id != id && x.StudentId == absence.StudentId && x.CancelledAt == null && x.FromDate <= input.ToDate && x.ToDate >= input.FromDate))
            return UseCaseResult.Conflict(new MessageResponse("Khoảng ngày trùng với đăng ký đang hiệu lực."));
        var now = clock.GetUtcNow();
        var acceptedToday = SchoolTime.Today(now);
        if (input.ToDate < acceptedToday || (input.FromDate < acceptedToday && input.FromDate != absence.FromDate))
            return UseCaseResult.Conflict(new MessageResponse("Đã sang ngày mới trong lúc lưu. Tải lại khoảng không ăn."));
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
        db.MealAbsences.Add(replacement);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Ok(new AbsenceReplacedResponse
        {
            Id = replacement.Id,
            ReplacedId = id
        });
    }

    public static async Task<UseCaseResult> ListAbsencesAsync(ClaimsPrincipal principal, IMealTraceData db)
    {
        return UseCaseResult.Ok(await db.MealAbsences.AsNoTracking(db.Queries).Where(x => x.ReportedByUserId == CurrentUserId(principal) && db.ParentStudents.Any(p => p.UserId == CurrentUserId(principal) && p.StudentId == x.StudentId)).OrderByDescending(x => x.ReportedAt).Take(100).Select(x => new AbsenceSummary
        {
            Id = x.Id,
            StudentId = x.StudentId,
            StudentName = x.Student.FullName,
            FromDate = x.FromDate,
            ToDate = x.ToDate,
            Reason = x.Reason,
            ReportedAt = x.ReportedAt,
            CancelledAt = x.CancelledAt,
            SchoolYear = x.SchoolYear ?? db.Enrollments.Where(e => e.StudentId == x.StudentId && e.StartDate <= x.FromDate && (e.EndDate == null || e.EndDate > x.FromDate)).Select(e => e.Class.SchoolYear).FirstOrDefault()
        }).ToListAsync(db.Queries));
    }

    public static async Task<UseCaseResult> CancelAbsenceAsync(Guid id, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock)
    {
        await using var transaction = await db.BeginTransactionAsync();
        var original = await db.MealAbsences.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == id && x.ReportedByUserId == CurrentUserId(principal));
        if (original is null)
            return UseCaseResult.NotFound();
        await db.LockStudentAsync(original.StudentId);
        var absence = await db.MealAbsences.SingleAsync(db.Queries, x => x.Id == id);
        if (!await db.ParentStudents.AnyAsync(db.Queries, x => x.UserId == CurrentUserId(principal) && x.StudentId == absence.StudentId))
            return UseCaseResult.Forbid();
        if (absence.CancelledAt is not null)
            return UseCaseResult.Conflict(new MessageResponse("Báo vắng đã được hủy."));
        absence.CancelledAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.NoContent();
    }

    public static async Task<UseCaseResult> CreateMealDayAsync(CreateMealDay input, IMealTraceData db, TimeProvider clock)
    {
        var mealType = input.MealType?.Trim();
        var schoolYear = input.SchoolYear?.Trim();
        if (string.IsNullOrWhiteSpace(mealType) || mealType.Length > 60 || string.IsNullOrWhiteSpace(schoolYear) || !await db.Classes.AnyAsync(db.Queries, x => x.SchoolYear == schoolYear))
            return UseCaseResult.BadRequest(new MessageResponse("Tên phiên ăn hoặc niên khóa không hợp lệ."));
        await using var transaction = await db.BeginTransactionAsync();
        await MealCalendarUseCases.LockYear(db, schoolYear);
        if (!await MealCalendarUseCases.Allows(db, schoolYear, input.Date, mealType))
            return UseCaseResult.BadRequest(new MessageResponse("Ngày/phiên không có trong lịch bữa ăn. Thiết lập lịch tuần hoặc ngày đặc biệt trước."));
        if (MealCalendarUseCases.Cutoff(input.Date) <= clock.GetUtcNow())
            return UseCaseResult.Conflict(new MessageResponse("Đã qua giờ chốt; không tạo phiên mới cho ngày này."));
        if (await db.MealDays.AnyAsync(db.Queries, x => x.Date == input.Date && x.MealType == mealType))
            return UseCaseResult.Conflict(new MessageResponse("Phiên ăn này đã tồn tại."));
        var day = new MealDay
        {

            Date = input.Date,

            MealType = mealType,

            SchoolYear = schoolYear,

            CutoffAt = SchoolTime.Cutoff(input.Date),


        };
        db.MealDays.Add(day);
        if (day.CutoffAt <= clock.GetUtcNow())
            return UseCaseResult.Conflict(new MessageResponse("Đã qua giờ chốt; không tạo phiên mới."));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Created($"/api/meal-days/{day.Id}", new MealDayCreatedResponse
        {
            Id = day.Id,
            Date = day.Date,
            MealType = day.MealType,
            SchoolYear = day.SchoolYear,
            CutoffAt = day.CutoffAt
        });
    }

    public static async Task<UseCaseResult> ListWorkflowDaysAsync(DateOnly? date, int? page, IMealTraceData db)
    {
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var query = db.MealDays.AsNoTracking(db.Queries).Where(x => date == null || x.Date == date);
        var total = await query.CountAsync(db.Queries);
        var items = await query.OrderByDescending(x => x.Date).ThenBy(x => x.MealType).ThenBy(x => x.Id).Skip((number - 1) * 25).Take(25).Select(x => new WorkflowMealDaySummary
        {
            Id = x.Id,
            Date = x.Date,
            MealType = x.MealType,
            SchoolYear = x.SchoolYear,
            CutoffAt = x.CutoffAt,
            IsCancelled = x.IsCancelled,
            CancellationReason = x.CancellationReason,
            IsSettled = x.SettledAt != null
        }).ToListAsync(db.Queries);
        return UseCaseResult.Ok(new WorkflowMealDayListResponse
        {
            Items = items,
            Total = total,
            Page = number,
            PageSize = 25
        });
    }

    public static async Task<UseCaseResult> GetPortionsAsync(Guid id, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock, CancellationToken ct)
    {
        var day = await db.MealDays.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == id, ct);
        if (day is null)
            return UseCaseResult.NotFound();
        Guid[]? allowed = null;
        if (principal.IsInRole(RoleNames.Teacher) && !principal.IsInRole(RoleNames.Admin) && !principal.IsInRole(RoleNames.KitchenStaff))
        {
            allowed = await db.TeacherAssignments.AsNoTracking(db.Queries).Where(x => x.UserId == CurrentUserId(principal)).Select(x => x.ClassId).ToArrayAsync(db.Queries, ct);
        }

        var preview = await PortionService.ReadAsync(db, day, clock.GetUtcNow(), allowed, ct);
        return UseCaseResult.Ok(new MealPortionsResponse
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

    public static async Task<UseCaseResult> SettlePortionsAsync(Guid id, ClaimsPrincipal principal, IMealTraceData db, TimeProvider clock, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var day = await db.LockMealDayAsync(id, ct);
        if (day is null)
            return UseCaseResult.NotFound();
        if (day.IsCancelled)
            return UseCaseResult.Conflict(new MessageResponse("Phiên đã hủy theo lịch bữa ăn; không thể chốt."));
        if (clock.GetUtcNow() < day.CutoffAt)
            return UseCaseResult.Conflict(new MessageResponse("Chưa đến giờ chốt suất."));
        if (day.SettledAt is not null || await db.PortionSettlements.AnyAsync(db.Queries, x => x.MealDayId == id, ct))
            return UseCaseResult.Conflict(new MessageResponse("Phiên ăn đã có bản chốt."));
        var decisions = await MealDecisionService.ReadAsync(db, day, clock.GetUtcNow(), ct: ct);
        if (decisions.Count == 0)
            return UseCaseResult.BadRequest(new MessageResponse("Niên khóa chưa có lớp với trẻ đang hoạt động."));
        var actor = CurrentUserId(principal).ToString();
        var settledAt = clock.GetUtcNow();
        var snapshots = PortionService.CreateSnapshots(day, decisions, actor, settledAt);
        db.PortionSettlements.AddRange(snapshots);
        day.SettledAt = settledAt;
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (db.IsConflict(ex))
        {
            return UseCaseResult.Conflict(new MessageResponse("Phiên ăn vừa được chốt bởi yêu cầu khác. Hãy tải lại danh sách."));
        }

        return UseCaseResult.Ok(new PortionsSettledResponse
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

    private static async Task<AcademicYear?> AbsenceYear(IMealTraceData db, Guid studentId, DateOnly from, DateOnly to)
    {
        var code = await StudentAdministrationUseCases.OnDate(db, from).Where(x => x.StudentId == studentId).Select(x => x.Class.SchoolYear).FirstOrDefaultAsync(db.Queries);
        return code is null ? null : await db.AcademicYears.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Code == code && x.StartDate <= from && x.EndDate >= to);
    }

    private static Guid CurrentUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirst("sub")?.Value!);
    private static async Task<bool> CanReadClass(IMealTraceData db, ClaimsPrincipal principal, Guid classId) => principal.IsInRole(RoleNames.Admin) || await db.TeacherAssignments.AnyAsync(db.Queries, x => x.UserId == CurrentUserId(principal) && x.ClassId == classId);
}

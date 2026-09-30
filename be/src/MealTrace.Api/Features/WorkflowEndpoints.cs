using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MealTrace.Api.Features;

public static class WorkflowEndpoints
{
    private static readonly TimeSpan SchoolOffset = TimeSpan.FromHours(7);
    private static readonly TimeOnly DefaultCutoff = new(7, 30);

    private sealed record CreateClass(string Name, string SchoolYear);
    private sealed record CreateStudent(string FullName, Guid ClassId);
    private sealed record LinkParent(string? Email, string? FullName, string? PhoneNumber = null);
    private sealed record CreateMealDay(DateOnly Date, string MealType, string SchoolYear);
    private sealed record ReportAbsence(Guid StudentId, DateOnly FromDate, DateOnly ToDate, string Reason);
    private sealed record RecordException(Guid StudentId, bool WillEat, string Reason);

    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().WithTags("Meal workflow");

        api.MapGet("/classes", async (ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var query = db.Classes.AsNoTracking().AsQueryable();
            if (!principal.IsInRole(RoleNames.Admin) && principal.IsInRole(RoleNames.Teacher))
            {
                var userId = CurrentUserId(principal);
                query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == userId && a.ClassId == x.Id));
            }
            return await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.SchoolYear, StudentCount = x.Students.Count(s => s.IsActive) }).ToListAsync();
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/classes", async (CreateClass input, MealTraceDbContext db) =>
        {
            var name = input.Name?.Trim();
            var year = input.SchoolYear?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(year) ||
                !System.Text.RegularExpressions.Regex.IsMatch(year, @"^\d{4}-\d{4}$"))
                return Results.BadRequest(new { message = "Tên lớp hoặc niên khóa không hợp lệ." });
            if (await db.Classes.AnyAsync(x => x.Name == name && x.SchoolYear == year))
                return Results.Conflict(new { message = "Lớp đã tồn tại trong niên khóa." });
            var room = new SchoolClass { Name = name, SchoolYear = year };
            db.Classes.Add(room);
            await db.SaveChangesAsync();
            return Results.Created($"/api/classes/{room.Id}", new { room.Id, room.Name, room.SchoolYear });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/classes/{classId:guid}/students", async (Guid classId, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            if (!await CanReadClass(db, principal, classId)) return Results.Forbid();
            var students = await db.Students.AsNoTracking().Where(x => x.ClassId == classId && x.IsActive)
                .OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName, x.ClassId }).ToListAsync();
            var ids = students.Select(x => x.Id).ToArray();
            var parents = await db.ParentStudents.AsNoTracking().Where(x => ids.Contains(x.StudentId))
                .Select(x => new { x.StudentId, x.User.Id, x.User.FullName, x.User.Email, x.User.PhoneNumber }).ToListAsync();
            return Results.Ok(students.Select(x => new
            {
                x.Id, x.FullName, x.ClassId,
                Parents = parents.Where(p => p.StudentId == x.Id).Select(p => new { p.Id, p.FullName, p.Email, p.PhoneNumber }).ToArray(),
            }));
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapPost("/students", async (CreateStudent input, MealTraceDbContext db) =>
        {
            var name = input.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || !await db.Classes.AnyAsync(x => x.Id == input.ClassId))
                return Results.BadRequest(new { message = "Tên trẻ hoặc lớp không hợp lệ." });
            var student = new Student { FullName = name, ClassId = input.ClassId };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            return Results.Created($"/api/students/{student.Id}", new { student.Id, student.FullName, student.ClassId });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapPost("/admin/students/{studentId:guid}/parents", async (Guid studentId, LinkParent input,
            MealTraceDbContext db, UserManager<ApplicationUser> users) =>
        {
            if (!await db.Students.AnyAsync(x => x.Id == studentId && x.IsActive)) return Results.NotFound();
            var email = input.Email?.Trim().ToLowerInvariant();
            var phone = PhoneNumbers.Normalize(input.PhoneNumber);
            if (!string.IsNullOrWhiteSpace(input.PhoneNumber) && phone is null)
                return Results.BadRequest(new { message = "SĐT phụ huynh không hợp lệ." });
            if (phone is null && (string.IsNullOrWhiteSpace(email) || email.Length > 254 ||
                !System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail) ||
                !parsedEmail.Address.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return Results.BadRequest(new { message = "Email phụ huynh không hợp lệ." });
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var parent = phone is not null ? await db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phone) : await users.FindByEmailAsync(email!);
            var created = parent is null;
            string? temporaryPassword = null;
            if (parent is null)
            {
                var name = input.FullName?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
                    return Results.BadRequest(new { message = "Cần họ tên để tạo tài khoản phụ huynh mới." });
                parent = new ApplicationUser
                {
                    Id = Guid.NewGuid(), FullName = name, UserName = phone ?? email, Email = phone is null ? email : null, PhoneNumber = phone,
                    EmailConfirmed = false, IsActive = true,
                };
                temporaryPassword = TemporaryPassword.Generate();
                var result = await users.CreateAsync(parent, temporaryPassword);
                if (!result.Succeeded)
                    return Results.BadRequest(new { message = string.Join("; ", result.Errors.Select(x => x.Description)) });
            }
            else if (!parent.IsActive)
                return Results.Conflict(new { message = "Tài khoản phụ huynh đang bị khóa. Hãy mở khóa trước khi liên kết." });

            if (await db.ParentStudents.AnyAsync(x => x.UserId == parent.Id && x.StudentId == studentId))
                return Results.Conflict(new { message = "Phụ huynh đã được liên kết với trẻ này." });
            if (!await users.IsInRoleAsync(parent, RoleNames.Parent))
            {
                var roleResult = await users.AddToRoleAsync(parent, RoleNames.Parent);
                if (!roleResult.Succeeded)
                    return Results.BadRequest(new { message = "Không thể cấp vai trò phụ huynh." });
                if (!created)
                {
                    var stampResult = await users.UpdateSecurityStampAsync(parent);
                    if (!stampResult.Succeeded) return Results.BadRequest(new { message = "Không thể cập nhật quyền truy cập." });
                }
            }
            db.ParentStudents.Add(new ParentStudent { UserId = parent.Id, StudentId = studentId });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.Ok(new { parentId = parent.Id, parent.FullName, parent.Email, parent.PhoneNumber, studentId, created, temporaryPassword });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/parent/students", async (ClaimsPrincipal principal, MealTraceDbContext db) =>
            await db.ParentStudents.AsNoTracking().Where(x => x.UserId == CurrentUserId(principal) && x.Student.IsActive)
                .OrderBy(x => x.Student.FullName)
                .Select(x => new { x.StudentId, x.Student.FullName, x.Student.ClassId, ClassName = x.Student.Class.Name })
                .ToListAsync()).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences", async (ReportAbsence input, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var userId = CurrentUserId(principal);
            if (!await db.ParentStudents.AnyAsync(x => x.UserId == userId && x.StudentId == input.StudentId && x.Student.IsActive))
                return Results.Forbid();
            var today = SchoolToday();
            if (input.FromDate < today || input.ToDate < input.FromDate || input.ToDate.DayNumber - input.FromDate.DayNumber > 89 ||
                string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                return Results.BadRequest(new { message = "Khoảng ngày vắng phải từ hôm nay, không quá 90 ngày và có lý do." });
            if (await db.MealAbsences.AnyAsync(x => x.StudentId == input.StudentId && x.CancelledAt == null &&
                x.FromDate <= input.ToDate && x.ToDate >= input.FromDate))
                return Results.Conflict(new { message = "Khoảng ngày vắng bị trùng với báo vắng đang hiệu lực." });
            var absence = new MealAbsence
            {
                StudentId = input.StudentId, ReportedByUserId = userId,
                FromDate = input.FromDate, ToDate = input.ToDate, Reason = input.Reason.Trim(),
            };
            db.MealAbsences.Add(absence);
            await db.SaveChangesAsync();
            return Results.Created($"/api/parent/absences/{absence.Id}", new
            { absence.Id, absence.StudentId, absence.FromDate, absence.ToDate, absence.Reason, absence.ReportedAt });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapGet("/parent/absences", async (ClaimsPrincipal principal, MealTraceDbContext db) =>
            await db.MealAbsences.AsNoTracking().Where(x => x.ReportedByUserId == CurrentUserId(principal) &&
                db.ParentStudents.Any(p => p.UserId == CurrentUserId(principal) && p.StudentId == x.StudentId) && x.Student.IsActive)
                .OrderByDescending(x => x.ReportedAt).Take(100)
                .Select(x => new { x.Id, x.StudentId, StudentName = x.Student.FullName, x.FromDate, x.ToDate, x.Reason, x.ReportedAt, x.CancelledAt })
                .ToListAsync()).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences/{id:guid}/cancel", async (Guid id, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var absence = await db.MealAbsences.FirstOrDefaultAsync(x => x.Id == id && x.ReportedByUserId == CurrentUserId(principal));
            if (absence is null) return Results.NotFound();
            if (!await db.ParentStudents.AnyAsync(x => x.UserId == CurrentUserId(principal) && x.StudentId == absence.StudentId && x.Student.IsActive))
                return Results.Forbid();
            if (absence.CancelledAt is not null) return Results.Conflict(new { message = "Báo vắng đã được hủy." });
            absence.CancelledAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/meal-days", async (CreateMealDay input, MealTraceDbContext db) =>
        {
            var mealType = input.MealType?.Trim();
            var schoolYear = input.SchoolYear?.Trim();
            if (string.IsNullOrWhiteSpace(mealType) || mealType.Length > 60 || string.IsNullOrWhiteSpace(schoolYear) ||
                !await db.Classes.AnyAsync(x => x.SchoolYear == schoolYear))
                return Results.BadRequest(new { message = "Tên phiên ăn hoặc niên khóa không hợp lệ." });
            if (await db.MealDays.AnyAsync(x => x.Date == input.Date && x.MealType == mealType))
                return Results.Conflict(new { message = "Phiên ăn này đã tồn tại." });
            var day = new MealDay
            {
                Date = input.Date, MealType = mealType, SchoolYear = schoolYear,
                CutoffAt = new DateTimeOffset(input.Date.ToDateTime(DefaultCutoff), SchoolOffset).ToUniversalTime(),
            };
            db.MealDays.Add(day);
            await db.SaveChangesAsync();
            return Results.Created($"/api/meal-days/{day.Id}", new { day.Id, day.Date, day.MealType, day.SchoolYear, day.CutoffAt });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapGet("/meal-days/workflow", async (MealTraceDbContext db) =>
            await db.MealDays.AsNoTracking().OrderByDescending(x => x.Date).ThenBy(x => x.MealType).Take(60)
                .Select(x => new { x.Id, x.Date, x.MealType, x.SchoolYear, x.CutoffAt,
                    IsSettled = x.SettledAt != null })
                .ToListAsync()).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/meal-days/{id:guid}/exceptions", async (Guid id, RecordException input, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var day = await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (day is null) return Results.NotFound();
            if (DateTimeOffset.UtcNow >= day.CutoffAt) return Results.Conflict(new { message = "Đã qua giờ chốt; không thể đổi số suất dự kiến." });
            if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                return Results.BadRequest(new { message = "Cần ghi lý do ngoại lệ." });
            var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.StudentId && x.IsActive);
            if (student is null) return Results.NotFound();
            if (day.SchoolYear is not null && !await db.Classes.AnyAsync(x => x.Id == student.ClassId && x.SchoolYear == day.SchoolYear))
                return Results.BadRequest(new { message = "Trẻ không thuộc niên khóa của phiên ăn." });
            if (!principal.IsInRole(RoleNames.Admin) && !await CanReadClass(db, principal, student.ClassId)) return Results.Forbid();
            db.MealRegistrations.Add(new MealRegistration
            {
                MealDayId = id, StudentId = student.Id, WillEat = input.WillEat, Reason = input.Reason.Trim(),
            });
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapGet("/meal-days/{id:guid}/portions", async (Guid id, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var day = await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (day is null) return Results.NotFound();
            var preview = await BuildPortions(db, day);
            if (principal.IsInRole(RoleNames.Teacher) && !principal.IsInRole(RoleNames.Admin) &&
                !principal.IsInRole(RoleNames.KitchenStaff))
            {
                var assigned = await db.TeacherAssignments.Where(x => x.UserId == CurrentUserId(principal)).Select(x => x.ClassId).ToListAsync();
                preview = preview.Where(x => assigned.Contains(x.ClassId)).ToList();
            }
            return Results.Ok(new { day.Id, day.Date, day.MealType, day.CutoffAt, Classes = preview });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/meal-days/{id:guid}/settle", async (Guid id, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var day = await db.MealDays.FirstOrDefaultAsync(x => x.Id == id);
            if (day is null) return Results.NotFound();
            if (DateTimeOffset.UtcNow < day.CutoffAt) return Results.Conflict(new { message = "Chưa đến giờ chốt suất." });
            if (day.SettledAt is not null || await db.PortionSettlements.AnyAsync(x => x.MealDayId == id))
                return Results.Conflict(new { message = "Phiên ăn đã có bản chốt." });
            var portions = await BuildPortions(db, day);
            if (portions.Count == 0) return Results.BadRequest(new { message = "Niên khóa chưa có lớp với trẻ đang hoạt động." });
            var actor = CurrentUserId(principal).ToString();
            foreach (var room in portions)
            {
                db.PortionSettlements.Add(new PortionSettlement
                {
                    MealDayId = id, ClassId = room.ClassId, ClassName = room.ClassName,
                    Count = room.StudentIds.Count, CutoffAt = day.CutoffAt, SettledBy = actor,
                    Students = room.StudentIds.Select((studentId, index) => new SettlementStudent
                    { StudentId = studentId, StudentName = room.StudentNames[index] }).ToList(),
                });
            }
            day.SettledAt = DateTimeOffset.UtcNow;
            try
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
            {
                return Results.Conflict(new { message = "Phiên ăn vừa được chốt bởi yêu cầu khác. Hãy tải lại danh sách." });
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Results.Conflict(new { message = "Phiên ăn vừa được chốt bởi yêu cầu khác. Hãy tải lại danh sách." });
            }
            return Results.Ok(new { day.Id, Classes = portions.Count, Total = portions.Sum(x => x.StudentIds.Count) });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        return app;
    }

    private sealed record PortionRow(Guid ClassId, string ClassName, string SchoolYear,
        List<Guid> StudentIds, List<string> StudentNames, List<Guid> AbsentStudentIds, bool IsSettled);

    private static async Task<List<PortionRow>> BuildPortions(MealTraceDbContext db, MealDay day)
    {
        var settled = await db.PortionSettlements.AsNoTracking().Where(x => x.MealDayId == day.Id && x.ClassId != null)
            .Include(x => x.Class).Include(x => x.Students).ToListAsync();
        if (day.SettledAt is not null)
            return settled.Select(x => new PortionRow(x.ClassId!.Value, x.ClassName ?? x.Class!.Name,
                x.Class?.SchoolYear ?? day.SchoolYear ?? "", x.Students.Select(s => s.StudentId).ToList(),
                x.Students.Select(s => s.StudentName).ToList(), [], true)).ToList();
        var classes = await db.Classes.AsNoTracking().Where(x => x.Students.Any(s => s.IsActive) && (day.SchoolYear == null || x.SchoolYear == day.SchoolYear))
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.SchoolYear }).ToListAsync();
        var students = await db.Students.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new { x.Id, x.FullName, x.ClassId }).ToListAsync();
        var asOf = DateTimeOffset.UtcNow < day.CutoffAt ? DateTimeOffset.UtcNow : day.CutoffAt;
        var absences = await db.MealAbsences.AsNoTracking().Where(x => x.FromDate <= day.Date && x.ToDate >= day.Date)
            .Select(x => new { x.StudentId, x.ReportedAt, x.CancelledAt }).ToListAsync();
        var absentIds = absences.Where(x => x.ReportedAt <= asOf && (x.CancelledAt is null || x.CancelledAt > asOf))
            .Select(x => x.StudentId).ToHashSet();
        var overrides = (await db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == day.Id)
            .Select(x => new { x.Id, x.StudentId, x.WillEat, x.RecordedAt }).ToListAsync())
            .Where(x => x.RecordedAt <= asOf).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToList();
        var latestOverrides = overrides.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.First().WillEat);
        return classes.Select(room =>
        {
            var snapshot = settled.FirstOrDefault(x => x.ClassId == room.Id);
            if (snapshot is not null)
            {
                return new PortionRow(room.Id, snapshot.ClassName ?? room.Name, room.SchoolYear,
                    snapshot.Students.Select(x => x.StudentId).ToList(), snapshot.Students.Select(x => x.StudentName).ToList(), [], true);
            }
            var members = students.Where(x => x.ClassId == room.Id).OrderBy(x => x.FullName).ToList();
            var eating = members.Where(x => latestOverrides.TryGetValue(x.Id, out var willEat) ? willEat : !absentIds.Contains(x.Id)).ToList();
            return new PortionRow(room.Id, room.Name, room.SchoolYear,
                eating.Select(x => x.Id).ToList(), eating.Select(x => x.FullName).ToList(),
                members.Where(x => !eating.Any(e => e.Id == x.Id)).Select(x => x.Id).ToList(), false);
        }).ToList();
    }

    private static DateOnly SchoolToday() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(SchoolOffset).DateTime);
    private static Guid CurrentUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static async Task<bool> CanReadClass(MealTraceDbContext db, ClaimsPrincipal principal, Guid classId) =>
        principal.IsInRole(RoleNames.Admin) || await db.TeacherAssignments.AnyAsync(x => x.UserId == CurrentUserId(principal) && x.ClassId == classId);
}

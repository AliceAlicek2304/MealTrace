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
    private sealed record CreateStudent(string FullName, Guid ClassId, string? StudentCode = null, DateOnly? StartDate = null);
    private sealed record LinkParent(string? Email, string? FullName, string? PhoneNumber = null);
    private sealed record CreateMealDay(DateOnly Date, string MealType, string SchoolYear);
    private sealed record ReportAbsence(Guid StudentId, DateOnly FromDate, DateOnly ToDate, string Reason);

    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization().WithTags("Meal workflow");
        api.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (Exception ex) when (DatabaseConflicts.IsConflict(ex))
            { return Results.Conflict(new { message = "Dữ liệu trùng hoặc vừa thay đổi. Hãy tải lại và thử lại." }); }
        });

        api.MapGet("/classes", async (ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var query = db.Classes.AsNoTracking().AsQueryable();
            if (!principal.IsInRole(RoleNames.Admin) && principal.IsInRole(RoleNames.Teacher))
            {
                var userId = CurrentUserId(principal);
                query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == userId && a.ClassId == x.Id));
            }
            var date = SchoolToday();
            return await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.SchoolYear, StudentCount = db.Enrollments.Count(s => s.ClassId == x.Id && s.StartDate <= date && (s.EndDate == null || s.EndDate > date)) }).ToListAsync();
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/classes", async (CreateClass input, MealTraceDbContext db) =>
        {
            var name = input.Name?.Trim();
            var year = input.SchoolYear?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || string.IsNullOrWhiteSpace(year) ||
                !System.Text.RegularExpressions.Regex.IsMatch(year, @"^\d{4}-\d{4}$") || int.Parse(year[5..]) != int.Parse(year[..4]) + 1)
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
            var students = await StudentAdministrationEndpoints.OnDate(db, SchoolToday()).Where(x => x.ClassId == classId)
                .OrderBy(x => x.Student.FullName).Select(x => new { Id = x.StudentId, x.Student.StudentCode, x.Student.FullName, x.ClassId }).ToListAsync();
            var ids = students.Select(x => x.Id).ToArray();
            var parents = await db.ParentStudents.AsNoTracking().Where(x => ids.Contains(x.StudentId))
                .Select(x => new { x.StudentId, x.User.Id, x.User.FullName, x.User.Email, x.User.PhoneNumber }).ToListAsync();
            return Results.Ok(students.Select(x => new
            {
                x.Id, x.StudentCode, x.FullName, x.ClassId,
                Parents = parents.Where(p => p.StudentId == x.Id).Select(p => new { p.Id, p.FullName, p.Email, p.PhoneNumber }).ToArray(),
            }));
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));

        api.MapPost("/students", async (CreateStudent input, ClaimsPrincipal principal, MealTraceDbContext db) =>
        {
            var name = input.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || !await db.Classes.AnyAsync(x => x.Id == input.ClassId))
                return Results.BadRequest(new { message = "Tên trẻ hoặc lớp không hợp lệ." });
            var student = new Student { FullName = name, ClassId = input.ClassId };
            if (!string.IsNullOrWhiteSpace(input.StudentCode))
            {
                var code = input.StudentCode.Trim().ToUpperInvariant();
                if (code == "HS-DEMO-0001") return Results.BadRequest(new { message = "Mã HS-DEMO-0001 dành riêng cho dữ liệu demo." });
                if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Z0-9][A-Z0-9-]{2,39}$"))
                    return Results.BadRequest(new { message = "Mã trẻ gồm 3–40 ký tự chữ không dấu, số hoặc dấu gạch ngang." });
                student.StudentCode = code;
            }
            if (await db.Students.AnyAsync(x => x.StudentCode == student.StudentCode))
                return Results.Conflict(new { message = "Mã trẻ đã tồn tại." });
            var start = input.StartDate ?? SchoolToday();
            if (start < SchoolToday()) return Results.BadRequest(new { message = "Ghi danh mới không được lùi ngày về quá khứ." });
            student.Enrollments.Add(new Enrollment { ClassId = input.ClassId, StartDate = start, RecordedByUserId = CurrentUserId(principal) });
            db.Students.Add(student);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            { return Results.Conflict(new { message = "Mã trẻ đã tồn tại." }); }
            return Results.Created($"/api/students/{student.Id}", new { student.Id, student.StudentCode, student.FullName, student.ClassId, student.Revision });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin));

        api.MapPost("/admin/students/{studentId:guid}/parents", async (Guid studentId, LinkParent input,
            MealTraceDbContext db, UserManager<ApplicationUser> users) =>
        {
            if (!await db.Students.AnyAsync(x => x.Id == studentId)) return Results.NotFound();
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
            await StudentAdministrationEndpoints.OnDate(db, SchoolToday()).Where(x => db.ParentStudents.Any(p => p.UserId == CurrentUserId(principal) && p.StudentId == x.StudentId))
                .OrderBy(x => x.Student.FullName)
                .Select(x => new { x.StudentId, x.Student.StudentCode, x.Student.FullName, x.ClassId, ClassName = x.Class.Name })
                .ToListAsync()).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences", async (ReportAbsence input, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            // Lock a shared parent row so two guardians cannot create overlapping absence intervals.
            if (db.Database.IsNpgsql())
                await db.Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {input.StudentId} FOR UPDATE").ToListAsync();
            var userId = CurrentUserId(principal);
            if (!await db.ParentStudents.AnyAsync(x => x.UserId == userId && x.StudentId == input.StudentId) ||
                !await StudentAdministrationEndpoints.OnDate(db, SchoolToday()).AnyAsync(x => x.StudentId == input.StudentId))
                return Results.Forbid();
            var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SchoolOffset).DateTime);
            if (input.FromDate < today || input.FromDate.Year >= 9999 || input.ToDate < input.FromDate || input.ToDate > input.FromDate.AddYears(1).AddDays(-1) ||
                string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                return Results.BadRequest(new { message = "Khoảng không ăn phải từ hôm nay, tối đa một năm tính từ ngày bắt đầu và có lý do." });
            if (await db.MealAbsences.AnyAsync(x => x.StudentId == input.StudentId && x.CancelledAt == null &&
                x.FromDate <= input.ToDate && x.ToDate >= input.FromDate))
                return Results.Conflict(new { message = "Khoảng ngày vắng bị trùng với báo vắng đang hiệu lực." });
            var absence = new MealAbsence
            {
                StudentId = input.StudentId, ReportedByUserId = userId,
                FromDate = input.FromDate, ToDate = input.ToDate, Reason = input.Reason.Trim(), ReportedAt = clock.GetUtcNow(),
            };
            db.MealAbsences.Add(absence);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.Created($"/api/parent/absences/{absence.Id}", new
            { absence.Id, absence.StudentId, absence.FromDate, absence.ToDate, absence.Reason, absence.ReportedAt });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        // Cancel the original and append a replacement so decisions at an earlier cutoff keep their original dates.
        api.MapPost("/parent/absences/{id:guid}/replace", async (Guid id, ReportAbsence input, ClaimsPrincipal principal,
            MealTraceDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var userId = CurrentUserId(principal);
            var original = await db.MealAbsences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ReportedByUserId == userId);
            if (original is null) return Results.NotFound();
            if (db.Database.IsNpgsql())
                await db.Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {original.StudentId} FOR UPDATE").ToListAsync();
            var absence = await db.MealAbsences.SingleAsync(x => x.Id == id);
            if (!await db.ParentStudents.AnyAsync(x => x.UserId == userId && x.StudentId == absence.StudentId)) return Results.Forbid();
            if (absence.CancelledAt is not null) return Results.Conflict(new { message = "Đăng ký đã hủy hoặc được cập nhật. Hãy tải lại danh sách." });
            var now = clock.GetUtcNow(); var today = DateOnly.FromDateTime(now.ToOffset(SchoolOffset).DateTime);
            if (input.StudentId != absence.StudentId || input.FromDate.Year >= 9999 || input.ToDate < today || input.ToDate < input.FromDate ||
                (input.FromDate < today && input.FromDate != absence.FromDate) || input.ToDate > input.FromDate.AddYears(1).AddDays(-1) ||
                string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                return Results.BadRequest(new { message = "Giữ ngày bắt đầu cũ hoặc chọn từ hôm nay; ngày kết thúc từ hôm nay, tối đa một năm và có lý do. Muốn ăn lại ngay hãy hủy đăng ký." });
            if (await db.MealAbsences.AnyAsync(x => x.Id != id && x.StudentId == absence.StudentId && x.CancelledAt == null &&
                x.FromDate <= input.ToDate && x.ToDate >= input.FromDate))
                return Results.Conflict(new { message = "Khoảng ngày trùng với đăng ký đang hiệu lực." });
            absence.CancelledAt = now;
            var replacement = new MealAbsence { StudentId = absence.StudentId, ReportedByUserId = userId,
                FromDate = input.FromDate, ToDate = input.ToDate, Reason = input.Reason.Trim(), ReportedAt = now };
            db.MealAbsences.Add(replacement); await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Results.Ok(new { replacement.Id, replacedId = id });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Parent)).WithName("UpdateMealAbsencePeriod");

        api.MapGet("/parent/absences", async (ClaimsPrincipal principal, MealTraceDbContext db) =>
            await db.MealAbsences.AsNoTracking().Where(x => x.ReportedByUserId == CurrentUserId(principal) &&
                db.ParentStudents.Any(p => p.UserId == CurrentUserId(principal) && p.StudentId == x.StudentId))
                .OrderByDescending(x => x.ReportedAt).Take(100)
                .Select(x => new { x.Id, x.StudentId, StudentName = x.Student.FullName, x.FromDate, x.ToDate, x.Reason, x.ReportedAt, x.CancelledAt })
                .ToListAsync()).RequireAuthorization(p => p.RequireRole(RoleNames.Parent));

        api.MapPost("/parent/absences/{id:guid}/cancel", async (Guid id, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var original = await db.MealAbsences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ReportedByUserId == CurrentUserId(principal));
            if (original is null) return Results.NotFound();
            if (db.Database.IsNpgsql())
                await db.Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {original.StudentId} FOR UPDATE").ToListAsync();
            var absence = await db.MealAbsences.SingleAsync(x => x.Id == id);
            if (!await db.ParentStudents.AnyAsync(x => x.UserId == CurrentUserId(principal) && x.StudentId == absence.StudentId))
                return Results.Forbid();
            if (absence.CancelledAt is not null) return Results.Conflict(new { message = "Báo vắng đã được hủy." });
            absence.CancelledAt = clock.GetUtcNow();
            await db.SaveChangesAsync(); await transaction.CommitAsync();
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

        api.MapGet("/meal-days/{id:guid}/portions", async (Guid id, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            var day = await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (day is null) return Results.NotFound();
            var preview = await BuildPortions(db, day, clock.GetUtcNow());
            if (principal.IsInRole(RoleNames.Teacher) && !principal.IsInRole(RoleNames.Admin) &&
                !principal.IsInRole(RoleNames.KitchenStaff))
            {
                var assigned = await db.TeacherAssignments.Where(x => x.UserId == CurrentUserId(principal)).Select(x => x.ClassId).ToListAsync();
                preview = preview.Where(x => assigned.Contains(x.ClassId)).ToList();
            }
            return Results.Ok(new { day.Id, day.Date, day.MealType, day.CutoffAt, Classes = preview });
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher, RoleNames.KitchenStaff));

        api.MapPost("/meal-days/{id:guid}/settle", async (Guid id, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var day = db.Database.IsNpgsql()
                ? (await db.MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).SingleOrDefault()
                : await db.MealDays.FindAsync(id);
            if (day is null) return Results.NotFound();
            if (clock.GetUtcNow() < day.CutoffAt) return Results.Conflict(new { message = "Chưa đến giờ chốt suất." });
            if (day.SettledAt is not null || await db.PortionSettlements.AnyAsync(x => x.MealDayId == id))
                return Results.Conflict(new { message = "Phiên ăn đã có bản chốt." });
            var portions = await BuildPortions(db, day, clock.GetUtcNow());
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
            day.SettledAt = clock.GetUtcNow();
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

    private static async Task<List<PortionRow>> BuildPortions(MealTraceDbContext db, MealDay day, DateTimeOffset now)
    {
        var settled = await db.PortionSettlements.AsNoTracking().Where(x => x.MealDayId == day.Id && x.ClassId != null)
            .Include(x => x.Class).Include(x => x.Students).ToListAsync();
        if (day.SettledAt is not null)
            return settled.Select(x => new PortionRow(x.ClassId!.Value, x.ClassName ?? x.Class!.Name,
                x.Class?.SchoolYear ?? day.SchoolYear ?? "", x.Students.Select(s => s.StudentId).ToList(),
                x.Students.Select(s => s.StudentName).ToList(), [], true)).ToList();
        var decisions = await MealDecisionService.ReadAsync(db, day, now);
        return decisions.GroupBy(x => new { x.ClassId, x.ClassName, x.SchoolYear }).OrderBy(x => x.Key.ClassName).Select(group =>
        {
            var room = group.Key;
            var snapshot = settled.FirstOrDefault(x => x.ClassId == room.ClassId);
            if (snapshot is not null)
                return new PortionRow(room.ClassId, snapshot.ClassName ?? room.ClassName, room.SchoolYear,
                    snapshot.Students.Select(x => x.StudentId).ToList(), snapshot.Students.Select(x => x.StudentName).ToList(), [], true);
            var members = group.OrderBy(x => x.FullName).ThenBy(x => x.StudentId).ToList();
            var eating = members.Where(x => x.WillEat).ToList();
            return new PortionRow(room.ClassId, room.ClassName, room.SchoolYear,
                eating.Select(x => x.StudentId).ToList(), eating.Select(x => x.FullName).ToList(),
                members.Where(x => !x.WillEat).Select(x => x.StudentId).ToList(), false);
        }).ToList();
    }

    private static DateOnly SchoolToday() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(SchoolOffset).DateTime);
    private static Guid CurrentUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static async Task<bool> CanReadClass(MealTraceDbContext db, ClaimsPrincipal principal, Guid classId) =>
        principal.IsInRole(RoleNames.Admin) || await db.TeacherAssignments.AnyAsync(x => x.UserId == CurrentUserId(principal) && x.ClassId == classId);
}

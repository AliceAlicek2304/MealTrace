using System.Security.Claims;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using MealTrace.Api.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MealTrace.Api.Features;

public static class StudentAdministrationEndpoints
{
    private sealed record EditClass(string Name);
    private sealed record EditStudent(string FullName, int Revision);
    private sealed record ChangeEnrollment(Guid? ClassId, DateOnly EffectiveDate, string Reason, int Revision);

    public static IQueryable<Enrollment> OnDate(MealTraceDbContext db, DateOnly date) => db.Enrollments.AsNoTracking()
        .Where(x => x.StartDate <= date && (x.EndDate == null || x.EndDate > date));

    public static IEndpointRouteBuilder MapStudentAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Classes and enrollment")
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (DbUpdateConcurrencyException) { return Conflict(); }
            catch (Exception ex) when (DatabaseConflicts.IsConflict(ex))
            { return Results.Conflict(new { message = "Mã trẻ, lớp hoặc ngày ghi danh đã tồn tại. Hãy tải lại dữ liệu." }); }
        });

        group.MapGet("/classes", async (string? search, int? page, int? pageSize, MealTraceDbContext db, TimeProvider clock) =>
        {
            var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var query = db.Classes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
            }
            var date = SchoolTime.Today(clock.GetUtcNow()); var total = await query.CountAsync();
            var items = await query.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).ThenBy(x => x.Id)
                .Skip((number - 1) * size).Take(size).Select(x => new { x.Id, x.Name, x.SchoolYear,
                    StudentCount = db.Enrollments.Count(e => e.ClassId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)) }).ToListAsync();
            return Results.Ok(new { items, total, page = number, pageSize = size });
        }).WithName("SearchClasses");

        group.MapPut("/classes/{id:guid}", async (Guid id, EditClass input, MealTraceDbContext db) =>
        {
            var room = await db.Classes.FindAsync(id); if (room is null) return Results.NotFound();
            var name = input.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) return Results.BadRequest(new { message = "Tên lớp không hợp lệ." });
            if (await db.Classes.AnyAsync(x => x.Id != id && x.SchoolYear == room.SchoolYear && x.Name == name))
                return Results.Conflict(new { message = "Lớp đã tồn tại trong niên khóa." });
            room.Name = name; await db.SaveChangesAsync(); return Results.NoContent();
        }).WithName("EditClass");

        group.MapGet("/students", async (Guid? classId, string? search, string? status, int? page, int? pageSize, MealTraceDbContext db, TimeProvider clock) =>
        {
            var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var now = clock.GetUtcNow(); var date = SchoolTime.Today(now); var query = db.Students.AsNoTracking();
            if (classId.HasValue) query = query.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId &&
                e.StartDate <= date && (e.EndDate == null || e.EndDate > date)) ||
                (!db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)) && x.ClassId == classId));
            if (status == "ACTIVE") query = query.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
            else if (status == "INACTIVE") query = query.Where(x => !db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
            }
            var total = await query.CountAsync();
            var rows = await query.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((number - 1) * size).Take(size)
                .Select(x => new { x.Id, x.StudentCode, x.FullName, x.Revision,
                    IsActive = db.Enrollments.Any(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)),
                    ClassId = db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)).Select(e => (Guid?)e.ClassId).FirstOrDefault() ?? x.ClassId,
                    ClassName = db.Enrollments.Where(e => e.StudentId == x.Id && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)).Select(e => e.Class.Name).FirstOrDefault() ?? x.Class.Name }).ToListAsync();
            var ids = rows.Select(x => x.Id).ToArray();
            var parents = await db.ParentStudents.AsNoTracking().Where(x => ids.Contains(x.StudentId))
                .Select(x => new { x.StudentId, x.User.Id, x.User.FullName, x.User.Email, x.User.PhoneNumber }).ToListAsync();
            return Results.Ok(new { items = rows.Select(x => new { x.Id, x.StudentCode, x.FullName, x.Revision, x.IsActive, x.ClassId, x.ClassName,
                Parents = parents.Where(p => p.StudentId == x.Id).Select(p => new { p.Id, p.FullName, p.Email, p.PhoneNumber }) }),
                total, page = number, pageSize = size, earliestChangeDate = SchoolTime.EarliestEnrollmentDate(now) });
        }).WithName("SearchStudents");

        group.MapPut("/students/{id:guid}", async (Guid id, EditStudent input, MealTraceDbContext db) =>
        {
            var student = await db.Students.FindAsync(id); if (student is null) return Results.NotFound();
            if (student.Revision != input.Revision) return Conflict();
            var name = input.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 150) return Results.BadRequest(new { message = "Họ tên trẻ không hợp lệ." });
            student.FullName = name; student.Revision++; await db.SaveChangesAsync(); return Results.NoContent();
        }).WithName("EditStudent");

        group.MapGet("/students/{id:guid}/enrollments", async (Guid id, MealTraceDbContext db) =>
        {
            if (!await db.Students.AnyAsync(x => x.Id == id)) return Results.NotFound();
            return Results.Ok(await db.Enrollments.AsNoTracking().Where(x => x.StudentId == id).OrderBy(x => x.StartDate)
                .Select(x => new { x.Id, x.ClassId, ClassName = x.Class.Name, x.Class.SchoolYear, x.StartDate, x.EndDate,
                    x.Reason, x.EndReason, x.RecordedAt, x.RecordedByUserId, x.EndRecordedAt, x.EndedByUserId }).ToListAsync());
        }).WithName("StudentEnrollmentHistory");

        // ClassId null means withdrawal; otherwise transfer or re-enrollment. Parent links stay attached to the student.
        group.MapPost("/students/{id:guid}/enrollments", async (Guid id, ChangeEnrollment input, ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow()) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                return Results.BadRequest(new { message = "Cần lý do và ngày hiệu lực từ ngày cho phép. Sau 07:30, thay đổi áp dụng từ ngày mai." });
            await using var transaction = await db.Database.BeginTransactionAsync();
            // Same student lock as absence/exception writes. Read revision and enrollment after waiting.
            if (db.Database.IsNpgsql())
                await db.Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync();
            var student = await db.Students.Include(x => x.Enrollments).FirstOrDefaultAsync(x => x.Id == id);
            if (student is null) return Results.NotFound();
            if (student.Revision != input.Revision) return Conflict();
            var latest = student.Enrollments.OrderByDescending(x => x.StartDate).FirstOrDefault();
            if (latest is not null && (input.EffectiveDate <= latest.StartDate || latest.EndDate > input.EffectiveDate))
                return Results.Conflict(new { message = "Ngày hiệu lực phải sau ngày bắt đầu lần ghi danh gần nhất và không chồng lịch sử." });
            if (input.ClassId.HasValue)
            {
                if (!await db.Classes.AnyAsync(x => x.Id == input.ClassId)) return Results.BadRequest(new { message = "Lớp không tồn tại." });
                if (latest is { EndDate: null } && latest.ClassId == input.ClassId)
                    return Results.BadRequest(new { message = "Trẻ đã được ghi danh vào lớp này." });
            }
            else if (latest is null || latest.EndDate is not null) return Results.Conflict(new { message = "Trẻ không có ghi danh đang mở để ngừng học." });
            var acceptedAt = clock.GetUtcNow();
            if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(acceptedAt)) return EnrollmentTimeExpired();
            var actor = Guid.Parse(principal.FindFirstValue("sub")!); var reason = input.Reason.Trim();
            if (latest is { EndDate: null })
            {
                latest.EndDate = input.EffectiveDate; latest.EndReason = reason; latest.EndedByUserId = actor; latest.EndRecordedAt = acceptedAt;
            }
            student.Revision++;
            // Save the closed enrollment before inserting another open one (partial unique index).
            await db.SaveChangesAsync();
            // A blocked UPDATE may have crossed cutoff. Roll back the whole transfer, including the closed enrollment.
            if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow())) return EnrollmentTimeExpired();
            if (input.ClassId.HasValue)
            {
                db.Enrollments.Add(new Enrollment { StudentId = id, ClassId = input.ClassId.Value,
                    StartDate = input.EffectiveDate, Reason = reason, RecordedByUserId = actor, RecordedAt = acceptedAt });
                student.ClassId = input.ClassId.Value;
            }
            // Legacy flag means an open or future enrollment exists. Current status is always date-derived.
            student.IsActive = input.ClassId.HasValue || input.EffectiveDate > SchoolTime.Today(acceptedAt);
            await db.SaveChangesAsync();
            if (input.EffectiveDate < SchoolTime.EarliestEnrollmentDate(clock.GetUtcNow())) return EnrollmentTimeExpired();
            await transaction.CommitAsync(); return Results.NoContent();
        }).WithName("ChangeStudentEnrollment");
        return app;
    }

    private static IResult Conflict() => Results.Conflict(new { message = "Hồ sơ đã được người khác cập nhật. Hãy đóng cửa sổ và tải lại trước khi sửa." });
    private static IResult EnrollmentTimeExpired() => Results.Conflict(new { message = "Đã qua giờ chốt hoặc sang ngày mới trong lúc lưu. Tải lại ngày hiệu lực cho phép trước khi sửa." });
}

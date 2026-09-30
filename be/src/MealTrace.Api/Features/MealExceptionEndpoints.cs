using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public static class MealExceptionEndpoints
{
    // WillEat preserves the old payload for a first override. New clients use Action and ExpectedEventId.
    public sealed record ExceptionInput(Guid StudentId, string Reason, string? Action = null, Guid? ExpectedEventId = null, bool? WillEat = null);

    public static IEndpointRouteBuilder MapMealExceptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/meal-days").WithTags("Meal exceptions")
            .RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.Teacher));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (DbUpdateConcurrencyException) { return Stale(); }
            catch (Exception ex) when (DatabaseConflicts.IsConflict(ex)) { return Stale(); }
        });

        group.MapGet("/{id:guid}/decisions", async (Guid id, Guid? classId, string? search, int? page, int? pageSize,
            ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            var day = await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); if (day is null) return Results.NotFound();
            var allowed = await AllowedClasses(db, principal);
            if (classId.HasValue && allowed is not null && !allowed.Contains(classId.Value)) return Results.Forbid();
            var now = clock.GetUtcNow(); var all = await MealDecisionService.ReadAsync(db, day, now, allowed);
            var rooms = all.GroupBy(x => new { x.ClassId, x.ClassName }).Select(x => new { id = x.Key.ClassId, name = x.Key.ClassName }).OrderBy(x => x.name).ToArray();
            var filtered = all.AsEnumerable();
            if (classId.HasValue) filtered = filtered.Where(x => x.ClassId == classId.Value);
            if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(x => x.FullName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) || x.StudentCode.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            var rows = filtered.OrderBy(x => x.ClassName).ThenBy(x => x.FullName).ThenBy(x => x.StudentId).ToList();
            var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            return Results.Ok(new { day.Id, day.Date, day.CutoffAt, isSettled = day.SettledAt != null,
                canEdit = day.SettledAt == null && now < day.CutoffAt, asOf = now < day.CutoffAt ? now : day.CutoffAt,
                classes = rooms, items = rows.Skip((number - 1) * size).Take(size), total = rows.Count, page = number, pageSize = size });
        }).WithName("MealDecisions");

        group.MapGet("/{id:guid}/students/{studentId:guid}/exceptions", async (Guid id, Guid studentId, int? page, int? pageSize,
            ClaimsPrincipal principal, MealTraceDbContext db, TimeProvider clock) =>
        {
            var day = await db.MealDays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); if (day is null) return Results.NotFound();
            var member = await Member(db, day, studentId, clock.GetUtcNow()); if (member is null) return Results.NotFound();
            if (day.SchoolYear is not null && member.Class.SchoolYear != day.SchoolYear)
                return Results.BadRequest(new { message = "Trẻ không thuộc niên khóa của phiên ăn." });
            if (!await CanEditClass(db, principal, member.ClassId)) return Results.Forbid();
            var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var query = db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == id && x.StudentId == studentId);
            var total = await query.CountAsync();
            var records = await query.OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
                .Skip((number - 1) * size).Take(size).ToListAsync();
            return Results.Ok(new { items = records.Select(x => new { x.Id, action = MealDecisionService.Action(x.WillEat), x.Reason,
                x.RecordedAt, x.RecordedByUserId, actorName = x.RecordedByName, isLegacy = x.RecordedByUserId == null, x.SupersedesId, x.Sequence }),
                total, page = number, pageSize = size });
        }).WithName("MealExceptionHistory");

        group.MapPost("/{id:guid}/exceptions", async (Guid id, ExceptionInput input, ClaimsPrincipal principal,
            MealTraceDbContext db, TimeProvider clock) =>
        {
            var action = input.Action?.Trim().ToUpperInvariant();
            if (action is null && input.WillEat.HasValue) action = MealDecisionService.Action(input.WillEat);
            if (action is not ("EAT" or "ABSENT" or "DEFAULT") ||
                (input.WillEat.HasValue && action != MealDecisionService.Action(input.WillEat)) ||
                string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500)
                return Results.BadRequest(new { message = "Chọn có suất/vắng/khôi phục mặc định và nhập lý do tối đa 500 ký tự." });
            await using var transaction = await db.Database.BeginTransactionAsync();
            // Serializes overrides and settlement on the same day. Recheck the clock after waiting for locks.
            var day = db.Database.IsNpgsql()
                ? (await db.MealDays.FromSqlInterpolated($"SELECT * FROM \"MealDays\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).SingleOrDefault()
                : await db.MealDays.FindAsync(id);
            if (day is null) return Results.NotFound();
            if (day.SettledAt is not null || clock.GetUtcNow() >= day.CutoffAt) return Closed();
            if (db.Database.IsNpgsql())
                await db.Students.FromSqlInterpolated($"SELECT * FROM \"Students\" WHERE \"Id\" = {input.StudentId} FOR UPDATE").ToListAsync();
            var member = await Member(db, day, input.StudentId, clock.GetUtcNow());
            if (member is null) return Results.NotFound();
            if (day.SchoolYear is not null && member.Class.SchoolYear != day.SchoolYear)
                return Results.BadRequest(new { message = "Trẻ không thuộc niên khóa của phiên ăn." });
            if (!await CanEditClass(db, principal, member.ClassId)) return Results.Forbid();
            var latest = await db.MealRegistrations.AsNoTracking().Where(x => x.MealDayId == id && x.StudentId == input.StudentId)
                .OrderByDescending(x => x.Sequence).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
            if (latest?.Id != input.ExpectedEventId) return Stale();
            var now = clock.GetUtcNow(); if (now >= day.CutoffAt) return Closed();
            var actor = await db.Users.AsNoTracking().Where(x => x.Id == UserId(principal)).Select(x => x.FullName).SingleAsync();
            var record = new MealRegistration { MealDayId = id, StudentId = input.StudentId,
                WillEat = action == "DEFAULT" ? null : action == "EAT", Reason = input.Reason.Trim(),
                RecordedByUserId = UserId(principal), RecordedByName = actor, RecordedAt = now,
                SupersedesId = latest?.Id, Sequence = checked((latest?.Sequence ?? 0) + 1) };
            // The authoritative acceptance time is sampled immediately before persisting.
            record.RecordedAt = clock.GetUtcNow(); if (record.RecordedAt >= day.CutoffAt) return Closed();
            day.DecisionRevision++;
            db.MealRegistrations.Add(record); await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Results.Ok(new { record.Id, action, record.RecordedAt, record.SupersedesId, record.Sequence });
        }).WithName("RecordMealException");
        return app;
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private static async Task<Guid[]?> AllowedClasses(MealTraceDbContext db, ClaimsPrincipal principal) => principal.IsInRole(RoleNames.Admin)
        ? null : await db.TeacherAssignments.Where(x => x.UserId == UserId(principal)).Select(x => x.ClassId).ToArrayAsync();
    private static async Task<bool> CanEditClass(MealTraceDbContext db, ClaimsPrincipal principal, Guid classId) =>
        principal.IsInRole(RoleNames.Admin) || await db.TeacherAssignments.AnyAsync(x => x.UserId == UserId(principal) && x.ClassId == classId);
    private static Task<Enrollment?> Member(MealTraceDbContext db, MealDay day, Guid studentId, DateTimeOffset now) =>
        StudentAdministrationEndpoints.OnDate(db, day.Date).Include(x => x.Class).FirstOrDefaultAsync(x => x.StudentId == studentId && x.RecordedAt <= (now < day.CutoffAt ? now : day.CutoffAt));
    private static IResult Closed() => Results.Conflict(new { message = "Đã qua giờ chốt hoặc phiên đã chốt; không thể đổi số suất dự kiến." });
    private static IResult Stale() => Results.Conflict(new { message = "Ngoại lệ đã được cập nhật. Hãy tải lại danh sách và mở lại form trước khi lưu." });
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Data;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public static class AccountEndpoints
{
    public sealed record AccountInput(string FullName, string Email, string[] Roles, string Status,
        Guid[] ClassIds, Guid[] StudentIds, DateOnly? InspectorAccessUntil);
    public sealed record AccountView(Guid Id, string FullName, string Email, string[] Roles, string Status,
        Guid[] ClassIds, Guid[] StudentIds, DateOnly? InspectorAccessUntil);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Account administration")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        group.MapGet("/users", async (int? page, int? pageSize, MealTraceDbContext db) =>
        {
            var number = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var total = await db.Users.CountAsync();
            var users = await db.Users.AsNoTracking().OrderBy(x => x.Email)
                .Skip((number - 1) * size).Take(size).ToListAsync();
            var ids = users.Select(x => x.Id).ToArray();
            var roleRows = await (from link in db.UserRoles.AsNoTracking()
                join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id
                where ids.Contains(link.UserId)
                select new { link.UserId, Role = role.Name! }).ToListAsync();
            var teacherRows = await db.TeacherAssignments.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync();
            var parentRows = await db.ParentStudents.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync();
            var grants = await db.InspectorGrants.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync();
            var items = users.Select(user => new AccountView(user.Id, user.FullName, user.Email!,
                roleRows.Where(x => x.UserId == user.Id).Select(x => x.Role).ToArray(),
                user.IsActive ? "ACTIVE" : "SUSPENDED",
                teacherRows.Where(x => x.UserId == user.Id).Select(x => x.ClassId).ToArray(),
                parentRows.Where(x => x.UserId == user.Id).Select(x => x.StudentId).ToArray(),
                grants.FirstOrDefault(x => x.UserId == user.Id)?.ExpiresOn)).ToArray();
            return Results.Ok(new { items, total, page = number, pageSize = size });
        }).WithName("ListUsers");

        group.MapGet("/users/{id:guid}", async (Guid id, MealTraceDbContext db, UserManager<ApplicationUser> manager) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (user is null) return Results.NotFound();
            var roles = (await manager.GetRolesAsync(user)).ToArray();
            var classIds = await db.TeacherAssignments.AsNoTracking().Where(x => x.UserId == id).Select(x => x.ClassId).ToArrayAsync();
            var studentIds = await db.ParentStudents.AsNoTracking().Where(x => x.UserId == id).Select(x => x.StudentId).ToArrayAsync();
            var grant = await db.InspectorGrants.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id);
            return Results.Ok(new AccountView(user.Id, user.FullName, user.Email!, roles,
                user.IsActive ? "ACTIVE" : "SUSPENDED", classIds, studentIds, grant?.ExpiresOn));
        }).WithName("GetUser");

        group.MapGet("/scope-options", async (MealTraceDbContext db) => Results.Ok(new
        {
            classes = await db.Classes.AsNoTracking().OrderBy(x => x.Name).Take(200)
                .Select(x => new { id = x.Id, name = x.Name }).ToListAsync(),
            students = await db.Students.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Take(200)
                .Select(x => new { id = x.Id, name = x.FullName, classId = x.ClassId }).ToListAsync(),
        })).WithName("GetScopeOptions");

        group.MapPost("/users", async (AccountInput input, ClaimsPrincipal principal, HttpContext http,
            MealTraceDbContext db, UserManager<ApplicationUser> manager) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var error = await ValidateAsync(input, db);
            if (error is not null) return Results.BadRequest(new { message = error });
            var email = input.Email.Trim().ToLowerInvariant();
            if (await manager.FindByEmailAsync(email) is not null)
                return Results.Conflict(new { message = "Email đã được sử dụng." });

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                FullName = input.FullName.Trim(), IsActive = input.Status == "ACTIVE",
            };
            var temporaryPassword = "Mt!9" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', 'A').Replace('/', 'b');
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var created = await manager.CreateAsync(user, temporaryPassword);
            if (!created.Succeeded) return Results.BadRequest(new { message = string.Join("; ", created.Errors.Select(x => x.Description)) });
            var roleResult = await manager.AddToRolesAsync(user, input.Roles);
            if (!roleResult.Succeeded) return Results.BadRequest(new { message = string.Join("; ", roleResult.Errors.Select(x => x.Description)) });
            await ReplaceScopesAsync(db, user.Id, input, Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!));
            await transaction.CommitAsync();
            return Results.Created($"/api/admin/users/{user.Id}", new
            {
                user = ToView(user, input), temporaryPassword,
                message = "Mật khẩu tạm chỉ hiển thị một lần. Hãy chuyển cho người dùng qua kênh an toàn.",
            });
        }).WithName("CreateUser");

        group.MapPut("/users/{id:guid}", async (Guid id, AccountInput input, ClaimsPrincipal principal,
            MealTraceDbContext db, UserManager<ApplicationUser> manager) =>
        {
            var error = await ValidateAsync(input, db);
            if (error is not null) return Results.BadRequest(new { message = error });
            var user = await manager.FindByIdAsync(id.ToString());
            if (user is null) return Results.NotFound();
            var email = input.Email.Trim().ToLowerInvariant();
            var other = await manager.FindByEmailAsync(email);
            if (other is not null && other.Id != id) return Results.Conflict(new { message = "Email đã được sử dụng." });

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var oldRoles = await manager.GetRolesAsync(user);
            if (user.IsActive && oldRoles.Contains(RoleNames.Admin) && (input.Status != "ACTIVE" || !input.Roles.Contains(RoleNames.Admin)))
            {
                var admins = await manager.GetUsersInRoleAsync(RoleNames.Admin);
                if (!admins.Any(x => x.Id != id && x.IsActive))
                    return Results.BadRequest(new { message = "Cần giữ ít nhất một Admin đang hoạt động." });
            }

            user.FullName = input.FullName.Trim();
            user.IsActive = input.Status == "ACTIVE";
            user.Email = email;
            user.UserName = email;
            var updated = await manager.UpdateAsync(user);
            if (!updated.Succeeded) return Results.BadRequest(new { message = string.Join("; ", updated.Errors.Select(x => x.Description)) });
            var remove = await manager.RemoveFromRolesAsync(user, oldRoles.Except(input.Roles));
            if (!remove.Succeeded) return Results.BadRequest(new { message = "Không thể gỡ vai trò." });
            var add = await manager.AddToRolesAsync(user, input.Roles.Except(oldRoles));
            if (!add.Succeeded) return Results.BadRequest(new { message = "Không thể gán vai trò." });
            await ReplaceScopesAsync(db, user.Id, input, Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!));
            var stampResult = await manager.UpdateSecurityStampAsync(user); // Revokes existing JWTs after role/status changes.
            if (!stampResult.Succeeded) return Results.BadRequest(new { message = "Không thể thu hồi phiên đăng nhập cũ." });
            await transaction.CommitAsync();
            return Results.Ok(ToView(user, input));
        }).WithName("UpdateUser");

        return app;
    }

    private static async Task<string?> ValidateAsync(AccountInput input, MealTraceDbContext db)
    {
        if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Length > 120) return "Họ tên không hợp lệ.";
        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || !input.Email.Contains('@')) return "Email không hợp lệ.";
        if (input.Roles is null || input.ClassIds is null || input.StudentIds is null) return "Thiếu danh sách vai trò hoặc phạm vi.";
        if (input.Roles.Distinct().Count() != input.Roles.Length || input.Roles.Any(x => !RoleNames.All.Contains(x))) return "Vai trò không hợp lệ.";
        if (input.Status is not ("ACTIVE" or "SUSPENDED")) return "Trạng thái không hợp lệ.";
        if (input.Roles.Length == 0 && input.InspectorAccessUntil is null) return "Cần vai trò hoặc grant thanh tra.";
        if (input.InspectorAccessUntil is not null && input.InspectorAccessUntil < DateOnly.FromDateTime(DateTime.UtcNow)) return "Grant thanh tra đã hết hạn.";
        if (input.Roles.Contains(RoleNames.Teacher) && input.ClassIds.Length == 0) return "Giáo viên cần được phân công lớp.";
        if (input.Roles.Contains(RoleNames.Parent) && input.StudentIds.Length == 0) return "Phụ huynh cần được liên kết học sinh.";
        if (input.ClassIds.Length > 0 && (!input.Roles.Contains(RoleNames.Teacher) || await db.Classes.CountAsync(x => input.ClassIds.Contains(x.Id)) != input.ClassIds.Distinct().Count())) return "Phạm vi lớp không hợp lệ.";
        if (input.StudentIds.Length > 0 && (!input.Roles.Contains(RoleNames.Parent) || await db.Students.CountAsync(x => input.StudentIds.Contains(x.Id) && x.IsActive) != input.StudentIds.Distinct().Count())) return "Phạm vi học sinh không hợp lệ.";
        return null;
    }

    private static async Task ReplaceScopesAsync(MealTraceDbContext db, Guid userId, AccountInput input, Guid adminId)
    {
        await db.TeacherAssignments.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await db.ParentStudents.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        db.TeacherAssignments.AddRange(input.ClassIds.Distinct().Select(classId => new TeacherAssignment { UserId = userId, ClassId = classId }));
        db.ParentStudents.AddRange(input.StudentIds.Distinct().Select(studentId => new ParentStudent { UserId = userId, StudentId = studentId }));
        var grant = await db.InspectorGrants.FindAsync(userId);
        if (input.InspectorAccessUntil is null && grant is not null) db.InspectorGrants.Remove(grant);
        else if (input.InspectorAccessUntil is not null)
        {
            if (grant is null) db.InspectorGrants.Add(new InspectorGrant { UserId = userId, ExpiresOn = input.InspectorAccessUntil.Value, GrantedById = adminId });
            else { grant.ExpiresOn = input.InspectorAccessUntil.Value; grant.GrantedById = adminId; grant.GrantedAt = DateTimeOffset.UtcNow; }
        }
        await db.SaveChangesAsync();
    }

    private static AccountView ToView(ApplicationUser user, AccountInput input) =>
        new(user.Id, user.FullName, user.Email!, input.Roles, user.IsActive ? "ACTIVE" : "SUSPENDED", input.ClassIds, input.StudentIds, input.InspectorAccessUntil);
}

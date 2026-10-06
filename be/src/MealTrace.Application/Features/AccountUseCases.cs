using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Dtos.Accounts;
using System.Security.Claims;
using System.Data;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features;
public static class AccountUseCases
{
    public static async Task<UseCaseResult> ListAccountsAsync(int? page, int? pageSize, Guid? classId, IMealTraceData db)
    {
        var number = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = db.Users.AsNoTracking(db.Queries).AsQueryable();
        if (classId is not null)
            query = query.Where(x => db.TeacherAssignments.Any(a => a.UserId == x.Id && a.ClassId == classId));
        var total = await query.CountAsync(db.Queries);
        var users = await query.OrderBy(x => x.Email).Skip((number - 1) * size).Take(size).ToListAsync(db.Queries);
        var ids = users.Select(x => x.Id).ToArray();
        var roleRows = await (
            from link in db.UserRoles.AsNoTracking(db.Queries)
            join role in db.Roles.AsNoTracking(db.Queries) on link.RoleId equals role.Id
            where ids.Contains(link.UserId)
            select new AccountRoleRow
            {

                UserId = link.UserId,

                Role = role.Name!

            }

        ).ToListAsync(db.Queries);
        var teacherRows = await db.TeacherAssignments.AsNoTracking(db.Queries).Where(x => ids.Contains(x.UserId)).ToListAsync(db.Queries);
        var parentRows = await db.ParentStudents.AsNoTracking(db.Queries).Where(x => ids.Contains(x.UserId)).ToListAsync(db.Queries);
        var grants = await db.InspectorGrants.AsNoTracking(db.Queries).Where(x => ids.Contains(x.UserId)).ToListAsync(db.Queries);
        var items = users.Select(user => new AccountView(user.Id, user.FullName, user.Email ?? "", roleRows.Where(x => x.UserId == user.Id).Select(x => x.Role).ToArray(), user.IsActive ? "ACTIVE" : "SUSPENDED", teacherRows.Where(x => x.UserId == user.Id).Select(x => x.ClassId).ToArray(), parentRows.Where(x => x.UserId == user.Id).Select(x => x.StudentId).ToArray(), grants.FirstOrDefault(x => x.UserId == user.Id)?.ExpiresOn, user.PhoneNumber)).ToArray();
        return UseCaseResult.Ok(new AccountListResponse
        {
            Items = items,
            Total = total,
            Page = number,
            PageSize = size
        });
    }

    public static async Task<UseCaseResult> GetAccountAsync(Guid id, IMealTraceData db, IIdentityService manager)
    {
        var user = await db.Users.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.Id == id);
        if (user is null)
            return UseCaseResult.NotFound();
        var roles = (await manager.GetRolesAsync(user)).ToArray();
        var classIds = await db.TeacherAssignments.AsNoTracking(db.Queries).Where(x => x.UserId == id).Select(x => x.ClassId).ToArrayAsync(db.Queries);
        var studentIds = await db.ParentStudents.AsNoTracking(db.Queries).Where(x => x.UserId == id).Select(x => x.StudentId).ToArrayAsync(db.Queries);
        var grant = await db.InspectorGrants.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.UserId == id);
        return UseCaseResult.Ok(new AccountView(user.Id, user.FullName, user.Email ?? "", roles, user.IsActive ? "ACTIVE" : "SUSPENDED", classIds, studentIds, grant?.ExpiresOn, user.PhoneNumber));
    }

    public static async Task<UseCaseResult> GetScopeOptionsAsync(string? search, Guid? classId, int? classPage, int? studentPage, string? selectedClassIds, string? selectedStudentIds, IMealTraceData db, TimeProvider clock)
    {
        var cp = Math.Clamp(classPage ?? 1, 1, 100000);
        var sp = Math.Clamp(studentPage ?? 1, 1, 100000);
        const int size = 25;
        if (!TryIds(selectedClassIds, out var classIds) || !TryIds(selectedStudentIds, out var studentIds))
            return UseCaseResult.BadRequest(new MessageResponse("Danh sách ID phạm vi không hợp lệ hoặc vượt quá 1000 mục."));
        var classes = db.Classes.AsNoTracking(db.Queries);
        var students = db.Students.AsNoTracking(db.Queries);
        var date = MealTrace.Domain.Time.SchoolTime.Today(clock.GetUtcNow());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            classes = classes.Where(x => x.Name.ToLower().Contains(term) || x.SchoolYear.Contains(term));
            students = students.Where(x => x.FullName.ToLower().Contains(term) || x.StudentCode.ToLower().Contains(term));
        }

        if (classId.HasValue)
            students = students.Where(x => db.Enrollments.Any(e => e.StudentId == x.Id && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate > date)));
        return UseCaseResult.Ok(new AccountScopeOptionsResponse
        {
            Classes = await classes.OrderBy(x => x.SchoolYear).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((cp - 1) * size).Take(size).Select(x => new ClassScopeOption
            {
                Id = x.Id,
                Name = x.Name + " · " + x.SchoolYear
            }).ToListAsync(db.Queries),
            Students = await students.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((sp - 1) * size).Take(size).Select(x => new StudentScopeOption
            {
                Id = x.Id,
                Name = x.StudentCode + " · " + x.FullName,
                ClassId = x.ClassId
            }).ToListAsync(db.Queries),
            SelectedClasses = await db.Classes.Where(x => classIds.Contains(x.Id)).Select(x => new ClassScopeOption
            {
                Id = x.Id,
                Name = x.Name + " · " + x.SchoolYear
            }).ToListAsync(db.Queries),
            SelectedStudents = await db.Students.Where(x => studentIds.Contains(x.Id)).Select(x => new StudentScopeOption
            {
                Id = x.Id,
                Name = x.StudentCode + " · " + x.FullName,
                ClassId = x.ClassId
            }).ToListAsync(db.Queries),
            ClassTotal = await classes.CountAsync(db.Queries),
            StudentTotal = await students.CountAsync(db.Queries),
            ClassPage = cp,
            StudentPage = sp,
            PageSize = size
        });
    }

    public static async Task<UseCaseResult> CreateAccountAsync(AccountInput input, ClaimsPrincipal principal, IMealTraceData db, IIdentityService manager)
    {
        var error = await ValidateAsync(input, db);
        if (error is not null)
            return UseCaseResult.BadRequest(new MessageResponse(error));
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        if (email is not null && await manager.FindByEmailAsync(email) is not null)
            return UseCaseResult.Conflict(new MessageResponse("Email đã được sử dụng."));
        if (phone is not null && await db.Users.AnyAsync(db.Queries, x => x.PhoneNumber == phone))
            return UseCaseResult.Conflict(new MessageResponse("SĐT đã được sử dụng."));
        var user = new IdentityAccount
        {

            Id = Guid.NewGuid(),

            UserName = email ?? phone,

            Email = email,

            PhoneNumber = phone,

            EmailConfirmed = false,

            FullName = input.FullName.Trim(),

            IsActive = input.Status == "ACTIVE",


        };
        var temporaryPassword = TemporaryPassword.Generate();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable);
        var created = await manager.CreateAsync(user, temporaryPassword);
        if (!created.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse(string.Join("; ", created.Errors.Select(x => x.Description))));
        var roleResult = await manager.AddToRolesAsync(user, input.Roles);
        if (!roleResult.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse(string.Join("; ", roleResult.Errors.Select(x => x.Description))));
        await ReplaceScopesAsync(db, user.Id, input, Guid.Parse(principal.FindFirst("sub")?.Value!));
        await transaction.CommitAsync();
        return UseCaseResult.Created($"/api/admin/users/{user.Id}", new AccountCreatedResponse(ToView(user, input), temporaryPassword, "Mật khẩu tạm chỉ hiển thị một lần. Hãy chuyển cho người dùng qua kênh an toàn."));
    }

    public static async Task<UseCaseResult> UpdateAccountAsync(Guid id, AccountInput input, ClaimsPrincipal principal, IMealTraceData db, IIdentityService manager)
    {
        var error = await ValidateAsync(input, db);
        if (error is not null)
            return UseCaseResult.BadRequest(new MessageResponse(error));
        var user = await manager.FindByIdAsync(id.ToString());
        if (user is null)
            return UseCaseResult.NotFound();
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        var other = email is null ? null : await manager.FindByEmailAsync(email);
        if (other is not null && other.Id != id)
            return UseCaseResult.Conflict(new MessageResponse("Email đã được sử dụng."));
        if (phone is not null && await db.Users.AnyAsync(db.Queries, x => x.PhoneNumber == phone && x.Id != id))
            return UseCaseResult.Conflict(new MessageResponse("SĐT đã được sử dụng."));
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable);
        var oldRoles = await manager.GetRolesAsync(user);
        if (user.IsActive && oldRoles.Contains(RoleNames.Admin) && (input.Status != "ACTIVE" || !input.Roles.Contains(RoleNames.Admin)))
        {
            var admins = await manager.GetUsersInRoleAsync(RoleNames.Admin);
            if (!admins.Any(x => x.Id != id && x.IsActive))
                return UseCaseResult.BadRequest(new MessageResponse("Cần giữ ít nhất một Admin đang hoạt động."));
        }

        user.FullName = input.FullName.Trim();
        user.IsActive = input.Status == "ACTIVE";
        user.Email = email;
        user.UserName = email ?? phone;
        user.PhoneNumber = phone;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        var updated = await manager.UpdateAsync(user);
        if (!updated.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse(string.Join("; ", updated.Errors.Select(x => x.Description))));
        var remove = await manager.RemoveFromRolesAsync(user, oldRoles.Except(input.Roles));
        if (!remove.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse("Không thể gỡ vai trò."));
        var add = await manager.AddToRolesAsync(user, input.Roles.Except(oldRoles));
        if (!add.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse("Không thể gán vai trò."));
        await ReplaceScopesAsync(db, user.Id, input, Guid.Parse(principal.FindFirst("sub")?.Value!));
        var stampResult = await manager.UpdateSecurityStampAsync(user); // Revokes existing JWTs after role/status changes.
        if (!stampResult.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse("Không thể thu hồi phiên đăng nhập cũ."));
        await transaction.CommitAsync();
        return UseCaseResult.Ok(ToView(user, input));
    }

    public static async Task<UseCaseResult> ResetPasswordAsync(Guid id, ResetPasswordInput input, ClaimsPrincipal principal, IMealTraceData db, IIdentityService manager)
    {
        var actorId = Guid.Parse(principal.FindFirst("sub")?.Value!);
        if (id == actorId)
            return UseCaseResult.BadRequest(new MessageResponse("Đổi mật khẩu của chính bạn tại Hồ sơ của tôi."));
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return UseCaseResult.BadRequest(new MessageResponse("Cần lý do đặt lại mật khẩu, tối đa 500 ký tự."));
        await using var transaction = await db.BeginTransactionAsync();
        var user = await manager.FindByIdAsync(id.ToString());
        if (user is null)
            return UseCaseResult.NotFound(new MessageResponse("Không tìm thấy tài khoản."));
        var temporaryPassword = TemporaryPassword.Generate();
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        // Identity hashes the password and changes SecurityStamp, revoking old JWTs.
        var result = await manager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!result.Succeeded)
            return UseCaseResult.Conflict(new MessageResponse("Không thể đặt lại mật khẩu. Hãy tải lại và thử lại."));
        var unlock = await manager.SetLockoutEndDateAsync(user, null);
        var clearFailures = await manager.ResetAccessFailedCountAsync(user);
        if (!unlock.Succeeded || !clearFailures.Succeeded)
            return UseCaseResult.Conflict(new MessageResponse("Không thể hoàn tất khôi phục tài khoản. Hãy thử lại."));
        db.AccountPasswordResetAudits.Add(new AccountPasswordResetAudit
        {
            UserId = user.Id,
            PerformedByUserId = actorId,
            Reason = reason
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return UseCaseResult.Ok(new PasswordResetResponse(user.Id, user.FullName, user.PhoneNumber, user.Email, temporaryPassword, "Mật khẩu tạm chỉ hiển thị một lần. Các phiên đăng nhập cũ đã được thu hồi.", user.IsActive));
    }

    private static async Task<string?> ValidateAsync(AccountInput input, IMealTraceData db)
    {
        if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Length > 120)
            return "Họ tên không hợp lệ.";
        if (string.IsNullOrWhiteSpace(input.Email) && string.IsNullOrWhiteSpace(input.PhoneNumber))
            return "Cần SĐT hoặc email đăng nhập.";
        if (!string.IsNullOrWhiteSpace(input.Email) && (input.Email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(input.Email.Trim(), out var parsed) || parsed.Address != input.Email.Trim()))
            return "Email không hợp lệ.";
        if (!string.IsNullOrWhiteSpace(input.PhoneNumber) && PhoneNumbers.Normalize(input.PhoneNumber) is null)
            return "SĐT không hợp lệ (10 số, bắt đầu bằng 0 hoặc +84).";
        if (input.Roles is null || input.ClassIds is null || input.StudentIds is null)
            return "Thiếu danh sách vai trò hoặc phạm vi.";
        if (input.Roles.Distinct().Count() != input.Roles.Length || input.Roles.Any(x => !RoleNames.All.Contains(x)))
            return "Vai trò không hợp lệ.";
        if (input.Status is not ("ACTIVE" or "SUSPENDED"))
            return "Trạng thái không hợp lệ.";
        if (input.Roles.Length == 0 && input.InspectorAccessUntil is null)
            return "Cần vai trò hoặc grant thanh tra.";
        if (input.InspectorAccessUntil is not null && input.InspectorAccessUntil < DateOnly.FromDateTime(DateTime.UtcNow))
            return "Grant thanh tra đã hết hạn.";
        if (input.Roles.Contains(RoleNames.Teacher) && input.ClassIds.Length == 0)
            return "Giáo viên cần được phân công lớp.";
        if (input.Roles.Contains(RoleNames.Parent) && input.StudentIds.Length == 0)
            return "Phụ huynh cần được liên kết học sinh.";
        if (input.ClassIds.Length > 0 && (!input.Roles.Contains(RoleNames.Teacher) || await db.Classes.CountAsync(db.Queries, x => input.ClassIds.Contains(x.Id)) != input.ClassIds.Distinct().Count()))
            return "Phạm vi lớp không hợp lệ.";
        if (input.StudentIds.Length > 0 && (!input.Roles.Contains(RoleNames.Parent) || await db.Students.CountAsync(db.Queries, x => input.StudentIds.Contains(x.Id)) != input.StudentIds.Distinct().Count()))
            return "Phạm vi học sinh không hợp lệ.";
        return null;
    }

    private static async Task ReplaceScopesAsync(IMealTraceData db, Guid userId, AccountInput input, Guid adminId)
    {
        await db.TeacherAssignments.Where(x => x.UserId == userId).ExecuteDeleteAsync(db.Queries);
        await db.ParentStudents.Where(x => x.UserId == userId).ExecuteDeleteAsync(db.Queries);
        db.TeacherAssignments.AddRange(input.ClassIds.Distinct().Select(classId => new TeacherAssignment
        {
            UserId = userId,
            ClassId = classId
        }));
        db.ParentStudents.AddRange(input.StudentIds.Distinct().Select(studentId => new ParentStudent
        {
            UserId = userId,
            StudentId = studentId
        }));
        var grant = await db.InspectorGrants.FindAsync(userId);
        if (input.InspectorAccessUntil is null && grant is not null)
            db.InspectorGrants.Remove(grant);
        else if (input.InspectorAccessUntil is not null)
        {
            if (grant is null)
                db.InspectorGrants.Add(new InspectorGrant
                {
                    UserId = userId,
                    ExpiresOn = input.InspectorAccessUntil.Value,
                    GrantedById = adminId
                });
            else
            {
                grant.ExpiresOn = input.InspectorAccessUntil.Value;
                grant.GrantedById = adminId;
                grant.GrantedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }

    private static bool TryIds(string? value, out Guid[] ids)
    {
        var parts = (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        ids = [];
        if (parts.Length > 1000 || parts.Any(x => !Guid.TryParse(x, out _)))
            return false;
        ids = parts.Select(Guid.Parse).Distinct().ToArray();
        return true;
    }

    private static AccountView ToView(IdentityAccount user, AccountInput input) => new(user.Id, user.FullName, user.Email ?? "", input.Roles, user.IsActive ? "ACTIVE" : "SUSPENDED", input.ClassIds, input.StudentIds, input.InspectorAccessUntil, user.PhoneNumber);
}

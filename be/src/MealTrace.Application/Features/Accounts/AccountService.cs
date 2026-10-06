using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Identity;
using MealTrace.Application.Dtos.Accounts;
using System.Data;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Accounts;
public sealed class AccountService(IAccountRepository repository, IIdentityService manager, TimeProvider clock, ICurrentActor currentActor, IUnitOfWork unitOfWork)
{
    public async Task<Result<AccountListResponse>> ListAccountsAsync(int? page, int? pageSize, Guid? classId)
    {
        var number = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var total = await repository.CountAccountsAsync(classId);
        var users = await repository.ListAccountsAsync(classId, number, size);
        var ids = users.Select(x => x.Id).ToArray();
        var roleRows = await repository.ListAccountRolesAsync(ids);
        var teacherRows = await repository.ListTeacherAssignmentsAsync(ids);
        var parentRows = await repository.ListParentLinksAsync(ids);
        var grants = await repository.ListInspectorGrantsAsync(ids);
        var items = users.Select(user => new AccountView(user.Id, user.FullName, user.Email ?? "", roleRows.Where(x => x.UserId == user.Id).Select(x => x.Role).ToArray(), user.IsActive ? "ACTIVE" : "SUSPENDED", teacherRows.Where(x => x.UserId == user.Id).Select(x => x.ClassId).ToArray(), parentRows.Where(x => x.UserId == user.Id).Select(x => x.StudentId).ToArray(), grants.FirstOrDefault(x => x.UserId == user.Id)?.ExpiresOn, user.PhoneNumber)).ToArray();
        return Result.Success(new AccountListResponse
        {
            Items = items,
            Total = total,
            Page = number,
            PageSize = size
        });
    }

    public async Task<Result<AccountView>> GetAccountAsync(Guid id)
    {
        var user = await repository.FindAccountAsync(id);
        if (user is null)
            return Result.NotFound();
        var roles = (await manager.GetRolesAsync(user)).ToArray();
        var classIds = await repository.ListAssignedClassIdsAsync(id);
        var studentIds = await repository.ListLinkedStudentIdsAsync(id);
        var grant = await repository.FindInspectorGrantAsync(id);
        return Result.Success(new AccountView(user.Id, user.FullName, user.Email ?? "", roles, user.IsActive ? "ACTIVE" : "SUSPENDED", classIds, studentIds, grant?.ExpiresOn, user.PhoneNumber));
    }

    public async Task<Result<AccountScopeOptionsResponse>> GetScopeOptionsAsync(string? search, Guid? classId, int? classPage, int? studentPage, string? selectedClassIds, string? selectedStudentIds)
    {
        var cp = Math.Clamp(classPage ?? 1, 1, 100000);
        var sp = Math.Clamp(studentPage ?? 1, 1, 100000);
        const int size = 25;
        if (!TryIds(selectedClassIds, out var classIds) || !TryIds(selectedStudentIds, out var studentIds))
            return Result.Invalid("Danh sách ID phạm vi không hợp lệ hoặc vượt quá 1000 mục.");
        var date = MealTrace.Domain.Time.SchoolTime.Today(clock.GetUtcNow());
        return Result.Success(new AccountScopeOptionsResponse
        {
            Classes = await repository.SearchClassOptionsAsync(search, classId, date, cp, size),
            Students = await repository.SearchStudentOptionsAsync(search, classId, date, sp, size),
            SelectedClasses = await repository.ListSelectedClassOptionsAsync(classIds),
            SelectedStudents = await repository.ListSelectedStudentOptionsAsync(studentIds),
            ClassTotal = await repository.CountClassOptionsAsync(search, classId, date),
            StudentTotal = await repository.CountStudentOptionsAsync(search, classId, date),
            ClassPage = cp,
            StudentPage = sp,
            PageSize = size
        });
    }

    public async Task<Result<AccountCreatedResponse>> CreateAccountAsync(AccountInput input)
    {
        var error = await ValidateAsync(input);
        if (error is not null)
            return Result.Invalid(error);
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        if (email is not null && await manager.FindByEmailAsync(email) is not null)
            return Result.Conflict("Email đã được sử dụng.");
        if (phone is not null && await repository.PhoneExistsAsync(phone))
            return Result.Conflict("SĐT đã được sử dụng.");
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
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var created = await manager.CreateAsync(user, temporaryPassword);
        if (!created.Succeeded)
            return Result.Invalid(string.Join("; ", created.Errors.Select(x => x.Description)));
        var roleResult = await manager.AddToRolesAsync(user, input.Roles);
        if (!roleResult.Succeeded)
            return Result.Invalid(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
        await ReplaceScopesAsync(user.Id, input, currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required."));
        await transaction.CommitAsync();
        return Result.Success(new AccountCreatedResponse(ToView(user, input), temporaryPassword, "Mật khẩu tạm chỉ hiển thị một lần. Hãy chuyển cho người dùng qua kênh an toàn."));
    }

    public async Task<Result<AccountView>> UpdateAccountAsync(Guid id, AccountInput input)
    {
        var error = await ValidateAsync(input);
        if (error is not null)
            return Result.Invalid(error);
        var user = await manager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.NotFound();
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        var other = email is null ? null : await manager.FindByEmailAsync(email);
        if (other is not null && other.Id != id)
            return Result.Conflict("Email đã được sử dụng.");
        if (phone is not null && await repository.PhoneUsedByOtherAccountAsync(phone, id))
            return Result.Conflict("SĐT đã được sử dụng.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        var oldRoles = await manager.GetRolesAsync(user);
        if (user.IsActive && oldRoles.Contains(RoleNames.Admin) && (input.Status != "ACTIVE" || !input.Roles.Contains(RoleNames.Admin)))
        {
            var admins = await manager.GetUsersInRoleAsync(RoleNames.Admin);
            if (!admins.Any(x => x.Id != id && x.IsActive))
                return Result.Invalid("Cần giữ ít nhất một Admin đang hoạt động.");
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
            return Result.Invalid(string.Join("; ", updated.Errors.Select(x => x.Description)));
        var remove = await manager.RemoveFromRolesAsync(user, oldRoles.Except(input.Roles));
        if (!remove.Succeeded)
            return Result.Invalid("Không thể gỡ vai trò.");
        var add = await manager.AddToRolesAsync(user, input.Roles.Except(oldRoles));
        if (!add.Succeeded)
            return Result.Invalid("Không thể gán vai trò.");
        await ReplaceScopesAsync(user.Id, input, currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required."));
        var stampResult = await manager.UpdateSecurityStampAsync(user); // Revokes existing JWTs after role/status changes.
        if (!stampResult.Succeeded)
            return Result.Invalid("Không thể thu hồi phiên đăng nhập cũ.");
        await transaction.CommitAsync();
        return Result.Success(ToView(user, input));
    }

    public async Task<Result<PasswordResetResponse>> ResetPasswordAsync(Guid id, ResetPasswordInput input)
    {
        var actorId = currentActor.UserId ?? throw new InvalidOperationException("An authenticated actor is required.");
        if (id == actorId)
            return Result.Invalid("Đổi mật khẩu của chính bạn tại Hồ sơ của tôi.");
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return Result.Invalid("Cần lý do đặt lại mật khẩu, tối đa 500 ký tự.");
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var user = await manager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.NotFound("Không tìm thấy tài khoản.");
        var temporaryPassword = TemporaryPassword.Generate();
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        // Identity hashes the password and changes SecurityStamp, revoking old JWTs.
        var result = await manager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!result.Succeeded)
            return Result.Conflict("Không thể đặt lại mật khẩu. Hãy tải lại và thử lại.");
        var unlock = await manager.SetLockoutEndDateAsync(user, null);
        var clearFailures = await manager.ResetAccessFailedCountAsync(user);
        if (!unlock.Succeeded || !clearFailures.Succeeded)
            return Result.Conflict("Không thể hoàn tất khôi phục tài khoản. Hãy thử lại.");
        repository.AddAccountPasswordResetAudit(new AccountPasswordResetAudit
        {
            UserId = user.Id,
            PerformedByUserId = actorId,
            Reason = reason
        });
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(new PasswordResetResponse(user.Id, user.FullName, user.PhoneNumber, user.Email, temporaryPassword, "Mật khẩu tạm chỉ hiển thị một lần. Các phiên đăng nhập cũ đã được thu hồi.", user.IsActive));
    }

    private async Task<string?> ValidateAsync(AccountInput input)
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
        if (input.ClassIds.Length > 0 && (!input.Roles.Contains(RoleNames.Teacher) || await repository.CountExistingClassesAsync(input) != input.ClassIds.Distinct().Count()))
            return "Phạm vi lớp không hợp lệ.";
        if (input.StudentIds.Length > 0 && (!input.Roles.Contains(RoleNames.Parent) || await repository.CountExistingStudentsAsync(input) != input.StudentIds.Distinct().Count()))
            return "Phạm vi học sinh không hợp lệ.";
        return null;
    }

    private async Task ReplaceScopesAsync(Guid userId, AccountInput input, Guid adminId)
    {
        await repository.DeleteTeacherAssignmentsAsync(userId);
        await repository.DeleteParentLinksAsync(userId);
        repository.AddTeacherAssignments(input.ClassIds.Distinct().Select(classId => new TeacherAssignment
        {
            UserId = userId,
            ClassId = classId
        }));
        repository.AddParentStudents(input.StudentIds.Distinct().Select(studentId => new ParentStudent
        {
            UserId = userId,
            StudentId = studentId
        }));
        var grant = await repository.FindTrackedInspectorGrantAsync(userId);
        if (input.InspectorAccessUntil is null && grant is not null)
            repository.RemoveInspectorGrant(grant);
        else if (input.InspectorAccessUntil is not null)
        {
            if (grant is null)
                repository.AddInspectorGrant(new InspectorGrant
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

        await unitOfWork.SaveChangesAsync();
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

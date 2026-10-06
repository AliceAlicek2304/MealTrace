using MealTrace.Application.Dtos.Responses;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Dtos.Auth;
using System.Security.Claims;
using MealTrace.Domain.Entities;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features;
public static class AuthUseCases
{
    public static async Task<UseCaseResult> LoginAsync(LoginRequest request, IMealTraceData db, IIdentityService users, IAccessTokenService tokens)
    {
        var identifier = request.Identifier ?? request.Email;
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
            return UseCaseResult.BadRequest(new MessageResponse("SĐT hoặc email và mật khẩu là bắt buộc."));
        var phone = PhoneNumbers.Normalize(identifier);
        var user = phone is not null ? await db.Users.SingleOrDefaultAsync(db.Queries, x => x.PhoneNumber == phone) : identifier.Contains('@') ? await users.FindByEmailAsync(identifier.Trim()) : null;
        if (user is null || !user.IsActive)
            return UseCaseResult.Unauthorized();
        var result = await users.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return UseCaseResult.Unauthorized();
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var grant = await db.InspectorGrants.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.UserId == user.Id);
        if (roles.Length == 0 && (grant is null || grant.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow)))
            return UseCaseResult.Unauthorized();
        var (token, expiresAt) = await tokens.CreateAsync(user);
        return UseCaseResult.Ok(new LoginResponse(token, expiresAt, new CurrentUser(user.Id, user.FullName, user.Email ?? "", roles, grant?.ExpiresOn, user.PhoneNumber)));
    }

    public static async Task<UseCaseResult> GetCurrentUserAsync(ClaimsPrincipal principal, IMealTraceData db, IIdentityService users)
    {
        var id = principal.FindFirst("sub")?.Value;
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null || !user.IsActive)
            return UseCaseResult.Unauthorized();
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var grant = await db.InspectorGrants.AsNoTracking(db.Queries).FirstOrDefaultAsync(db.Queries, x => x.UserId == user.Id);
        return UseCaseResult.Ok(new CurrentUser(user.Id, user.FullName, user.Email ?? "", roles, grant?.ExpiresOn, user.PhoneNumber));
    }

    public static async Task<UseCaseResult> ChangePasswordAsync(ChangePasswordRequest request, ClaimsPrincipal principal, IIdentityService users)
    {
        var id = principal.FindFirst("sub")?.Value;
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null || !user.IsActive)
            return UseCaseResult.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return UseCaseResult.BadRequest(new MessageResponse("Cần nhập mật khẩu hiện tại và mật khẩu mới."));
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return UseCaseResult.BadRequest(new MessageResponse(string.Join("; ", result.Errors.Select(x => x.Description))));
        return UseCaseResult.NoContent();
    }

    public static async Task<UseCaseResult> LogoutAsync(ClaimsPrincipal principal, IIdentityService users)
    {
        var id = principal.FindFirst("sub")?.Value;
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null)
            return UseCaseResult.Unauthorized();
        var result = await users.UpdateSecurityStampAsync(user);
        return result.Succeeded ? UseCaseResult.NoContent() : UseCaseResult.Problem("Không thể kết thúc phiên đăng nhập.");
    }
}

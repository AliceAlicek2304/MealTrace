using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Auth;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Auth;
public sealed class AuthUseCases(IAuthRepository repository, IIdentityService users, IAccessTokenService tokens, ICurrentActor currentActor)
{
    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var identifier = request.Identifier ?? request.Email;
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
            return Result.Invalid("SĐT hoặc email và mật khẩu là bắt buộc.");
        var phone = PhoneNumbers.Normalize(identifier);
        var user = phone is not null ? await repository.FindAccountByPhoneAsync(phone) : identifier.Contains('@') ? await users.FindByEmailAsync(identifier.Trim()) : null;
        if (user is null || !user.IsActive)
            return Result.Unauthenticated();
        var result = await users.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Result.Unauthenticated();
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var grant = await repository.FindInspectorGrantAsync(user);
        if (roles.Length == 0 && (grant is null || grant.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow)))
            return Result.Unauthenticated();
        var (token, expiresAt) = await tokens.CreateAsync(user);
        return Result.Success(new LoginResponse(token, expiresAt, new CurrentUser(user.Id, user.FullName, user.Email ?? "", roles, grant?.ExpiresOn, user.PhoneNumber)));
    }

    public async Task<Result<CurrentUser>> GetCurrentUserAsync()
    {
        var id = currentActor.UserId?.ToString();
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null || !user.IsActive)
            return Result.Unauthenticated();
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var grant = await repository.FindInspectorGrantAsync(user);
        return Result.Success(new CurrentUser(user.Id, user.FullName, user.Email ?? "", roles, grant?.ExpiresOn, user.PhoneNumber));
    }

    public async Task<Result<Unit>> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var id = currentActor.UserId?.ToString();
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null || !user.IsActive)
            return Result.Unauthenticated();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return Result.Invalid("Cần nhập mật khẩu hiện tại và mật khẩu mới.");
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return Result.Invalid(string.Join("; ", result.Errors.Select(x => x.Description)));
        return Result.Success(Unit.Value);
    }

    public async Task<Result<Unit>> LogoutAsync()
    {
        var id = currentActor.UserId?.ToString();
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null)
            return Result.Unauthenticated();
        var result = await users.UpdateSecurityStampAsync(user);
        return result.Succeeded ? Result.Success(Unit.Value) : Result.Unexpected("Không thể kết thúc phiên đăng nhập.");
    }
}

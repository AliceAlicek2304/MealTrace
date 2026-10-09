using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Auth;
using MealTrace.Application.Abstractions;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Identity;
using System.Data;

namespace MealTrace.Application.Features.Auth;
public sealed class AuthService(IAuthRepository repository, IIdentityService users, IAccessTokenService tokens, ICurrentActor currentActor, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<Unit>> RegisterParentAsync(RegisterParentRequest request)
    {
        var name = request.FullName?.Trim();
        var phone = PhoneNumbers.Normalize(request.PhoneNumber);
        var password = request.Password;
        if (string.IsNullOrWhiteSpace(name) || name.Length is < 2 or > 120 || name.Any(char.IsControl))
            return Result.Invalid("Họ tên cần từ 2 đến 120 ký tự.");
        if (phone is null)
            return Result.Invalid("Số điện thoại Việt Nam không hợp lệ.");
        if (password is null || password.Length is < 12 or > 128 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(c => !char.IsLetterOrDigit(c)))
            return Result.Invalid("Mật khẩu cần 12–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await repository.FindAccountByPhoneAsync(phone) is not null)
            return Result.Conflict("SĐT đã có tài khoản. Vui lòng đăng nhập hoặc liên hệ nhà trường nếu quên mật khẩu.");
        var otp = await repository.FindSignupOtpAsync(phone);
        if (otp is null || request.ChallengeId != otp.ChallengeId || otp.Used || otp.ExpiresAt <= clock.GetUtcNow() || otp.FailedAttempts >= 5)
            return Result.Invalid("OTP không hợp lệ, đã hết hạn hoặc đã dùng. Vui lòng yêu cầu mã mới.");
        if (request.OtpCode is null || request.OtpCode.Length != 6 || !request.OtpCode.All(char.IsAsciiDigit) || !ParentSignupOtpService.Matches(otp, request.OtpCode))
        {
            otp.FailedAttempts++;
            await unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Invalid("OTP không đúng. Mỗi mã chỉ được thử tối đa 5 lần.");
        }
        var account = new IdentityAccount
        {
            Id = Guid.NewGuid(), FullName = name, UserName = phone,
            PhoneNumber = phone, PhoneNumberConfirmed = true,
            EmailConfirmed = false, IsActive = true
        };
        var created = await users.CreateAsync(account, password);
        if (!created.Succeeded)
            return Result.Invalid("Không thể tạo tài khoản. Kiểm tra SĐT và yêu cầu mật khẩu.");
        var role = await users.AddToRoleAsync(account, RoleNames.Parent);
        if (!role.Succeeded)
            return Result.Unexpected("Không thể hoàn tất đăng ký. Vui lòng thử lại sau.");
        otp.Used = true;
        otp.CodeHash = "";
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success(Unit.Value);
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var identifier = request.Identifier;
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

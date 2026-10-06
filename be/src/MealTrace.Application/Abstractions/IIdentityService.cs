using MealTrace.Application.Dtos.Identity;

namespace MealTrace.Application.Abstractions;

public interface IIdentityService
{
    Task<IdentityAccount?> FindByIdAsync(string id);
    Task<IdentityAccount?> FindByEmailAsync(string email);
    Task<IList<string>> GetRolesAsync(IdentityAccount user);
    Task<IList<IdentityAccount>> GetUsersInRoleAsync(string role);
    Task<bool> IsInRoleAsync(IdentityAccount user, string role);
    Task<IdentityOperation> CreateAsync(IdentityAccount user, string password);
    Task<IdentityOperation> UpdateAsync(IdentityAccount user);
    Task<IdentityOperation> AddToRolesAsync(IdentityAccount user, IEnumerable<string> roles);
    Task<IdentityOperation> AddToRoleAsync(IdentityAccount user, string role);
    Task<IdentityOperation> RemoveFromRolesAsync(IdentityAccount user, IEnumerable<string> roles);
    Task<IdentityOperation> UpdateSecurityStampAsync(IdentityAccount user);
    Task<IdentityOperation> ChangePasswordAsync(IdentityAccount user, string currentPassword, string newPassword);
    Task<string> GeneratePasswordResetTokenAsync(IdentityAccount user);
    Task<IdentityOperation> ResetPasswordAsync(IdentityAccount user, string token, string newPassword);
    Task<IdentityOperation> SetLockoutEndDateAsync(IdentityAccount user, DateTimeOffset? endDate);
    Task<IdentityOperation> ResetAccessFailedCountAsync(IdentityAccount user);
    Task<IdentityOperation> CheckPasswordSignInAsync(IdentityAccount user, string password, bool lockoutOnFailure);
}

public interface IAccessTokenService
{
    Task<(string Token, DateTimeOffset ExpiresAt)> CreateAsync(IdentityAccount user);
}

using MealTrace.Infrastructure.Persistence;
using MealTrace.Application.Dtos.Identity;
using System.Linq.Expressions;
using MealTrace.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace MealTrace.Infrastructure.Identity;
public sealed class IdentityService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : IIdentityService
{
    internal static readonly Expression<Func<ApplicationUser, IdentityAccount>> AccountProjection = user => new IdentityAccount
    {
        Id = user.Id,
        FullName = user.FullName,
        UserName = user.UserName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        EmailConfirmed = user.EmailConfirmed,
        PhoneNumberConfirmed = user.PhoneNumberConfirmed,
        IsActive = user.IsActive
    };
    private static IdentityAccount Map(ApplicationUser user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        UserName = user.UserName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        EmailConfirmed = user.EmailConfirmed,
        PhoneNumberConfirmed = user.PhoneNumberConfirmed,
        IsActive = user.IsActive
    };
    private static IdentityOperation Map(IdentityResult result) => new(result.Succeeded, result.Errors.Select(error => new IdentityFailure(error.Description)).ToArray());
    // Translate provider errors after Identity's store has handled its own concurrency failures.
    private async Task<ApplicationUser> RequireUser(IdentityAccount account) => await users.FindByIdAsync(account.Id.ToString()) ?? throw new InvalidOperationException("Account no longer exists.");
    public async Task<IdentityAccount?> FindByIdAsync(string id)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await users.FindByIdAsync(id) is { } user ? Map(user) : null;
        });
    }

    public async Task<IdentityAccount?> FindByEmailAsync(string email)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await users.FindByEmailAsync(email) is { } user ? Map(user) : null;
        });
    }

    public async Task<IList<string>> GetRolesAsync(IdentityAccount user)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await users.GetRolesAsync(await RequireUser(user));
        });
    }

    public async Task<IList<IdentityAccount>> GetUsersInRoleAsync(string role)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return (await users.GetUsersInRoleAsync(role)).Select(Map).ToList();
        });
    }

    public async Task<bool> IsInRoleAsync(IdentityAccount user, string role)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await users.IsInRoleAsync(await RequireUser(user), role);
        });
    }

    public async Task<IdentityOperation> CreateAsync(IdentityAccount account, string password)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.CreateAsync(new ApplicationUser { Id = account.Id, FullName = account.FullName, UserName = account.UserName, Email = account.Email, PhoneNumber = account.PhoneNumber, EmailConfirmed = account.EmailConfirmed, PhoneNumberConfirmed = account.PhoneNumberConfirmed, IsActive = account.IsActive }, password));
        });
    }

    public async Task<IdentityOperation> UpdateAsync(IdentityAccount account)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var user = await RequireUser(account);
            user.FullName = account.FullName;
            user.UserName = account.UserName;
            user.Email = account.Email;
            user.PhoneNumber = account.PhoneNumber;
            user.EmailConfirmed = account.EmailConfirmed;
            user.PhoneNumberConfirmed = account.PhoneNumberConfirmed;
            user.IsActive = account.IsActive;
            return Map(await users.UpdateAsync(user));
        });
    }

    public async Task<IdentityOperation> AddToRolesAsync(IdentityAccount user, IEnumerable<string> roles)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.AddToRolesAsync(await RequireUser(user), roles));
        });
    }

    public async Task<IdentityOperation> AddToRoleAsync(IdentityAccount user, string role)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.AddToRoleAsync(await RequireUser(user), role));
        });
    }

    public async Task<IdentityOperation> RemoveFromRolesAsync(IdentityAccount user, IEnumerable<string> roles)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.RemoveFromRolesAsync(await RequireUser(user), roles));
        });
    }

    public async Task<IdentityOperation> UpdateSecurityStampAsync(IdentityAccount user)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.UpdateSecurityStampAsync(await RequireUser(user)));
        });
    }

    public async Task<IdentityOperation> ChangePasswordAsync(IdentityAccount user, string currentPassword, string newPassword)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.ChangePasswordAsync(await RequireUser(user), currentPassword, newPassword));
        });
    }

    public async Task<string> GeneratePasswordResetTokenAsync(IdentityAccount user)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await users.GeneratePasswordResetTokenAsync(await RequireUser(user));
        });
    }

    public async Task<IdentityOperation> ResetPasswordAsync(IdentityAccount user, string token, string newPassword)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.ResetPasswordAsync(await RequireUser(user), token, newPassword));
        });
    }

    public async Task<IdentityOperation> SetLockoutEndDateAsync(IdentityAccount user, DateTimeOffset? endDate)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.SetLockoutEndDateAsync(await RequireUser(user), endDate));
        });
    }

    public async Task<IdentityOperation> ResetAccessFailedCountAsync(IdentityAccount user)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return Map(await users.ResetAccessFailedCountAsync(await RequireUser(user)));
        });
    }

    public async Task<IdentityOperation> CheckPasswordSignInAsync(IdentityAccount user, string password, bool lockoutOnFailure)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var result = await signIn.CheckPasswordSignInAsync(await RequireUser(user), password, lockoutOnFailure);
            return new IdentityOperation(result.Succeeded, []);
        });
    }
}

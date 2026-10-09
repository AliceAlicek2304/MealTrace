using MealTrace.Application.Dtos.Identity;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MealTrace.Infrastructure.Identity;
using MealTrace.Application.Abstractions.Repositories;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class AuthRepository(MealTraceDbContext db) : IAuthRepository
{
    public Task<ParentSignupOtp?> FindSignupOtpAsync(string phone) =>
        PersistenceErrors.ExecuteAsync(() => db.ParentSignupOtps.SingleOrDefaultAsync(x => x.PhoneNumber == phone));
    public void AddSignupOtp(ParentSignupOtp otp) => db.ParentSignupOtps.Add(otp);
    public async Task<IdentityAccount?> FindAccountByPhoneAsync(string? phone)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Users.Select(IdentityService.AccountProjection).SingleOrDefaultAsync(x => x.PhoneNumber == phone);
        });
    }
    public async Task<InspectorGrant?> FindInspectorGrantAsync(IdentityAccount user)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.InspectorGrants.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
        });
    }
}

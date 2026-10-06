using MealTrace.Application.Dtos.Identity;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for this module. Mutation methods do not commit; the use case owns the unit of work.</summary>
public interface IAuthRepository
{
    Task<IdentityAccount?> FindAccountByPhoneAsync(string? phone);
    Task<InspectorGrant?> FindInspectorGrantAsync(IdentityAccount user);
}

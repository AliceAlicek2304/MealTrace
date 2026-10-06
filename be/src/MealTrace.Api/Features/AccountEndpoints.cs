using MealTrace.Application.Dtos.Responses;
using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Accounts;

namespace MealTrace.Api.Features;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Account administration")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        group.MapGet("/users", async (int? page, int? pageSize, Guid? classId, IMealTraceData db) => (await AccountUseCases.ListAccountsAsync(page, pageSize, classId, db)).ToHttpResult()).Produces<AccountListResponse>(StatusCodes.Status200OK).WithName("ListUsers");

        group.MapGet("/users/{id:guid}", async (Guid id, IMealTraceData db, IIdentityService manager) => (await AccountUseCases.GetAccountAsync(id, db, manager)).ToHttpResult()).Produces<AccountView>().WithName("GetUser");

        group.MapGet("/scope-options", async (string? search, Guid? classId, int? classPage, int? studentPage,
            string? selectedClassIds, string? selectedStudentIds, IMealTraceData db, TimeProvider clock) => (await AccountUseCases.GetScopeOptionsAsync(search, classId, classPage, studentPage, selectedClassIds, selectedStudentIds, db, clock)).ToHttpResult()).Produces<AccountScopeOptionsResponse>(StatusCodes.Status200OK).WithName("GetScopeOptions");

        group.MapPost("/users", async (AccountInput input, ClaimsPrincipal principal, IMealTraceData db, IIdentityService manager) => (await AccountUseCases.CreateAccountAsync(input, principal, db, manager)).ToHttpResult()).Produces<AccountCreatedResponse>(StatusCodes.Status201Created).WithName("CreateUser");

        group.MapPut("/users/{id:guid}", async (Guid id, AccountInput input, ClaimsPrincipal principal,
            IMealTraceData db, IIdentityService manager) => (await AccountUseCases.UpdateAccountAsync(id, input, principal, db, manager)).ToHttpResult()).Produces<AccountView>().WithName("UpdateUser");

        group.MapPost("/users/{id:guid}/reset-password", async (Guid id, ResetPasswordInput input,
            ClaimsPrincipal principal, IMealTraceData db, IIdentityService manager) => (await AccountUseCases.ResetPasswordAsync(id, input, principal, db, manager)).ToHttpResult()).Produces<PasswordResetResponse>().WithName("AdminResetPassword");

        return app;
    }
}

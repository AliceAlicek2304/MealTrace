using MealTrace.Application.Exceptions;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Features.Accounts;
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
            try { return await next(context); }
            catch (PersistenceConflictException) { return Results.Conflict(new MessageResponse("Dữ liệu tài khoản vừa thay đổi hoặc bị trùng. Hãy tải lại và thử lại.")); }
        });

        group.MapGet("/users", async (int? page, int? pageSize, Guid? classId, AccountService service) => (await service.ListAccountsAsync(page, pageSize, classId)).ToHttpResult()).Produces<AccountListResponse>(StatusCodes.Status200OK).WithName("ListUsers");

        group.MapGet("/users/{id:guid}", async (Guid id, AccountService service) => (await service.GetAccountAsync(id)).ToHttpResult()).Produces<AccountView>().WithName("GetUser");

        group.MapGet("/scope-options", async (string? search, Guid? classId, int? classPage, int? studentPage, string? selectedClassIds, string? selectedStudentIds, AccountService service) => (await service.GetScopeOptionsAsync(search, classId, classPage, studentPage, selectedClassIds, selectedStudentIds)).ToHttpResult()).Produces<AccountScopeOptionsResponse>(StatusCodes.Status200OK).WithName("GetScopeOptions");

        group.MapPost("/users", async (AccountInput input, AccountService service) => (await service.CreateAccountAsync(input)).ToCreatedHttpResult(value => $"/api/admin/users/{value.User.Id}")).Produces<AccountCreatedResponse>(StatusCodes.Status201Created).WithName("CreateUser");

        group.MapPut("/users/{id:guid}", async (Guid id, AccountInput input, AccountService service) => (await service.UpdateAccountAsync(id, input)).ToHttpResult()).Produces<AccountView>().WithName("UpdateUser");

        group.MapPost("/users/{id:guid}/reset-password", async (Guid id, ResetPasswordInput input, AccountService service) => (await service.ResetPasswordAsync(id, input)).ToHttpResult()).Produces<PasswordResetResponse>().WithName("AdminResetPassword");

        return app;
    }
}

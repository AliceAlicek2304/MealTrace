using System.Security.Claims;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Features;
using MealTrace.Domain.Security;
using MealTrace.Application.Dtos.Auth;

namespace MealTrace.Api.Features;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");
        group.AddEndpointFilter(async (context, next) => { context.HttpContext.Response.Headers.CacheControl = "no-store"; return await next(context); });

        group.MapPost("/login", async (LoginRequest request, IMealTraceData db, IIdentityService users,
            IAccessTokenService tokens) => (await AuthUseCases.LoginAsync(request, db, users, tokens)).ToHttpResult()).RequireRateLimiting("login").AllowAnonymous().Produces<LoginResponse>().WithName("Login");

        group.MapGet("/me", async (ClaimsPrincipal principal, IMealTraceData db, IIdentityService users) => (await AuthUseCases.GetCurrentUserAsync(principal, db, users)).ToHttpResult()).RequireAuthorization().Produces<CurrentUser>().WithName("CurrentUser");

        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, IIdentityService users) => (await AuthUseCases.ChangePasswordAsync(request, principal, users)).ToHttpResult()).RequireAuthorization().Produces(StatusCodes.Status204NoContent).WithName("ChangePassword");

        group.MapPost("/logout", async (ClaimsPrincipal principal, IIdentityService users) => (await AuthUseCases.LogoutAsync(principal, users)).ToHttpResult()).RequireAuthorization().Produces(StatusCodes.Status204NoContent).WithName("Logout");

        return app;
    }
}

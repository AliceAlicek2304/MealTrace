using MealTrace.Application.Features.Auth;
using MealTrace.Application.Dtos.Auth;

namespace MealTrace.Api.Features;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");
        group.AddEndpointFilter(async (context, next) => { context.HttpContext.Response.Headers.CacheControl = "no-store"; return await next(context); });

        group.MapPost("/login", async (LoginRequest request, AuthService service) => (await service.LoginAsync(request)).ToHttpResult()).RequireRateLimiting("login").AllowAnonymous().Produces<LoginResponse>().WithName("Login");

        group.MapGet("/me", async (AuthService service) => (await service.GetCurrentUserAsync()).ToHttpResult()).RequireAuthorization().Produces<CurrentUser>().WithName("CurrentUser");

        group.MapPost("/change-password", async (ChangePasswordRequest request, AuthService service) => (await service.ChangePasswordAsync(request)).ToHttpResult()).RequireAuthorization().Produces(StatusCodes.Status204NoContent).WithName("ChangePassword");

        group.MapPost("/logout", async (AuthService service) => (await service.LogoutAsync()).ToHttpResult()).RequireAuthorization().Produces(StatusCodes.Status204NoContent).WithName("Logout");

        return app;
    }
}

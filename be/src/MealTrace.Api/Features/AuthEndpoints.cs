using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MealTrace.Api.Data;
using MealTrace.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Features;

public static class AuthEndpoints
{
    public sealed record LoginRequest(string Email, string Password);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public sealed record CurrentUser(Guid Id, string FullName, string Email, string[] Roles, DateOnly? InspectorAccessUntil);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, HttpContext http, MealTraceDbContext db, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn, JwtTokenService tokens) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { message = "Email và mật khẩu là bắt buộc." });

            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Unauthorized();

            var roles = (await users.GetRolesAsync(user)).ToArray();
            var grant = await db.InspectorGrants.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (roles.Length == 0 && (grant is null || grant.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow)))
                return Results.Unauthorized();
            var (token, expiresAt) = await tokens.CreateAsync(user);
            return Results.Ok(new { accessToken = token, expiresAt, user = new CurrentUser(user.Id, user.FullName, user.Email!, roles, grant?.ExpiresOn) });
        }).RequireRateLimiting("login").AllowAnonymous().WithName("Login");

        group.MapGet("/me", async (ClaimsPrincipal principal, HttpContext http, MealTraceDbContext db, UserManager<ApplicationUser> users) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var roles = (await users.GetRolesAsync(user)).ToArray();
            var grant = await db.InspectorGrants.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
            return Results.Ok(new CurrentUser(user.Id, user.FullName, user.Email!, roles, grant?.ExpiresOn));
        }).RequireAuthorization().WithName("CurrentUser");

        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal,
            HttpContext http, UserManager<ApplicationUser> users) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return Results.BadRequest(new { message = "Cần nhập mật khẩu hiện tại và mật khẩu mới." });
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return Results.BadRequest(new { message = string.Join("; ", result.Errors.Select(x => x.Description)) });
            return Results.NoContent();
        }).RequireAuthorization().WithName("ChangePassword");

        group.MapPost("/logout", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null) return Results.Unauthorized();
            var result = await users.UpdateSecurityStampAsync(user);
            return result.Succeeded ? Results.NoContent() : Results.Problem("Không thể kết thúc phiên đăng nhập.");
        }).RequireAuthorization().WithName("Logout");

        return app;
    }
}

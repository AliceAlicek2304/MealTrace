using MealTrace.Application.Features.Auth;
using MealTrace.Application.Dtos.Auth;
using MealTrace.Application.Dtos.Common;
using MealTrace.Application.Exceptions;

namespace MealTrace.Api.Features;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (PersistenceConflictException)
            {
                return Results.Conflict(new MessageResponse("Dữ liệu tài khoản vừa thay đổi hoặc SĐT đã được sử dụng. Vui lòng đăng nhập hoặc thử lại."));
            }
        });

        group.MapPost("/login", async (LoginRequest request, AuthService service) => (await service.LoginAsync(request)).ToHttpResult()).RequireRateLimiting("login").AllowAnonymous().Produces<LoginResponse>().WithName("Login").WithSummary("Đăng nhập").WithDescription("Gửi identifier là email hoặc số điện thoại cùng với mật khẩu.");

        group.MapPost("/register/otp", async (RequestParentOtp request, ParentSignupOtpService service) => (await service.RequestAsync(request)).ToHttpResult()).RequireRateLimiting("registrationOtp").AllowAnonymous().Produces<ParentOtpResponse>().WithName("RequestParentOtp");

        group.MapPost("/register", async (RegisterParentRequest request, AuthService service) => (await service.RegisterParentAsync(request)).ToHttpResult()).RequireRateLimiting("register").AllowAnonymous().Produces(StatusCodes.Status204NoContent).WithName("RegisterParent");

        group.MapGet("/me", async (AuthService service) => (await service.GetCurrentUserAsync()).ToHttpResult()).RequireAuthorization().Produces<CurrentUser>().WithName("CurrentUser");

        group.MapPost("/change-password", async (ChangePasswordRequest request, AuthService service) => (await service.ChangePasswordAsync(request)).ToHttpResult()).RequireAuthorization().Produces(StatusCodes.Status204NoContent).WithName("ChangePassword");

        group.MapPost("/logout", async (AuthService service) => (await service.LogoutAsync()).ToHttpResult()).RequireAuthorization().Produces(StatusCodes.Status204NoContent).WithName("Logout");

        return app;
    }
}

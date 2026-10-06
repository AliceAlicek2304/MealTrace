using System.IdentityModel.Tokens.Jwt;
using MealTrace.Domain.Security;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace MealTrace.Infrastructure.Identity;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddMealTraceAuthentication(this IServiceCollection services, IConfiguration configuration, byte[] jwtKey)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = configuration["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var id = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    var stamp = context.Principal?.FindFirst("stamp")?.Value;
                    var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = id is null ? null : await manager.FindByIdAsync(id);
                    if (user is null || !user.IsActive || stamp != await manager.GetSecurityStampAsync(user))
                    {
                        context.Fail("Account is inactive or token was revoked.");
                        return;
                    }
                    if (!RoleNames.All.Any(context.Principal!.IsInRole))
                    {
                        var db = context.HttpContext.RequestServices.GetRequiredService<MealTraceDbContext>();
                        var activeGrant = await db.InspectorGrants.AnyAsync(x => x.UserId == user.Id && x.ExpiresOn >= DateOnly.FromDateTime(DateTime.UtcNow));
                        if (!activeGrant) context.Fail("Inspector grant has expired.");
                    }
                },
            };
        });
        return services;
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MealTrace.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace MealTrace.Api.Security;

public sealed class JwtTokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager)
{
    public async Task<(string Token, DateTimeOffset ExpiresAt)> CreateAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var expires = DateTimeOffset.UtcNow.AddMinutes(60);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("stamp", await userManager.GetSecurityStampAsync(user)),
        };
        if (!string.IsNullOrWhiteSpace(user.Email)) claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var key = Convert.FromBase64String(configuration["Jwt:Key"]!);
        var jwt = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

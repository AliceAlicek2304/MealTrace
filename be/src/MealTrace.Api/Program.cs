using MealTrace.Api.Data;
using MealTrace.Api.Features;
using MealTrace.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();
}
var connectionString = builder.Configuration.GetConnectionString("MealTrace")
    ?? throw new InvalidOperationException("ConnectionStrings:MealTrace is required.");
var jwtKeyText = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required in User Secrets or environment variables.");
byte[] jwtKey;
try { jwtKey = Convert.FromBase64String(jwtKeyText); }
catch (FormatException) { throw new InvalidOperationException("Jwt:Key must be a Base64-encoded random key."); }
if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key must contain at least 32 random bytes.");
builder.Services.AddDbContext<MealTraceDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = false;
    options.Password.RequiredLength = builder.Environment.IsDevelopment() ? 8 : 12;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    if (builder.Environment.IsDevelopment())
    {
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    }
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
}).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<MealTraceDbContext>()
    .AddSignInManager().AddDefaultTokenProviders();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
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
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MealTrace API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Dán access token từ POST /api/auth/login.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    if (app.Configuration.GetValue("Seed:Enabled", true))
        await DevelopmentSeeder.SeedAsync(app.Services);
    if (args.Contains("--seed-only")) return;
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "MealTrace API" }));
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapMealEndpoints();
app.MapWorkflowEndpoints();
app.MapMealExceptionEndpoints();
app.MapMealCalendarEndpoints();
app.MapStudentAdministrationEndpoints();
app.Run();

public partial class Program;

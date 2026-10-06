using MealTrace.Application;
using MealTrace.Infrastructure;
using MealTrace.Infrastructure.Persistence;
using MealTrace.Infrastructure.Identity;
using MealTrace.Api.Features;

using Microsoft.OpenApi.Models;
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
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, builder.Environment.IsDevelopment());
builder.Services.AddMealTraceAuthentication(builder.Configuration, jwtKey);
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
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán access token từ POST /api/auth/login.",
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
app.MapGet("/api/health", () => Results.Ok(new MealTrace.Application.Dtos.Common.HealthResponse("ok", "MealTrace API"))).Produces<MealTrace.Application.Dtos.Common.HealthResponse>();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapMealEndpoints();
app.MapWorkflowEndpoints();
app.MapMealExceptionEndpoints();
app.MapMealCalendarEndpoints();
app.MapPortionAmendmentEndpoints();
app.MapStudentAdministrationEndpoints();
app.MapKitchenEndpoints();
app.Run();

public partial class Program;

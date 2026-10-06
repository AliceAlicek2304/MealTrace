using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Infrastructure.Persistence.Repositories;
using MealTrace.Application.Abstractions;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MealTrace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, bool development)
    {
        services.AddDbContext<MealTraceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = false;
            options.Password.RequiredLength = development ? 8 : 12;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            if (development)
            {
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            }
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<MealTraceDbContext>()
            .AddSignInManager().AddDefaultTokenProviders();
        services.AddHttpContextAccessor();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IAccessTokenService, JwtTokenService>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IStudentAdministrationRepository, StudentAdministrationRepository>();
        services.AddScoped<IMealCalendarRepository, MealCalendarRepository>();
        services.AddScoped<IMealExceptionRepository, MealExceptionRepository>();
        services.AddScoped<IPortionAmendmentRepository, PortionAmendmentRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IMealDecisionRepository, MealDecisionRepository>();
        services.AddScoped<IPortionRepository, PortionRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<INutritionRepository, NutritionRepository>();
        return services;
    }
}

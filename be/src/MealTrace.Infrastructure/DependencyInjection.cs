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
    public static IServiceCollection AddNotifications(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        var enabled = bool.TryParse(config["Notifications:Messaging:Enabled"], out var value) && value;
        var preferVonage = string.Equals(config["Notifications:Messaging:PrimaryProvider"], "Vonage", StringComparison.OrdinalIgnoreCase);
        services.AddSingleton(new MealTrace.Application.Features.Notifications.NotificationPolicy(enabled, config["Notifications:Messaging:TestNumber"], "WhatsApp", !preferVonage));
        services.AddSingleton(new Notifications.TwilioWhatsAppSettings(config["Notifications:Twilio:AccountSid"], config["Notifications:Twilio:AuthToken"], config["Notifications:Twilio:From"], config["Notifications:Twilio:ContentSid"]));
        services.AddSingleton(new Notifications.VonageWhatsAppSettings(config["Notifications:Vonage:ApiKey"], config["Notifications:Vonage:ApiSecret"], config["Notifications:Vonage:From"]));
        services.AddHttpClient<Notifications.TwilioWhatsAppSender>(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(["Authorization"]);
        services.AddHttpClient<Notifications.VonageWhatsAppSender>(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(["Authorization"]);
        services.AddTransient<MealTrace.Application.Abstractions.Notifications.IRegistrationOtpSender>(sp => new Notifications.RegistrationWhatsAppOtpSender(
            sp.GetRequiredService<Notifications.VonageWhatsAppSender>(), sp.GetRequiredService<Notifications.TwilioWhatsAppSender>(), preferVonage));
        services.AddTransient<MealTrace.Application.Abstractions.Notifications.INotificationSender>(sp => preferVonage
            ? new Notifications.PriorityWhatsAppSender(sp.GetRequiredService<Notifications.VonageWhatsAppSender>(), sp.GetRequiredService<Notifications.TwilioWhatsAppSender>())
            : sp.GetRequiredService<Notifications.TwilioWhatsAppSender>());
        return services;
    }
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, bool development)
    {
        services.AddDbContext<MealTraceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAuditSource, Persistence.Audit.CalendarAuditSource>();
        services.AddScoped<IAuditSource, Persistence.Audit.PasswordResetAuditSource>();
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.EnrollmentAuditSource(sp.GetRequiredService<MealTraceDbContext>()));
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.EnrollmentAuditSource(sp.GetRequiredService<MealTraceDbContext>(), ending: true));
        services.AddScoped<IAuditSource, Persistence.Audit.MealExceptionAuditSource>();
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.AbsenceAuditSource(sp.GetRequiredService<MealTraceDbContext>()));
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.AbsenceAuditSource(sp.GetRequiredService<MealTraceDbContext>(), cancelled: true));
        services.AddScoped<IAuditSource, Persistence.Audit.AmendmentAuditSource>();
        services.AddScoped<IAuditSource, Persistence.Audit.AmendmentResolutionAuditSource>();
        services.AddScoped<IAuditSource, Persistence.Audit.SettlementAuditSource>();
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.ParentLinkAuditSource(sp.GetRequiredService<MealTraceDbContext>()));
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.ParentLinkAuditSource(sp.GetRequiredService<MealTraceDbContext>(), "review"));
        services.AddScoped<IAuditSource>(sp => new Persistence.Audit.ParentLinkAuditSource(sp.GetRequiredService<MealTraceDbContext>(), "revoke"));
        services.AddScoped<IAuditSource, Persistence.Audit.StudentImportAuditSource>();
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
        services.AddScoped<IStudentImportRepository, StudentImportRepository>();
        services.AddSingleton<MealTrace.Application.Abstractions.IStudentSpreadsheetReader, Imports.StudentSpreadsheetReader>();
        services.AddScoped<IParentLinkRepository, ParentLinkRepository>();
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

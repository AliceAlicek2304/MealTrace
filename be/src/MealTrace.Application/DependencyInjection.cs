using MealTrace.Application.Features.Workflow;
using MealTrace.Application.Features.Students;
using MealTrace.Application.Features.Portions;
using MealTrace.Application.Features.Meals;
using MealTrace.Application.Features.Calendar;
using MealTrace.Application.Features.Auth;
using MealTrace.Application.Features.Accounts;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Features.Kitchen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MealTrace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<INutritionService, NutritionService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<Features.Notifications.NotificationService>();
        services.AddScoped<Features.Notifications.ParentRegistrationNotificationService>();
        services.AddSingleton<Features.Notifications.NotificationSendGate>();
        services.AddScoped<WorkflowService>();
        services.AddScoped<StudentAdministrationService>();
        services.AddScoped<MealCalendarService>();
        services.AddScoped<MealExceptionService>();
        services.AddScoped<PortionAmendmentService>();
        services.AddScoped<MealService>();
        services.AddScoped<MealDecisionService>();
        services.AddScoped<PortionService>();
        return services;
    }
}

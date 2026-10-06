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
        services.AddScoped<AuthUseCases>();
        services.AddScoped<AccountUseCases>();
        services.AddScoped<WorkflowUseCases>();
        services.AddScoped<StudentAdministrationUseCases>();
        services.AddScoped<MealCalendarUseCases>();
        services.AddScoped<MealExceptionUseCases>();
        services.AddScoped<PortionAmendmentUseCases>();
        services.AddScoped<MealUseCases>();
        services.AddScoped<MealDecisionService>();
        services.AddScoped<PortionService>();
        return services;
    }
}

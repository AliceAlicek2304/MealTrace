using MealTrace.Domain.Entities;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Dtos.Common;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface IRecipeService
{
    Task<RecipeVersionCreatedResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct);

    Task<RecipeVersionCreatedResponse> AddVersionAsync(
        Guid recipeId, CreateRecipeVersionRequest request, CancellationToken ct);
}

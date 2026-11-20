using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface IRecipeService
{
    Task<RecipeVersionCreatedResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct);

    Task<RecipeVersionCreatedResponse> AddVersionAsync(
        Guid recipeId, CreateRecipeVersionRequest request, CancellationToken ct);

    Task<RecipeCatalogPage> ListAsync(string? search, int? page, int? pageSize, CancellationToken ct);

    /// <summary>Every version of a recipe with reason, actor, rounding rule and its lines; nothing is "the latest" implicitly.</summary>
    Task<RecipeVersionList> ListVersionsAsync(Guid recipeId, CancellationToken ct);
}

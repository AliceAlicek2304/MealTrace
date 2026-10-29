namespace MealTrace.Application.Dtos.Kitchen;

public sealed record RecipeVersionCreatedResponse(
    Guid RecipeId,
    int Version,
    Guid? VersionId = null,
    int? PreviousVersion = null,
    Guid? PreviousVersionId = null,
    DateTimeOffset? RecordedAt = null,
    Guid? RecordedByUserId = null,
    string? Reason = null,
    NutritionRoundingRule? RoundingRule = null);

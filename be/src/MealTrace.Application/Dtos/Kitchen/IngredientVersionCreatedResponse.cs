namespace MealTrace.Application.Dtos.Kitchen;

public sealed record IngredientVersionCreatedResponse(
    Guid VersionId, int Version, DateTimeOffset ValidFrom, DateTimeOffset RecordedAt);

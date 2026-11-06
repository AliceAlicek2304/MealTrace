namespace MealTrace.Application.Dtos.Kitchen;

public sealed record IngredientVersionCreatedResponse(
    Guid VersionId,
    int Version,
    DateTimeOffset ValidFrom,
    DateTimeOffset RecordedAt,
    DateTimeOffset? ValidTo = null,
    Guid? RecordedByUserId = null,
    Guid? SupersedesId = null,
    string? Source = null,
    string? SourceRef = null);

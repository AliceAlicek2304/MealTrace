namespace MealTrace.Application.Models.Persistence;

public sealed record PortionClassKey
{
    public Guid ClassId { get; init; } = default!;
    public string ClassName { get; init; } = default!;
    public string SchoolYear { get; init; } = default!;
}

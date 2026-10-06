namespace MealTrace.Application.Models.Persistence;

public sealed record LatestSettlementRow
{
    public Guid Id { get; init; } = default!;
    public Guid ClassId { get; init; } = default!;
    public string ClassName { get; init; } = default!;
    public string SchoolYear { get; init; } = default!;
    public int Version { get; init; } = default!;
    public int Count { get; init; } = default!;
    public int OriginalCount { get; init; } = default!;
}

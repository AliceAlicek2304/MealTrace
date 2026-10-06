
namespace MealTrace.Application.Dtos.Responses;
public sealed record MealDaySummary
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public required List<MealDishSummary> Dishes { get; init; }
    public int? SettledPortions { get; init; }
}

public sealed record MealDishSummary
{
    public Guid Id { get; init; }
    public Guid RecipeVersionId { get; init; }
    public required string Name { get; init; }
}

public sealed record MealDayDetail
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public required string MealType { get; init; }
    public DateTimeOffset CutoffAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
    public int? SettledPortions { get; init; }
    public required IEnumerable<MealDishSummary> Dishes { get; init; }
    public required IEnumerable<SettlementSummary> Settlements { get; init; }
    public required IEnumerable<MealEvidenceSummary> Evidence { get; init; }
}

public sealed record SettlementSummary
{
    public Guid Id { get; init; }
    public Guid? ClassId { get; init; }
    public string? ClassName { get; init; }
    public int Version { get; init; }
    public int Count { get; init; }
    public DateTimeOffset SettledAt { get; init; }
    public required string SettledBy { get; init; }
    public string? Reason { get; init; }
    public Guid? SupersedesId { get; init; }
}

public sealed record MealEvidenceSummary
{
    public Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string Description { get; init; }
    public string? PhotoUrl { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public DateTimeOffset SyncedAt { get; init; }
    public Guid? AmendsId { get; init; }
}

public sealed record ReportLineageResponse
{
    public Guid Id { get; init; }
    public Guid MealDayId { get; init; }
    public Guid SettlementId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SignedAt { get; init; }
    public decimal EnergyKcalPerPortion { get; init; }
    public decimal CostPerPortion { get; init; }
    public required string SourceJson { get; init; }
}

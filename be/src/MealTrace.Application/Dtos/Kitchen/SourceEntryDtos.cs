namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>A dated physical receipt of an ingredient. Not stock, not a purchase order.</summary>
public sealed record SourceEntryRequest(
    Guid? IngredientId, Guid? SupplierVersionId, DateOnly? ReferenceDate, decimal? Quantity, string? Unit,
    string? DeliveryRef, string? LotRef);

public sealed record SourceEntryCorrectionRequest(
    int? ExpectedVersion, string? Reason, Guid? SupplierVersionId, DateOnly? ReferenceDate, decimal? Quantity,
    string? Unit, string? DeliveryRef, string? LotRef);

public sealed record SourceEntryCreatedResponse(
    Guid Id, Guid VersionId, int Version, Guid IngredientId, decimal Quantity, string Unit,
    DateTimeOffset RecordedAt, Guid? RecordedBy, Guid? PriorVersionId = null);

public sealed record SourceEntryView(
    Guid Id, Guid VersionId, int Version, Guid IngredientId, string IngredientName, Guid SupplierId,
    Guid SupplierVersionId, string SupplierName, DateOnly ReferenceDate, decimal Quantity, string Unit,
    string? DeliveryRef, string? LotRef, DateTimeOffset RecordedAt, Guid? RecordedBy);

public sealed record SourceEntryPage(IReadOnlyList<SourceEntryView> Items, int Total, int Page, int PageSize);

public sealed record SourceEntryHistoryItem(
    Guid VersionId, int Version, Guid? PriorVersionId, Guid SupplierVersionId, string SupplierName,
    DateOnly ReferenceDate, decimal Quantity, string Unit, string? DeliveryRef, string? LotRef,
    string? Reason, DateTimeOffset RecordedAt, Guid? RecordedBy);

public sealed record SourceEntryHistory(Guid Id, Guid IngredientId, IReadOnlyList<SourceEntryHistoryItem> Items);

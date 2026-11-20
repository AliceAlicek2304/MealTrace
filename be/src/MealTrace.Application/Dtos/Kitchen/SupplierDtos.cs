namespace MealTrace.Application.Dtos.Kitchen;

public sealed record SupplierRequest(string? Name, string? Contact, string? Reference);

/// <summary>A correction carries the full new values, the version it was based on and a reason.</summary>
public sealed record SupplierCorrectionRequest(int? ExpectedVersion, string? Reason, string? Name, string? Contact, string? Reference);

public sealed record SupplierCreatedResponse(
    Guid Id, Guid VersionId, int Version, DateTimeOffset RecordedAt, Guid? RecordedBy, Guid? PriorVersionId = null);

public sealed record SupplierView(
    Guid Id, Guid VersionId, int Version, string Name, string? Contact, string? Reference,
    DateTimeOffset RecordedAt, Guid? RecordedBy);

public sealed record SupplierPage(IReadOnlyList<SupplierView> Items, int Total, int Page, int PageSize);

public sealed record SupplierHistoryItem(
    Guid VersionId, int Version, Guid? PriorVersionId, string Name, string? Contact, string? Reference,
    string? Reason, DateTimeOffset RecordedAt, Guid? RecordedBy);

public sealed record SupplierHistory(Guid Id, IReadOnlyList<SupplierHistoryItem> Items);

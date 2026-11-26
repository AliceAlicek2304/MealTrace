using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

/// <summary>
/// Dated physical receipts of an ingredient from a supplier revision (LD-02). This is unrelated to the nutrient
/// "source" on an ingredient version, which says where nutrient values came from.
/// </summary>
public sealed class IngredientSourceService(
    ISourceRepository repository, TimeProvider time, IUnitOfWork unitOfWork, ICurrentActor actor)
    : IIngredientSourceService
{
    private const decimal MaxQuantity = 1_000_000_000m;
    private const int MaxRefLength = 100;
    private const int MaxReasonLength = 500;

    public async Task<SourceEntryCreatedResponse> CreateAsync(SourceEntryRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var unit = request.Unit?.Trim().ToLowerInvariant();
        var (deliveryRef, lotRef) = CleanRefs(request.DeliveryRef, request.LotRef, errors);

        if (request.IngredientId is null || request.IngredientId == Guid.Empty) errors.Add("ingredientId", "ingredientId is required.");
        if (request.SupplierVersionId is null || request.SupplierVersionId == Guid.Empty) errors.Add("supplierVersionId", "supplierVersionId is required.");
        CheckValues(request.ReferenceDate, request.Quantity, unit, errors);
        errors.ThrowIfAny();

        var ingredient = await repository.FindIngredientAsync(request.IngredientId!.Value, ct);
        if (ingredient is null) errors.Add("ingredientId", "Ingredient not found.");
        else if (unit != ingredient.Unit) errors.Add("unit", $"unit must match the ingredient unit '{ingredient.Unit}'.");

        var supplier = await repository.FindSupplierRevisionAsync(request.SupplierVersionId!.Value, ct);
        if (supplier is null) errors.Add("supplierVersionId", "Supplier version not found.");
        errors.ThrowIfAny();

        var now = time.GetUtcNow();
        var entry = new IngredientSourceEntry
        {
            Id = Guid.NewGuid(), IngredientId = ingredient!.Id, CreatedAt = now, CreatedByUserId = actor.UserId
        };
        var version = NewVersion(entry.Id, 1, supplier!.Id, request.ReferenceDate!.Value, request.Quantity!.Value, unit!,
            deliveryRef, lotRef, null, null, now);

        repository.AddSourceEntry(entry);
        repository.AddSourceEntryVersion(version);
        await unitOfWork.SaveChangesAsync(ct);

        return Created(entry.IngredientId, version);
    }

    public async Task<SourceEntryCreatedResponse> AddVersionAsync(
        Guid entryId, SourceEntryCorrectionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var unit = request.Unit?.Trim().ToLowerInvariant();
        var reason = request.Reason?.Trim();
        var (deliveryRef, lotRef) = CleanRefs(request.DeliveryRef, request.LotRef, errors);

        if (request.ExpectedVersion is null or < 1) errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        if (string.IsNullOrEmpty(reason)) errors.Add("reason", "reason is required for a correction.");
        else if (reason.Length > MaxReasonLength) errors.Add("reason", $"reason must be at most {MaxReasonLength} characters.");
        if (request.SupplierVersionId is null || request.SupplierVersionId == Guid.Empty) errors.Add("supplierVersionId", "supplierVersionId is required.");
        CheckValues(request.ReferenceDate, request.Quantity, unit, errors);
        errors.ThrowIfAny();

        var entry = await repository.FindSourceEntryAsync(entryId, ct)
            ?? throw KitchenException.NotFound("SOURCE_ENTRY_NOT_FOUND", "Source entry not found.");
        var latest = await repository.GetLatestEntryVersionAsync(entryId, ct)
            ?? throw KitchenException.NotFound("SOURCE_ENTRY_NOT_FOUND", "Source entry not found.");
        if (latest.Version != request.ExpectedVersion) throw Conflict(latest.Version);

        // The ingredient of an entry never changes: a different ingredient is a different receipt.
        var ingredient = await repository.FindIngredientAsync(entry.IngredientId, ct);
        if (ingredient is not null && unit != ingredient.Unit)
            errors.Add("unit", $"unit must match the ingredient unit '{ingredient.Unit}'.");
        var supplier = await repository.FindSupplierRevisionAsync(request.SupplierVersionId!.Value, ct);
        if (supplier is null) errors.Add("supplierVersionId", "Supplier version not found.");
        errors.ThrowIfAny();

        var version = NewVersion(entryId, latest.Version + 1, supplier!.Id, request.ReferenceDate!.Value,
            request.Quantity!.Value, unit!, deliveryRef, lotRef, reason, latest.Id, time.GetUtcNow());

        repository.AddSourceEntryVersion(version);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            throw Conflict(version.Version);
        }

        return Created(entry.IngredientId, version);
    }

    public async Task<SourceEntryPage> ListAsync(
        Guid? ingredientId, DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNo, size, skip) = KitchenPaging.Normalize(page, pageSize);
        var total = await repository.CountEntriesAsync(ingredientId, from, to, ct);
        var rows = await repository.ListLatestEntriesAsync(ingredientId, from, to, skip, size, ct);

        return new SourceEntryPage(
            rows.Select(r => new SourceEntryView(
                r.Entry.Id, r.Version.Id, r.Version.Version, r.Entry.IngredientId, r.IngredientName, r.Supplier.SupplierId,
                r.Supplier.Id, r.Supplier.Name, r.Version.ReferenceDate, r.Version.Quantity, r.Version.Unit,
                r.Version.DeliveryRef, r.Version.LotRef, r.Version.RecordedAt.ToVn(), r.Version.RecordedByUserId)).ToList(),
            total, pageNo, size);
    }

    public async Task<SourceEntryHistory> HistoryAsync(Guid entryId, CancellationToken ct)
    {
        var entry = await repository.FindSourceEntryAsync(entryId, ct)
            ?? throw KitchenException.NotFound("SOURCE_ENTRY_NOT_FOUND", "Source entry not found.");
        var versions = (await repository.ListEntryVersionsAsync(entryId, ct)).OrderBy(v => v.Version).ToList();
        var suppliers = (await repository.GetSupplierRevisionsByIdAsync(
            versions.Select(v => v.SupplierRevisionId).Distinct().ToArray(), ct)).ToDictionary(s => s.Id);

        return new SourceEntryHistory(entryId, entry.IngredientId,
            versions.Select(v => new SourceEntryHistoryItem(
                v.Id, v.Version, v.PriorVersionId, v.SupplierRevisionId,
                suppliers.TryGetValue(v.SupplierRevisionId, out var s) ? s.Name : "", v.ReferenceDate, v.Quantity, v.Unit,
                v.DeliveryRef, v.LotRef, v.Reason, v.RecordedAt.ToVn(), v.RecordedByUserId)).ToList());
    }

    private IngredientSourceEntryVersion NewVersion(
        Guid entryId, int version, Guid supplierRevisionId, DateOnly date, decimal quantity, string unit,
        string? deliveryRef, string? lotRef, string? reason, Guid? priorId, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), SourceEntryId = entryId, Version = version, SupplierRevisionId = supplierRevisionId,
        ReferenceDate = date, Quantity = Math.Round(quantity, 3), Unit = unit, DeliveryRef = deliveryRef, LotRef = lotRef,
        Reason = reason, PriorVersionId = priorId, RecordedAt = at, RecordedByUserId = actor.UserId
    };

    private static SourceEntryCreatedResponse Created(Guid ingredientId, IngredientSourceEntryVersion v) =>
        new(v.SourceEntryId, v.Id, v.Version, ingredientId, v.Quantity, v.Unit, v.RecordedAt.ToVn(), v.RecordedByUserId, v.PriorVersionId);

    private static KitchenException Conflict(int currentVersion) =>
        new(409, "SOURCE_ENTRY_VERSION_CONFLICT",
            "The source entry was changed by someone else. Reload it and retry with the latest version.",
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

    private static void CheckValues(DateOnly? date, decimal? quantity, string? unit, ValidationErrors errors)
    {
        if (date is null) errors.Add("referenceDate", "referenceDate is required.");
        if (quantity is null or <= 0m) errors.Add("quantity", "quantity must be greater than 0.");
        else if (quantity > MaxQuantity) errors.Add("quantity", $"quantity must be at most {MaxQuantity:0}.");
        if (unit is not ("g" or "ml")) errors.Add("unit", "unit must be 'g' or 'ml'.");
    }

    private static (string? DeliveryRef, string? LotRef) CleanRefs(string? deliveryRef, string? lotRef, ValidationErrors errors)
    {
        deliveryRef = string.IsNullOrWhiteSpace(deliveryRef) ? null : deliveryRef.Trim();
        lotRef = string.IsNullOrWhiteSpace(lotRef) ? null : lotRef.Trim();
        if (deliveryRef is { Length: > MaxRefLength }) errors.Add("deliveryRef", $"deliveryRef must be at most {MaxRefLength} characters.");
        if (lotRef is { Length: > MaxRefLength }) errors.Add("lotRef", $"lotRef must be at most {MaxRefLength} characters.");
        return (deliveryRef, lotRef);
    }
}

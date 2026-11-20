using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

public sealed class SupplierService(
    ISourceRepository repository, TimeProvider time, IUnitOfWork unitOfWork, ICurrentActor actor) : ISupplierService
{
    private const int MaxNameLength = 200;
    private const int MaxContactLength = 500;
    private const int MaxReferenceLength = 100;
    private const int MaxReasonLength = 500;

    public async Task<SupplierCreatedResponse> CreateAsync(SupplierRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var (name, contact, reference) = Clean(request.Name, request.Contact, request.Reference, errors);
        errors.ThrowIfAny();

        var now = time.GetUtcNow();
        var supplier = new Supplier { Id = Guid.NewGuid(), CreatedAt = now, CreatedByUserId = actor.UserId };
        var revision = new SupplierRevision
        {
            Id = Guid.NewGuid(), SupplierId = supplier.Id, Version = 1, Name = name!, Contact = contact,
            Reference = reference, RecordedAt = now, RecordedByUserId = actor.UserId
        };

        repository.AddSupplier(supplier);
        repository.AddSupplierRevision(revision);
        await unitOfWork.SaveChangesAsync(ct);

        return Created(revision);
    }

    public async Task<SupplierCreatedResponse> AddVersionAsync(
        Guid supplierId, SupplierCorrectionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var (name, contact, reference) = Clean(request.Name, request.Contact, request.Reference, errors);
        var reason = request.Reason?.Trim();
        if (request.ExpectedVersion is null or < 1)
            errors.Add("expectedVersion", "expectedVersion is required and must be at least 1.");
        if (string.IsNullOrEmpty(reason)) errors.Add("reason", "reason is required for a correction.");
        else if (reason.Length > MaxReasonLength) errors.Add("reason", $"reason must be at most {MaxReasonLength} characters.");
        errors.ThrowIfAny();

        var latest = await repository.GetLatestSupplierRevisionAsync(supplierId, ct)
            ?? throw KitchenException.NotFound("SUPPLIER_NOT_FOUND", "Supplier not found.");
        if (latest.Version != request.ExpectedVersion)
            throw Conflict(latest.Version);

        var revision = new SupplierRevision
        {
            Id = Guid.NewGuid(), SupplierId = supplierId, Version = latest.Version + 1, Name = name!, Contact = contact,
            Reference = reference, Reason = reason, PriorVersionId = latest.Id, RecordedAt = time.GetUtcNow(),
            RecordedByUserId = actor.UserId
        };

        repository.AddSupplierRevision(revision);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is PersistenceConflictException { Kind: PersistenceConflictKind.DuplicateKey })
        {
            throw Conflict(revision.Version);
        }

        return Created(revision);
    }

    public async Task<SupplierPage> ListAsync(string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNo, size, skip) = KitchenPaging.Normalize(page, pageSize);
        var total = await repository.CountSuppliersAsync(search, ct);
        var rows = await repository.ListLatestSupplierRevisionsAsync(search, skip, size, ct);

        return new SupplierPage(
            rows.Select(r => new SupplierView(
                r.SupplierId, r.Id, r.Version, r.Name, r.Contact, r.Reference, r.RecordedAt.ToVn(), r.RecordedByUserId)).ToList(),
            total, pageNo, size);
    }

    public async Task<SupplierHistory> HistoryAsync(Guid supplierId, CancellationToken ct)
    {
        var rows = await repository.ListSupplierRevisionsAsync(supplierId, ct);
        if (rows.Count == 0) throw KitchenException.NotFound("SUPPLIER_NOT_FOUND", "Supplier not found.");

        return new SupplierHistory(supplierId,
            rows.OrderBy(r => r.Version).Select(r => new SupplierHistoryItem(
                r.Id, r.Version, r.PriorVersionId, r.Name, r.Contact, r.Reference, r.Reason,
                r.RecordedAt.ToVn(), r.RecordedByUserId)).ToList());
    }

    private static SupplierCreatedResponse Created(SupplierRevision r) =>
        new(r.SupplierId, r.Id, r.Version, r.RecordedAt.ToVn(), r.RecordedByUserId, r.PriorVersionId);

    private static KitchenException Conflict(int currentVersion) =>
        new(409, "SUPPLIER_VERSION_CONFLICT",
            "The supplier was changed by someone else. Reload it and retry with the latest version.",
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

    private static (string? Name, string? Contact, string? Reference) Clean(
        string? name, string? contact, string? reference, ValidationErrors errors)
    {
        name = name?.Trim();
        contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();

        if (string.IsNullOrEmpty(name)) errors.Add("name", "name is required.");
        else if (name.Length > MaxNameLength) errors.Add("name", $"name must be at most {MaxNameLength} characters.");
        else if (name.Any(char.IsControl)) errors.Add("name", "name must not contain control characters.");
        if (contact is { Length: > MaxContactLength }) errors.Add("contact", $"contact must be at most {MaxContactLength} characters.");
        if (reference is { Length: > MaxReferenceLength }) errors.Add("reference", $"reference must be at most {MaxReferenceLength} characters.");

        return (name, contact, reference);
    }
}

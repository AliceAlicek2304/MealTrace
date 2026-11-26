using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;

namespace MealTrace.Application.Abstractions.Repositories;

/// <summary>Typed data operations for suppliers and dated ingredient source entries. Mutations do not commit.</summary>
public interface ISourceRepository
{
    Task<SupplierRevision?> FindSupplierRevisionAsync(Guid revisionId, CancellationToken ct);
    Task<SupplierRevision?> GetLatestSupplierRevisionAsync(Guid supplierId, CancellationToken ct);
    Task<List<SupplierRevision>> ListSupplierRevisionsAsync(Guid supplierId, CancellationToken ct);
    Task<int> CountSuppliersAsync(string? search, CancellationToken ct);
    /// <summary>The latest revision of each supplier, ordered by name then id.</summary>
    Task<List<SupplierRevision>> ListLatestSupplierRevisionsAsync(string? search, int skip, int size, CancellationToken ct);
    Task<List<SupplierRevision>> GetSupplierRevisionsByIdAsync(Guid[] revisionIds, CancellationToken ct);

    Task<Ingredient?> FindIngredientAsync(Guid ingredientId, CancellationToken ct);
    Task<IngredientSourceEntry?> FindSourceEntryAsync(Guid entryId, CancellationToken ct);
    Task<IngredientSourceEntryVersion?> GetLatestEntryVersionAsync(Guid entryId, CancellationToken ct);
    Task<List<IngredientSourceEntryVersion>> ListEntryVersionsAsync(Guid entryId, CancellationToken ct);
    Task<int> CountEntriesAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, CancellationToken ct);
    /// <summary>The latest version of each entry whose reference date falls in the range, newest date first.</summary>
    Task<List<SourceEntryRow>> ListLatestEntriesAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, int skip, int size, CancellationToken ct);

    void AddSupplier(Supplier value);
    void AddSupplierRevision(SupplierRevision value);
    void AddSourceEntry(IngredientSourceEntry value);
    void AddSourceEntryVersion(IngredientSourceEntryVersion value);
}

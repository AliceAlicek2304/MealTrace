using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal sealed class SourceRepository(MealTraceDbContext db) : ISourceRepository
{
    public async Task<SupplierRevision?> FindSupplierRevisionAsync(Guid revisionId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.SupplierRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == revisionId, ct);
        });
    }
    public async Task<SupplierRevision?> GetLatestSupplierRevisionAsync(Guid supplierId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.SupplierRevisions.AsNoTracking().Where(r => r.SupplierId == supplierId)
                .OrderByDescending(r => r.Version).FirstOrDefaultAsync(ct);
        });
    }
    public async Task<List<SupplierRevision>> ListSupplierRevisionsAsync(Guid supplierId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.SupplierRevisions.AsNoTracking().Where(r => r.SupplierId == supplierId).ToListAsync(ct);
        });
    }
    public async Task<int> CountSuppliersAsync(string? search, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await LatestSuppliers(search).CountAsync(ct);
        });
    }
    public async Task<List<SupplierRevision>> ListLatestSupplierRevisionsAsync(string? search, int skip, int size, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await LatestSuppliers(search).OrderBy(r => r.Name).ThenBy(r => r.SupplierId).Skip(skip).Take(size).ToListAsync(ct);
        });
    }
    public async Task<List<SupplierRevision>> GetSupplierRevisionsByIdAsync(Guid[] revisionIds, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.SupplierRevisions.AsNoTracking().Where(r => revisionIds.Contains(r.Id)).ToListAsync(ct);
        });
    }
    public async Task<Ingredient?> FindIngredientAsync(Guid ingredientId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.Ingredients.AsNoTracking().SingleOrDefaultAsync(i => i.Id == ingredientId, ct);
        });
    }
    public async Task<IngredientSourceEntry?> FindSourceEntryAsync(Guid entryId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.IngredientSourceEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId, ct);
        });
    }
    public async Task<IngredientSourceEntryVersion?> GetLatestEntryVersionAsync(Guid entryId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.IngredientSourceEntryVersions.AsNoTracking().Where(v => v.SourceEntryId == entryId)
                .OrderByDescending(v => v.Version).FirstOrDefaultAsync(ct);
        });
    }
    public async Task<List<IngredientSourceEntryVersion>> ListEntryVersionsAsync(Guid entryId, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await db.IngredientSourceEntryVersions.AsNoTracking().Where(v => v.SourceEntryId == entryId).ToListAsync(ct);
        });
    }
    public async Task<int> CountEntriesAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            return await LatestEntries(ingredientId, from, to).CountAsync(ct);
        });
    }
    public async Task<List<SourceEntryRow>> ListLatestEntriesAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, int skip, int size, CancellationToken ct)
    {
        return await PersistenceErrors.ExecuteAsync(async () =>
        {
            var rows = await LatestEntries(ingredientId, from, to)
                .OrderByDescending(x => x.Version.ReferenceDate).ThenBy(x => x.Entry.Id)
                .Skip(skip).Take(size).ToListAsync(ct);
            return rows.Select(x => new SourceEntryRow(x.Entry, x.Version, x.IngredientName, x.Supplier)).ToList();
        });
    }

    private IQueryable<SupplierRevision> LatestSuppliers(string? search)
    {
        var query = db.SupplierRevisions.AsNoTracking()
            .Where(r => r.Version == db.SupplierRevisions.Where(x => x.SupplierId == r.SupplierId).Max(x => x.Version));
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().ToLower();
        return query.Where(r => r.Name.ToLower().Contains(term));
    }

    private IQueryable<EntryRow> LatestEntries(Guid? ingredientId, DateOnly? from, DateOnly? to)
    {
        var query =
            from v in db.IngredientSourceEntryVersions.AsNoTracking()
            where v.Version == db.IngredientSourceEntryVersions.Where(x => x.SourceEntryId == v.SourceEntryId).Max(x => x.Version)
            join e in db.IngredientSourceEntries.AsNoTracking() on v.SourceEntryId equals e.Id
            join i in db.Ingredients.AsNoTracking() on e.IngredientId equals i.Id
            join s in db.SupplierRevisions.AsNoTracking() on v.SupplierRevisionId equals s.Id
            select new EntryRow { Entry = e, Version = v, IngredientName = i.Name, Supplier = s };
        if (ingredientId is { } id) query = query.Where(x => x.Entry.IngredientId == id);
        if (from is { } start) query = query.Where(x => x.Version.ReferenceDate >= start);
        if (to is { } end) query = query.Where(x => x.Version.ReferenceDate <= end);
        return query;
    }

    private sealed class EntryRow
    {
        public IngredientSourceEntry Entry { get; set; } = null!;
        public IngredientSourceEntryVersion Version { get; set; } = null!;
        public string IngredientName { get; set; } = "";
        public SupplierRevision Supplier { get; set; } = null!;
    }

    public void AddSupplier(Supplier value) => db.Suppliers.Add(value);
    public void AddSupplierRevision(SupplierRevision value) => db.SupplierRevisions.Add(value);
    public void AddSourceEntry(IngredientSourceEntry value) => db.IngredientSourceEntries.Add(value);
    public void AddSourceEntryVersion(IngredientSourceEntryVersion value) => db.IngredientSourceEntryVersions.Add(value);
}

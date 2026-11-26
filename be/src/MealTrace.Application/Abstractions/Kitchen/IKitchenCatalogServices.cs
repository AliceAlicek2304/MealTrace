using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Abstractions.Kitchen;

public interface ISupplierService
{
    Task<SupplierCreatedResponse> CreateAsync(SupplierRequest request, CancellationToken ct);
    Task<SupplierCreatedResponse> AddVersionAsync(Guid supplierId, SupplierCorrectionRequest request, CancellationToken ct);
    Task<SupplierPage> ListAsync(string? search, int? page, int? pageSize, CancellationToken ct);
    Task<SupplierHistory> HistoryAsync(Guid supplierId, CancellationToken ct);
}

public interface IIngredientSourceService
{
    Task<SourceEntryCreatedResponse> CreateAsync(SourceEntryRequest request, CancellationToken ct);
    Task<SourceEntryCreatedResponse> AddVersionAsync(Guid entryId, SourceEntryCorrectionRequest request, CancellationToken ct);
    Task<SourceEntryPage> ListAsync(Guid? ingredientId, DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken ct);
    Task<SourceEntryHistory> HistoryAsync(Guid entryId, CancellationToken ct);
}

public interface IMenuService
{
    Task<MenuCreatedResponse> CreateAsync(CreateMenuRequest request, CancellationToken ct);
    Task<MenuCreatedResponse> AddVersionAsync(Guid menuId, CreateMenuVersionRequest request, CancellationToken ct);
    Task<MenuTransitionResponse> SubmitAsync(Guid menuId, MenuTransitionRequest request, CancellationToken ct);
    Task<MenuTransitionResponse> ReviewAsync(Guid menuId, MenuReviewRequest request, CancellationToken ct);
    Task<MenuTransitionResponse> PublishAsync(Guid menuId, MenuPublishRequest request, CancellationToken ct);
    Task<MenuPage> ListAsync(DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken ct);
    Task<MenuView> GetAsync(Guid menuId, int? version, CancellationToken ct);
    Task<MenuHistory> HistoryAsync(Guid menuId, CancellationToken ct);

    /// <summary>mode "pinned" (default) returns the frozen snapshot; "recompute" returns a new view and stores nothing.</summary>
    Task<MenuNutritionResponse> GetNutritionAsync(
        Guid menuId, int? version, string? mode, string? ageGroup, DateTimeOffset? validAt, DateTimeOffset? knownAt,
        CancellationToken ct);

    /// <summary>Published menus for a linked child: frozen numbers only. The parent is the current actor.</summary>
    Task<ParentMenuResponse> ListForParentAsync(Guid studentId, DateOnly from, DateOnly to, CancellationToken ct);
}

public interface IIngredientEstimateService
{
    Task<IngredientEstimateResponse> CreateAsync(IngredientEstimateRequest request, CancellationToken ct);
    Task<IngredientEstimateResponse> GetAsync(Guid id, CancellationToken ct);
    Task<string> ExportCsvAsync(Guid id, CancellationToken ct);
}

public interface INutritionReferenceService
{
    Task<NutritionReferenceCreatedResponse> CreateAsync(CreateNutritionReferenceRequest request, CancellationToken ct);
    Task<NutritionReferenceList> ListAsync(string? ageGroup, DateTimeOffset? validAt, DateTimeOffset? knownAt, CancellationToken ct);
}

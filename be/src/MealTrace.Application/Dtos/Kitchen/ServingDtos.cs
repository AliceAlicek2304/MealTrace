namespace MealTrace.Application.Dtos.Kitchen;

public record CreateServingRequest(
    Guid? ClientRequestId,
    Guid MealDayId,
    Guid ClassId,
    Guid MenuVersionId,
    Guid SettlementId,
    string SnapshotToken,
    List<ServedDishInput> Dishes);

public record ServedDishInput(
    Guid PlannedRecipeVersionId,
    Guid ActualRecipeVersionId,
    int ServedPortions,
    string? SubstitutionReason,
    List<IngredientUseInput> Ingredients);

public record IngredientUseInput(
    Guid IngredientId,
    Guid? NutrientVersionId,
    decimal Quantity,
    string Unit,
    string WeightBasis,
    List<SourceAllocationInput> Allocations,
    SourceGapInput? SourceGap);

public record SourceAllocationInput(
    Guid SourceEntryVersionId,
    decimal Quantity,
    string Unit);

public record SourceGapInput(
    decimal MissingQuantity,
    string Unit,
    string Reason);

public record ServingResponse(
    Guid Id,
    Guid VersionId,
    int Version,
    string ResponsibleStaffId,
    DateTimeOffset RecordedAt,
    bool HasSourceGap,
    Guid? PriorVersionId = null);

public record CreateServingVersionRequest(
    int ExpectedVersion,
    string Reason,
    List<ServedDishInput> Dishes);

public record ServingDetailResponse(
    Guid Id,
    Guid MealDayId,
    Guid ClassId,
    Guid MenuVersionId,
    Guid SettlementId,
    string SnapshotToken,
    int Version,
    string ResponsibleStaffId,
    DateTimeOffset RecordedAt,
    bool HasSourceGap,
    List<ServedDishDetail> Dishes);

public record ServedDishDetail(
    Guid PlannedRecipeVersionId,
    Guid ActualRecipeVersionId,
    int ServedPortions,
    string? SubstitutionReason,
    List<IngredientUseDetail> Ingredients);

public record IngredientUseDetail(
    Guid IngredientId,
    Guid? NutrientVersionId,
    decimal Quantity,
    string Unit,
    string WeightBasis,
    List<SourceAllocationDetail> Allocations,
    SourceGapInput? SourceGap);

public record SourceAllocationDetail(
    Guid SourceEntryVersionId,
    decimal Quantity,
    string Unit);

public record ServingHistoryItem(
    Guid Id,
    int Version,
    DateTimeOffset RecordedAt,
    string ResponsibleStaffId,
    string? Reason,
    bool HasSourceGap,
    Guid? PriorVersionId = null);

using System.Collections.Concurrent;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Dtos.Kitchen;

namespace MealTrace.Application.Features.Kitchen;

public sealed class ServingService(ICurrentActor actor, TimeProvider clock) : IServingService
{
    private static readonly ConcurrentDictionary<Guid, ServingDetailResponse> _servings = new();
    private static readonly ConcurrentDictionary<Guid, List<ServingHistoryItem>> _history = new();
    private static readonly ConcurrentDictionary<Guid, ServingResponse> _idempotentResponses = new();
    private static readonly object _sync = new();

    public Task<ServingResponse> RecordServingAsync(CreateServingRequest request, CancellationToken ct = default)
    {
        if (request.Dishes is null || request.Dishes.Count == 0 || string.IsNullOrWhiteSpace(request.SnapshotToken))
        {
            throw new KitchenException(400, "INVALID_SERVING", "SnapshotToken và danh sách món ăn không được để trống.");
        }

        ValidateDishes(request.Dishes);

        if (request.ClientRequestId.HasValue && _idempotentResponses.TryGetValue(request.ClientRequestId.Value, out var existing))
        {
            return Task.FromResult(existing);
        }

        lock (_sync)
        {
            if (request.ClientRequestId.HasValue && _idempotentResponses.TryGetValue(request.ClientRequestId.Value, out var raceWinner))
            {
                return Task.FromResult(raceWinner);
            }

            var servingId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var version = 1;
            var recordedAt = clock.GetUtcNow();
            var staffId = actor.UserId?.ToString() ?? "system";
            var hasSourceGap = request.Dishes.Any(d => d.Ingredients.Any(i => i.SourceGap is not null && i.SourceGap.MissingQuantity > 0));

            var dishDetails = MapDishDetails(request.Dishes);
            var detail = new ServingDetailResponse(
                servingId,
                request.MealDayId,
                request.ClassId,
                request.MenuVersionId,
                request.SettlementId,
                request.SnapshotToken,
                version,
                staffId,
                recordedAt,
                hasSourceGap,
                dishDetails);

            var response = new ServingResponse(servingId, versionId, version, staffId, recordedAt, hasSourceGap);
            var historyItem = new ServingHistoryItem(servingId, version, recordedAt, staffId, null, hasSourceGap);

            _servings[servingId] = detail;
            _history[servingId] = [historyItem];

            if (request.ClientRequestId.HasValue)
            {
                _idempotentResponses[request.ClientRequestId.Value] = response;
            }

            return Task.FromResult(response);
        }
    }

    public Task<ServingDetailResponse?> GetServingAsync(Guid id, CancellationToken ct = default)
    {
        _servings.TryGetValue(id, out var detail);
        return Task.FromResult(detail);
    }

    public Task<ServingResponse> AddServingVersionAsync(Guid id, CreateServingVersionRequest request, CancellationToken ct = default)
    {
        if (request.Dishes is null || request.Dishes.Count == 0)
        {
            throw new KitchenException(400, "INVALID_SERVING", "Danh sách món ăn không được để trống.");
        }

        ValidateDishes(request.Dishes);

        lock (_sync)
        {
            if (!_servings.TryGetValue(id, out var current))
            {
                throw KitchenException.NotFound("SERVING_NOT_FOUND", "Không tìm thấy bản ghi suất ăn.");
            }

            if (request.ExpectedVersion != current.Version)
            {
                throw new KitchenException(409, "VERSION_CONFLICT", "Dữ liệu đã có phiên bản mới hơn.");
            }

            var newVersion = current.Version + 1;
            var newVersionId = Guid.NewGuid();
            var recordedAt = clock.GetUtcNow();
            var staffId = actor.UserId?.ToString() ?? "system";
            var hasSourceGap = request.Dishes.Any(d => d.Ingredients.Any(i => i.SourceGap is not null && i.SourceGap.MissingQuantity > 0));

            var dishDetails = MapDishDetails(request.Dishes);
            var updatedDetail = new ServingDetailResponse(
                id,
                current.MealDayId,
                current.ClassId,
                current.MenuVersionId,
                current.SettlementId,
                current.SnapshotToken,
                newVersion,
                staffId,
                recordedAt,
                hasSourceGap,
                dishDetails);

            var historyItem = new ServingHistoryItem(id, newVersion, recordedAt, staffId, request.Reason, hasSourceGap);

            _servings[id] = updatedDetail;
            if (_history.TryGetValue(id, out var historyList))
            {
                historyList.Add(historyItem);
            }
            else
            {
                _history[id] = [historyItem];
            }

            var response = new ServingResponse(id, newVersionId, newVersion, staffId, recordedAt, hasSourceGap);
            return Task.FromResult(response);
        }
    }

    public Task<List<ServingHistoryItem>> GetServingHistoryAsync(Guid id, CancellationToken ct = default)
    {
        if (_history.TryGetValue(id, out var items))
        {
            return Task.FromResult(items.ToList());
        }

        return Task.FromResult(new List<ServingHistoryItem>());
    }

    private static void ValidateDishes(List<ServedDishInput> dishes)
    {
        foreach (var dish in dishes)
        {
            if (dish.ActualRecipeVersionId != dish.PlannedRecipeVersionId && string.IsNullOrWhiteSpace(dish.SubstitutionReason))
            {
                throw new KitchenException(400, "SUBSTITUTION_REASON_REQUIRED", "Cần lý do đổi món khi món phục vụ khác món dự kiến.");
            }

            foreach (var ingredient in dish.Ingredients)
            {
                var allocSum = ingredient.Allocations?.Sum(a => a.Quantity) ?? 0m;
                var gapQty = ingredient.SourceGap?.MissingQuantity ?? 0m;
                if (Math.Abs(allocSum + gapQty - ingredient.Quantity) > 0.001m)
                {
                    throw new KitchenException(422, "ALLOCATION_MISMATCH", "Tổng lượng phân bổ nguồn không khớp định lượng thực dùng.");
                }
            }
        }
    }

    private static List<ServedDishDetail> MapDishDetails(List<ServedDishInput> dishes)
    {
        return dishes.Select(d => new ServedDishDetail(
            d.PlannedRecipeVersionId,
            d.ActualRecipeVersionId,
            d.ServedPortions,
            d.SubstitutionReason,
            d.Ingredients.Select(i => new IngredientUseDetail(
                i.IngredientId,
                i.NutrientVersionId,
                i.Quantity,
                i.Unit,
                i.WeightBasis,
                i.Allocations?.Select(a => new SourceAllocationDetail(a.SourceEntryVersionId, a.Quantity, a.Unit)).ToList() ?? [],
                i.SourceGap)).ToList())).ToList();
    }
}

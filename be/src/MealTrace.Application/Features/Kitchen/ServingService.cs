using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Models.Persistence;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Security;

namespace MealTrace.Application.Features.Kitchen;

public sealed class ServingService(
    IServingRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider clock) : IServingService
{
    private readonly IServingRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentActor _actor = actor;
    private readonly TimeProvider _clock = clock;

    public ServingService(ICurrentActor actor, TimeProvider clock)
        : this(new InMemoryServingRepository(), new DummyUnitOfWork(), actor, clock)
    {
    }

    public async Task<ServingResponse> RecordServingAsync(CreateServingRequest request, CancellationToken ct = default)
        => (await RecordServingWithStatusAsync(request, ct)).Response;

    public async Task<ServingWriteResult> RecordServingWithStatusAsync(CreateServingRequest request, CancellationToken ct = default)
    {
        EnsureKitchenWriter();

        if (request.Dishes is null || request.Dishes.Count == 0 || string.IsNullOrWhiteSpace(request.SnapshotToken))
        {
            throw new KitchenException(400, "INVALID_SERVING", "SnapshotToken và danh sách món ăn không được để trống.");
        }

        if (request.MenuVersionId == Guid.Empty)
        {
            throw new KitchenException(400, "INVALID_MENU_VERSION", "MenuVersionId không hợp lệ.");
        }

        ValidateDishes(request.Dishes);

        var payloadHash = ComputePayloadHash(request);

        if (request.ClientRequestId.HasValue)
        {
            var existing = await _repository.FindVersionByClientRequestIdAsync(request.ClientRequestId.Value, ct);
            if (existing is not null)
            {
                if (string.Equals(existing.RequestPayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new ServingWriteResult(ToServingResponse(existing), IsReplay: true);
                }

                throw new KitchenException(409, "IDEMPOTENT_PAYLOAD_MISMATCH", "Cùng ClientRequestId nhưng nội dung yêu cầu khác nhau.");
            }
        }

        if (!await _repository.MealDayIsActiveAndMatchesClassAsync(request.MealDayId, request.ClassId, ct))
        {
            throw new KitchenException(400, "INVALID_MEAL_DAY_OR_CLASS", "Phiên ăn hoặc lớp học không hợp lệ, đã hủy hoặc không cùng niên khóa.");
        }

        var settlementStatus = await _repository.ValidateSettlementAsync(request.SettlementId, request.MealDayId, request.ClassId, request.SnapshotToken, ct);
        if (settlementStatus == ServingSettlementValidationStatus.Stale)
        {
            throw new KitchenException(409, "STALE_SETTLEMENT", "Bản chốt suất ăn hoặc SnapshotToken đã cũ. Vui lòng tải lại bản chốt mới nhất.");
        }

        if (settlementStatus == ServingSettlementValidationStatus.Invalid)
        {
            throw new KitchenException(400, "INVALID_SETTLEMENT_SNAPSHOT", "Bản chốt suất ăn hoặc SnapshotToken không khớp với phiên ăn và lớp học.");
        }

        await ValidateMasterDataAsync(request.Dishes, ct);

        if (await _repository.FindServingByClassAndMealDayAsync(request.MealDayId, request.ClassId, ct) is not null)
        {
            throw new KitchenException(409, "SERVING_CONFLICT", "Lớp học trong phiên ăn này đã được ghi nhận suất ăn.");
        }

        var servingId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var recordedAt = _clock.GetUtcNow();
        var staffId = _actor.UserId?.ToString() ?? "system";
        var hasSourceGap = request.Dishes.Any(d => d.Ingredients.Any(i => i.SourceGap is not null && i.SourceGap.MissingQuantity > 0));

        var serving = new Serving
        {
            Id = servingId,
            MealDayId = request.MealDayId,
            ClassId = request.ClassId,
            CreatedAt = recordedAt,
            Versions = []
        };

        var servingVersion = new ServingVersion
        {
            Id = versionId,
            ServingId = servingId,
            Serving = serving,
            Version = 1,
            PriorVersionId = null,
            MenuVersionId = request.MenuVersionId,
            SettlementId = request.SettlementId,
            SnapshotToken = request.SnapshotToken,
            RecordedAt = recordedAt,
            ResponsibleStaffId = staffId,
            CorrectionReason = null,
            ClientRequestId = request.ClientRequestId,
            RequestPayloadHash = payloadHash,
            Dishes = MapDishEntities(request.Dishes, versionId)
        };

        serving.Versions.Add(servingVersion);

        _repository.AddServing(serving);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (PersistenceConflictException)
        {
            if (request.ClientRequestId.HasValue)
            {
                var existing = await _repository.FindVersionByClientRequestIdAsync(request.ClientRequestId.Value, ct);
                if (existing is not null && string.Equals(existing.RequestPayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new ServingWriteResult(ToServingResponse(existing), IsReplay: true);
                }
            }

            throw new KitchenException(409, "SERVING_CONFLICT", "Bản ghi suất ăn bị xung đột với dữ liệu đã tồn tại.");
        }

        return new ServingWriteResult(new ServingResponse(servingId, versionId, 1, staffId, recordedAt, hasSourceGap, null), IsReplay: false);
    }

    public async Task<ServingDetailResponse?> GetServingAsync(Guid id, CancellationToken ct = default)
    {
        var serving = await _repository.FindServingWithVersionAsync(id, ct);
        if (serving is null)
        {
            return null;
        }

        var latestVersion = serving.Versions.OrderByDescending(v => v.Version).FirstOrDefault();
        if (latestVersion is null)
        {
            return null;
        }

        var hasSourceGap = latestVersion.Dishes.Any(d => d.IngredientUses.Any(u => u.MissingQuantity.HasValue && u.MissingQuantity.Value > 0));

        var dishDetails = latestVersion.Dishes.Select(d => new ServedDishDetail(
            d.PlannedRecipeVersionId,
            d.ActualRecipeVersionId,
            d.ServedPortions,
            d.SubstitutionReason,
            d.IngredientUses.Select(u => new IngredientUseDetail(
                u.IngredientId,
                u.NutrientVersionId,
                u.Quantity,
                u.Unit,
                u.WeightBasis,
                u.Allocations.Select(a => new SourceAllocationDetail(
                    a.SourceEntryVersionId,
                    a.Quantity,
                    a.Unit)).ToList(),
                u.MissingQuantity.HasValue && u.MissingQuantity.Value > 0
                    ? new SourceGapInput(u.MissingQuantity.Value, u.Unit, u.MissingReason ?? string.Empty)
                    : null
            )).ToList()
        )).ToList();

        return new ServingDetailResponse(
            serving.Id,
            serving.MealDayId,
            serving.ClassId,
            latestVersion.MenuVersionId,
            latestVersion.SettlementId,
            latestVersion.SnapshotToken,
            latestVersion.Version,
            latestVersion.ResponsibleStaffId,
            latestVersion.RecordedAt,
            hasSourceGap,
            dishDetails);
    }

    public async Task<ServingResponse> AddServingVersionAsync(Guid id, CreateServingVersionRequest request, CancellationToken ct = default)
    {
        EnsureKitchenWriter();

        if (request.Dishes is null || request.Dishes.Count == 0)
        {
            throw new KitchenException(400, "INVALID_SERVING", "Danh sách món ăn không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
        {
            throw new KitchenException(400, "CORRECTION_REASON_REQUIRED", "Cần lý do hợp lệ (tối đa 500 ký tự) khi điều chỉnh suất ăn.");
        }

        ValidateDishes(request.Dishes);

        var serving = await _repository.FindServingWithVersionAsync(id, ct);
        if (serving is null)
        {
            throw KitchenException.NotFound("SERVING_NOT_FOUND", "Không tìm thấy bản ghi suất ăn.");
        }

        var latestVersion = serving.Versions.OrderByDescending(v => v.Version).FirstOrDefault();
        if (latestVersion is null)
        {
            throw KitchenException.NotFound("SERVING_NOT_FOUND", "Không tìm thấy phiên bản suất ăn.");
        }

        if (request.ExpectedVersion != latestVersion.Version)
        {
            throw new KitchenException(409, "VERSION_CONFLICT", "Dữ liệu đã có phiên bản mới hơn.");
        }

        await ValidateMasterDataAsync(request.Dishes, ct);

        var newVersionNumber = latestVersion.Version + 1;
        var newVersionId = Guid.NewGuid();
        var recordedAt = _clock.GetUtcNow();
        var staffId = _actor.UserId?.ToString() ?? "system";
        var hasSourceGap = request.Dishes.Any(d => d.Ingredients.Any(i => i.SourceGap is not null && i.SourceGap.MissingQuantity > 0));

        // Do not attach Serving navigation so v1 and root remain untracked and immutable.
        var newVersion = new ServingVersion
        {
            Id = newVersionId,
            ServingId = serving.Id,
            Version = newVersionNumber,
            PriorVersionId = latestVersion.Id,
            MenuVersionId = latestVersion.MenuVersionId,
            SettlementId = latestVersion.SettlementId,
            SnapshotToken = latestVersion.SnapshotToken,
            RecordedAt = recordedAt,
            ResponsibleStaffId = staffId,
            CorrectionReason = request.Reason.Trim(),
            ClientRequestId = null,
            RequestPayloadHash = null,
            Dishes = MapDishEntities(request.Dishes, newVersionId)
        };

        _repository.AddServingVersion(newVersion);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (PersistenceConflictException)
        {
            throw new KitchenException(409, "VERSION_CONFLICT", "Dữ liệu đã có phiên bản mới hơn.");
        }

        return new ServingResponse(serving.Id, newVersionId, newVersionNumber, staffId, recordedAt, hasSourceGap, latestVersion.Id);
    }

    public async Task<List<ServingHistoryItem>> GetServingHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var serving = await _repository.FindServingWithVersionAsync(id, ct);
        if (serving is null)
        {
            return [];
        }

        return serving.Versions
            .OrderBy(v => v.Version)
            .Select(v => new ServingHistoryItem(
                serving.Id,
                v.Version,
                v.RecordedAt,
                v.ResponsibleStaffId,
                v.CorrectionReason,
                v.Dishes.Any(d => d.IngredientUses.Any(u => u.MissingQuantity.HasValue && u.MissingQuantity.Value > 0)),
                v.PriorVersionId
            ))
            .ToList();
    }

    private async Task ValidateMasterDataAsync(List<ServedDishInput> dishes, CancellationToken ct)
    {
        var recipeVersionIds = dishes
            .SelectMany(d => new[] { d.PlannedRecipeVersionId, d.ActualRecipeVersionId })
            .Distinct()
            .ToArray();
        if (!await _repository.RecipesExistAsync(recipeVersionIds, ct))
        {
            throw new KitchenException(400, "RECIPE_NOT_FOUND", "Công thức món ăn không tồn tại.");
        }

        var ingredients = dishes.SelectMany(d => d.Ingredients ?? []).ToList();
        if (ingredients.Count == 0)
        {
            return;
        }

        var ingredientIds = ingredients.Select(i => i.IngredientId).Distinct().ToArray();
        var nutrientPairs = ingredients
            .Where(i => i.NutrientVersionId.HasValue)
            .Select(i => (i.IngredientId, i.NutrientVersionId!.Value))
            .Distinct()
            .ToArray();
        if (!await _repository.IngredientsExistAsync(ingredientIds, nutrientPairs, ct))
        {
            throw new KitchenException(400, "INGREDIENT_NOT_FOUND", "Nguyên liệu hoặc phiên bản dinh dưỡng không hợp lệ.");
        }
    }

    private void EnsureKitchenWriter()
    {
        if (!_actor.IsInRole(RoleNames.KitchenStaff) || _actor.IsInRole(RoleNames.Admin))
        {
            throw new KitchenException(403, "FORBIDDEN", "Chỉ nhân viên bếp được phép ghi nhận hoặc điều chỉnh suất ăn phục vụ.");
        }
    }

    private static (decimal Quantity, string BaseUnit) NormalizeToBaseUnit(decimal qty, string? unit)
    {
        var normalized = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "kg" => (qty * 1000m, "g"),
            "l" => (qty * 1000m, "ml"),
            _ => (qty, normalized)
        };
    }

    private static decimal ConvertToTargetUnit(decimal qty, string? fromUnit, string toUnit)
    {
        var (baseQty, fromBase) = NormalizeToBaseUnit(qty, fromUnit);
        var (_, toBase) = NormalizeToBaseUnit(0m, toUnit);
        if (string.IsNullOrEmpty(fromBase) || string.IsNullOrEmpty(toBase) || !string.Equals(fromBase, toBase, StringComparison.Ordinal))
        {
            throw new KitchenException(400, "UNIT_MISMATCH", "Đơn vị phân bổ nguồn không tương thích với đơn vị nguyên liệu.");
        }

        var targetNormalized = toUnit.Trim().ToLowerInvariant();
        return targetNormalized is "kg" or "l" ? baseQty / 1000m : baseQty;
    }

    private static void ValidateDishes(List<ServedDishInput> dishes)
    {
        foreach (var dish in dishes)
        {
            if (dish.ServedPortions <= 0)
            {
                throw new KitchenException(400, "INVALID_SERVING", "Số suất phục vụ phải lớn hơn 0.");
            }

            if (dish.ActualRecipeVersionId != dish.PlannedRecipeVersionId && string.IsNullOrWhiteSpace(dish.SubstitutionReason))
            {
                throw new KitchenException(400, "SUBSTITUTION_REASON_REQUIRED", "Cần lý do đổi món khi món phục vụ khác món dự kiến.");
            }

            if (dish.Ingredients is null)
            {
                continue;
            }

            foreach (var ingredient in dish.Ingredients)
            {
                if (ingredient.Quantity <= 0 || string.IsNullOrWhiteSpace(ingredient.Unit))
                {
                    throw new KitchenException(400, "INVALID_SERVING", "Định lượng và đơn vị nguyên liệu phải hợp lệ.");
                }

                if (ingredient.SourceGap is { MissingQuantity: < 0 })
                {
                    throw new KitchenException(400, "INVALID_ALLOCATION", "Lượng thiếu hụt nguồn không được âm.");
                }

                if (ingredient.SourceGap is { MissingQuantity: > 0 } && string.IsNullOrWhiteSpace(ingredient.SourceGap.Reason))
                {
                    throw new KitchenException(400, "SOURCE_GAP_REASON_REQUIRED", "Cần lý do khi khai báo thiếu hụt nguồn nguyên liệu.");
                }

                var allocSum = 0m;
                var seenSources = new HashSet<Guid>();
                if (ingredient.Allocations is not null)
                {
                    foreach (var allocation in ingredient.Allocations)
                    {
                        if (allocation.SourceEntryVersionId == Guid.Empty || allocation.Quantity <= 0 || !seenSources.Add(allocation.SourceEntryVersionId))
                        {
                            throw new KitchenException(400, "INVALID_ALLOCATION", "Phân bổ nguồn phải có mã hợp lệ, số lượng dương và không trùng nguồn.");
                        }

                        allocSum += ConvertToTargetUnit(allocation.Quantity, allocation.Unit, ingredient.Unit);
                    }
                }

                var gapQty = 0m;
                if (ingredient.SourceGap is { MissingQuantity: > 0 })
                {
                    gapQty = ConvertToTargetUnit(ingredient.SourceGap.MissingQuantity, ingredient.SourceGap.Unit, ingredient.Unit);
                }
                else if (ingredient.SourceGap is not null && !string.IsNullOrWhiteSpace(ingredient.SourceGap.Unit))
                {
                    ConvertToTargetUnit(0m, ingredient.SourceGap.Unit, ingredient.Unit);
                }

                var (actualBaseQty, _) = NormalizeToBaseUnit(allocSum + gapQty, ingredient.Unit);
                var (targetBaseQty, _) = NormalizeToBaseUnit(ingredient.Quantity, ingredient.Unit);
                if (Math.Abs(actualBaseQty - targetBaseQty) > 0.001m)
                {
                    throw new KitchenException(422, "ALLOCATION_MISMATCH", "Tổng lượng phân bổ nguồn không khớp định lượng thực dùng.");
                }
            }
        }
    }

    private static ServingResponse ToServingResponse(ServingVersion version)
    {
        var hasGap = version.Dishes.Any(d => d.IngredientUses.Any(u => u.MissingQuantity.HasValue && u.MissingQuantity.Value > 0));
        return new ServingResponse(version.ServingId, version.Id, version.Version, version.ResponsibleStaffId, version.RecordedAt, hasGap, version.PriorVersionId);
    }

    private static string ComputePayloadHash(CreateServingRequest request)
    {
        var payloadObj = new
        {
            request.MealDayId,
            request.ClassId,
            request.MenuVersionId,
            request.SettlementId,
            request.SnapshotToken,
            request.Dishes
        };
        var json = JsonSerializer.Serialize(payloadObj);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    private static List<ServedDish> MapDishEntities(List<ServedDishInput> dishes, Guid versionId)
    {
        var dishEntities = new List<ServedDish>(dishes.Count);
        foreach (var d in dishes)
        {
            var dishId = Guid.NewGuid();
            var dish = new ServedDish
            {
                Id = dishId,
                ServingVersionId = versionId,
                PlannedRecipeVersionId = d.PlannedRecipeVersionId,
                ActualRecipeVersionId = d.ActualRecipeVersionId,
                ServedPortions = d.ServedPortions,
                SubstitutionReason = d.SubstitutionReason,
                IngredientUses = []
            };

            foreach (var i in d.Ingredients)
            {
                var useId = Guid.NewGuid();
                var use = new IngredientUse
                {
                    Id = useId,
                    ServedDishId = dishId,
                    IngredientId = i.IngredientId,
                    NutrientVersionId = i.NutrientVersionId,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    WeightBasis = i.WeightBasis,
                    MissingQuantity = i.SourceGap is { MissingQuantity: > 0 }
                        ? ConvertToTargetUnit(i.SourceGap.MissingQuantity, i.SourceGap.Unit, i.Unit)
                        : i.SourceGap?.MissingQuantity,
                    MissingReason = i.SourceGap?.Reason?.Trim(),
                    Allocations = []
                };

                if (i.Allocations is not null)
                {
                    foreach (var a in i.Allocations)
                    {
                        use.Allocations.Add(new SourceAllocation
                        {
                            Id = Guid.NewGuid(),
                            IngredientUseId = useId,
                            SourceEntryVersionId = a.SourceEntryVersionId,
                            Quantity = a.Quantity,
                            Unit = a.Unit
                        });
                    }
                }

                dish.IngredientUses.Add(use);
            }

            dishEntities.Add(dish);
        }

        return dishEntities;
    }

    private sealed class InMemoryServingRepository : IServingRepository
    {
        private readonly ConcurrentDictionary<Guid, Serving> _servings = new();
        private readonly ConcurrentDictionary<Guid, ServingVersion> _versionsByClientRequestId = new();

        public Task<ServingVersion?> FindVersionByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct = default)
        {
            _versionsByClientRequestId.TryGetValue(clientRequestId, out var version);
            return Task.FromResult(version);
        }

        public Task<Serving?> FindServingWithVersionAsync(Guid servingId, CancellationToken ct = default)
        {
            _servings.TryGetValue(servingId, out var serving);
            return Task.FromResult(serving);
        }

        public Task<Serving?> FindServingByClassAndMealDayAsync(Guid mealDayId, Guid classId, CancellationToken ct = default)
        {
            var serving = _servings.Values.FirstOrDefault(s => s.MealDayId == mealDayId && s.ClassId == classId);
            return Task.FromResult(serving);
        }

        public Task<bool> MealDayIsActiveAndMatchesClassAsync(Guid mealDayId, Guid classId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<ServingSettlementValidationStatus> ValidateSettlementAsync(Guid settlementId, Guid mealDayId, Guid classId, string snapshotToken, CancellationToken ct = default)
            => Task.FromResult(ServingSettlementValidationStatus.Valid);

        public Task<bool> RecipesExistAsync(Guid[] recipeVersionIds, CancellationToken ct = default) => Task.FromResult(true);

        public Task<bool> IngredientsExistAsync(Guid[] ingredientIds, (Guid IngredientId, Guid NutrientVersionId)[] nutrientPairs, CancellationToken ct = default) => Task.FromResult(true);

        public void AddServing(Serving serving)
        {
            _servings[serving.Id] = serving;
            foreach (var v in serving.Versions)
            {
                if (v.ClientRequestId.HasValue)
                {
                    _versionsByClientRequestId[v.ClientRequestId.Value] = v;
                }
            }
        }

        public void AddServingVersion(ServingVersion version)
        {
            if (_servings.TryGetValue(version.ServingId, out var serving))
            {
                serving.Versions.Add(version);
            }

            if (version.ClientRequestId.HasValue)
            {
                _versionsByClientRequestId[version.ClientRequestId.Value] = version;
            }
        }
    }

    private sealed class DummyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IDataTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Abstractions.Kitchen;
using MealTrace.Application.Abstractions.Repositories;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Domain.Entities;

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
    {
        if (request.Dishes is null || request.Dishes.Count == 0 || string.IsNullOrWhiteSpace(request.SnapshotToken))
        {
            throw new KitchenException(400, "INVALID_SERVING", "SnapshotToken và danh sách món ăn không được để trống.");
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
                    var hasGap = existing.Dishes.Any(d => d.IngredientUses.Any(u => u.MissingQuantity.HasValue && u.MissingQuantity.Value > 0));
                    return new ServingResponse(existing.ServingId, existing.Id, existing.Version, existing.ResponsibleStaffId, existing.RecordedAt, hasGap);
                }

                throw new KitchenException(409, "IDEMPOTENT_PAYLOAD_MISMATCH", "Cùng ClientRequestId nhưng nội dung yêu cầu khác nhau.");
            }
        }

        if (!await _repository.MealDayExistsAsync(request.MealDayId, ct))
        {
            throw new KitchenException(400, "MEAL_DAY_NOT_FOUND", "Không tìm thấy phiên ăn.");
        }

        if (!await _repository.ClassExistsAsync(request.ClassId, ct))
        {
            throw new KitchenException(400, "CLASS_NOT_FOUND", "Không tìm thấy lớp học.");
        }

        if (!await _repository.SettlementSnapshotMatchesAsync(request.SettlementId, request.SnapshotToken, ct))
        {
            throw new KitchenException(400, "INVALID_SETTLEMENT_SNAPSHOT", "SnapshotToken không khớp với bản chốt suất ăn.");
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
        await _unitOfWork.SaveChangesAsync(ct);

        return new ServingResponse(servingId, versionId, 1, staffId, recordedAt, hasSourceGap);
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
        if (request.Dishes is null || request.Dishes.Count == 0)
        {
            throw new KitchenException(400, "INVALID_SERVING", "Danh sách món ăn không được để trống.");
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

        var newVersionNumber = latestVersion.Version + 1;
        var newVersionId = Guid.NewGuid();
        var recordedAt = _clock.GetUtcNow();
        var staffId = _actor.UserId?.ToString() ?? "system";
        var hasSourceGap = request.Dishes.Any(d => d.Ingredients.Any(i => i.SourceGap is not null && i.SourceGap.MissingQuantity > 0));

        var newVersion = new ServingVersion
        {
            Id = newVersionId,
            ServingId = serving.Id,
            Version = newVersionNumber,
            MenuVersionId = latestVersion.MenuVersionId,
            SettlementId = latestVersion.SettlementId,
            SnapshotToken = latestVersion.SnapshotToken,
            RecordedAt = recordedAt,
            ResponsibleStaffId = staffId,
            CorrectionReason = request.Reason,
            ClientRequestId = null,
            RequestPayloadHash = null,
            Dishes = MapDishEntities(request.Dishes, newVersionId)
        };

        _repository.AddServingVersion(newVersion);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ServingResponse(serving.Id, newVersionId, newVersionNumber, staffId, recordedAt, hasSourceGap);
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
                v.Dishes.Any(d => d.IngredientUses.Any(u => u.MissingQuantity.HasValue && u.MissingQuantity.Value > 0))
            ))
            .ToList();
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
                    MissingQuantity = i.SourceGap?.MissingQuantity,
                    MissingReason = i.SourceGap?.Reason,
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

        public Task<bool> MealDayExistsAsync(Guid mealDayId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<bool> SettlementSnapshotMatchesAsync(Guid settlementId, string snapshotToken, CancellationToken ct = default) => Task.FromResult(true);

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

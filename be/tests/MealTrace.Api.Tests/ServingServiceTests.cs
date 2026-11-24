using MealTrace.Application.Abstractions;
using MealTrace.Application.Dtos.Kitchen;
using MealTrace.Application.Exceptions;
using MealTrace.Application.Features.Kitchen;

namespace MealTrace.Api.Tests;

public sealed class ServingServiceTests
{
    private readonly TestActor _actor = new();
    private readonly TimeProvider _clock = TimeProvider.System;

    [Fact]
    public async Task RecordServing_ValidSplitAllocations_Succeeds()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 30,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: Guid.NewGuid(),
                            Quantity: 1000m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations:
                            [
                                new SourceAllocationInput(source1, 600m, "g"),
                                new SourceAllocationInput(source2, 400m, "g")
                            ],
                            SourceGap: null)
                    ])
            ]);

        var response = await service.RecordServingAsync(request);

        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.NotEqual(Guid.Empty, response.VersionId);
        Assert.Equal(1, response.Version);
        Assert.Equal(_actor.UserId!.Value.ToString(), response.ResponsibleStaffId);
        Assert.False(response.HasSourceGap);
    }

    [Fact]
    public async Task RecordServing_AllocationMismatch_Throws422()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "valid-token",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 30,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: Guid.NewGuid(),
                            Quantity: 1000m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations:
                            [
                                new SourceAllocationInput(source1, 600m, "g"),
                                new SourceAllocationInput(source2, 500m, "g")
                            ],
                            SourceGap: null)
                    ])
            ]);

        var ex = await Assert.ThrowsAsync<KitchenException>(() => service.RecordServingAsync(request));

        Assert.Equal(422, ex.Status);
        Assert.Equal("ALLOCATION_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task RecordServing_SubstitutionWithoutReason_Throws400()
    {
        var service = new ServingService(_actor, _clock);
        var plannedRecipeId = Guid.NewGuid();
        var actualRecipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "valid-token",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: plannedRecipeId,
                    ActualRecipeVersionId: actualRecipeId,
                    ServedPortions: 30,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: Guid.NewGuid(),
                            Quantity: 500m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 500m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var ex = await Assert.ThrowsAsync<KitchenException>(() => service.RecordServingAsync(request));

        Assert.Equal(400, ex.Status);
        Assert.Equal("SUBSTITUTION_REASON_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task RecordServing_DuplicateClientRequestId_ReturnsSameInstance()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: clientRequestId,
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "valid-token",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 25,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 200m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 200m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var first = await service.RecordServingAsync(request);
        var second = await service.RecordServingAsync(request);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.VersionId, second.VersionId);
        Assert.Equal(first.Version, second.Version);
    }

    [Fact]
    public async Task GetServing_ReturnsCorrectGraph()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();
        var mealDayId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var menuVersionId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: mealDayId,
            ClassId: classId,
            MenuVersionId: menuVersionId,
            SettlementId: settlementId,
            SnapshotToken: "token-12345",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 35,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 1000m,
                            Unit: "g",
                            WeightBasis: "edible_portion",
                            Allocations:
                            [
                                new SourceAllocationInput(source1, 600m, "g"),
                                new SourceAllocationInput(source2, 400m, "g")
                            ],
                            SourceGap: null)
                    ])
            ]);

        var created = await service.RecordServingAsync(request);
        var detail = await service.GetServingAsync(created.Id);

        Assert.NotNull(detail);
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(mealDayId, detail.MealDayId);
        Assert.Equal(classId, detail.ClassId);
        Assert.Equal(menuVersionId, detail.MenuVersionId);
        Assert.Equal(settlementId, detail.SettlementId);
        Assert.Equal("token-12345", detail.SnapshotToken);
        Assert.Equal(1, detail.Version);
        Assert.False(detail.HasSourceGap);

        var dish = Assert.Single(detail.Dishes);
        Assert.Equal(recipeId, dish.PlannedRecipeVersionId);
        Assert.Equal(recipeId, dish.ActualRecipeVersionId);
        Assert.Equal(35, dish.ServedPortions);

        var ing = Assert.Single(dish.Ingredients);
        Assert.Equal(ingredientId, ing.IngredientId);
        Assert.Equal(1000m, ing.Quantity);
        Assert.Equal(2, ing.Allocations.Count);
        Assert.Equal(600m, ing.Allocations[0].Quantity);
        Assert.Equal(400m, ing.Allocations[1].Quantity);
    }

    [Fact]
    public async Task AddServingVersion_StaleVersion_Throws409()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "token-abc",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 20,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 100m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 100m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var created = await service.RecordServingAsync(request);

        var versionRequest = new CreateServingVersionRequest(
            ExpectedVersion: 999,
            Reason: "Cập nhật suất ăn sau kiểm tra",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 22,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 110m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 110m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var ex = await Assert.ThrowsAsync<KitchenException>(() => service.AddServingVersionAsync(created.Id, versionRequest));

        Assert.Equal(409, ex.Status);
        Assert.Equal("VERSION_CONFLICT", ex.Code);
    }

    [Fact]
    public async Task AddServingVersion_Valid_IncrementsVersionAndRecordsHistory()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "token-v1",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 20,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 100m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 100m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var created = await service.RecordServingAsync(request);

        var versionRequest = new CreateServingVersionRequest(
            ExpectedVersion: 1,
            Reason: "Tăng 5 suất ăn phát sinh",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 25,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 125m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 125m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var v2 = await service.AddServingVersionAsync(created.Id, versionRequest);

        Assert.Equal(created.Id, v2.Id);
        Assert.Equal(2, v2.Version);

        var history = await service.GetServingHistoryAsync(created.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal(1, history[0].Version);
        Assert.Null(history[0].Reason);
        Assert.Equal(2, history[1].Version);
        Assert.Equal("Tăng 5 suất ăn phát sinh", history[1].Reason);
    }

    [Fact]
    public async Task RecordServing_WithSourceGap_MarksHasSourceGapTrue()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "token-gap",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 30,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 1000m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 800m, "g")],
                            SourceGap: new SourceGapInput(200m, "g", "Nhà cung cấp giao thiếu 200g"))
                    ])
            ]);

        var response = await service.RecordServingAsync(request);

        Assert.True(response.HasSourceGap);
    }

    [Fact]
    public async Task RecordServing_DuplicateClientRequestId_DifferentPayload_Throws409IdempotentMismatch()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        var request1 = new CreateServingRequest(
            ClientRequestId: clientRequestId,
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "token-first",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 20,
                    SubstitutionReason: null,
                    Ingredients:
                    [
                        new IngredientUseInput(
                            IngredientId: ingredientId,
                            NutrientVersionId: null,
                            Quantity: 100m,
                            Unit: "g",
                            WeightBasis: "as_purchased",
                            Allocations: [new SourceAllocationInput(Guid.NewGuid(), 100m, "g")],
                            SourceGap: null)
                    ])
            ]);

        var request2 = request1 with { SnapshotToken = "token-different" };

        await service.RecordServingAsync(request1);
        var ex = await Assert.ThrowsAsync<KitchenException>(() => service.RecordServingAsync(request2));

        Assert.Equal(409, ex.Status);
        Assert.Equal("IDEMPOTENT_PAYLOAD_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task RecordServing_MissingDishesOrEmptyToken_Throws400()
    {
        var service = new ServingService(_actor, _clock);

        var request = new CreateServingRequest(
            ClientRequestId: Guid.NewGuid(),
            MealDayId: Guid.NewGuid(),
            ClassId: Guid.NewGuid(),
            MenuVersionId: Guid.NewGuid(),
            SettlementId: Guid.NewGuid(),
            SnapshotToken: "",
            Dishes: []);

        var ex = await Assert.ThrowsAsync<KitchenException>(() => service.RecordServingAsync(request));

        Assert.Equal(400, ex.Status);
        Assert.Equal("INVALID_SERVING", ex.Code);
    }

    [Fact]
    public async Task AddServingVersion_NonExistentServing_Throws404()
    {
        var service = new ServingService(_actor, _clock);
        var recipeId = Guid.NewGuid();

        var versionRequest = new CreateServingVersionRequest(
            ExpectedVersion: 1,
            Reason: "Cập nhật",
            Dishes:
            [
                new ServedDishInput(
                    PlannedRecipeVersionId: recipeId,
                    ActualRecipeVersionId: recipeId,
                    ServedPortions: 20,
                    SubstitutionReason: null,
                    Ingredients: [])
            ]);

        var ex = await Assert.ThrowsAsync<KitchenException>(() => service.AddServingVersionAsync(Guid.NewGuid(), versionRequest));

        Assert.Equal(404, ex.Status);
        Assert.Equal("SERVING_NOT_FOUND", ex.Code);
    }

    private sealed class TestActor : ICurrentActor
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public bool IsInRole(string role) => true;
    }
}

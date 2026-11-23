using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Tests;

public sealed class ServingPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ServingPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MealTraceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MealTraceDbContext>()
            .UseSqlite(_connection)
            .Options;

        var db = new MealTraceDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void ServingConfiguration_HasExpectedMetadata()
    {
        using var db = CreateDbContext();
        var model = db.Model;

        var servingType = model.FindEntityType(typeof(Serving));
        Assert.NotNull(servingType);
        Assert.Equal(nameof(Serving.Id), servingType.FindPrimaryKey()?.Properties[0].Name);

        var servingVersionType = model.FindEntityType(typeof(ServingVersion));
        Assert.NotNull(servingVersionType);
        Assert.Equal(128, servingVersionType.FindProperty(nameof(ServingVersion.SnapshotToken))?.GetMaxLength());
        Assert.Equal(64, servingVersionType.FindProperty(nameof(ServingVersion.ResponsibleStaffId))?.GetMaxLength());
        Assert.Equal(500, servingVersionType.FindProperty(nameof(ServingVersion.CorrectionReason))?.GetMaxLength());
        Assert.Equal(64, servingVersionType.FindProperty(nameof(ServingVersion.RequestPayloadHash))?.GetMaxLength());

        var servedDishType = model.FindEntityType(typeof(ServedDish));
        Assert.NotNull(servedDishType);
        Assert.Equal(500, servedDishType.FindProperty(nameof(ServedDish.SubstitutionReason))?.GetMaxLength());

        var ingredientUseType = model.FindEntityType(typeof(IngredientUse));
        Assert.NotNull(ingredientUseType);
        Assert.Equal(20, ingredientUseType.FindProperty(nameof(IngredientUse.Unit))?.GetMaxLength());
        Assert.Equal(20, ingredientUseType.FindProperty(nameof(IngredientUse.WeightBasis))?.GetMaxLength());
        Assert.Equal(500, ingredientUseType.FindProperty(nameof(IngredientUse.MissingReason))?.GetMaxLength());
        Assert.Equal(18, ingredientUseType.FindProperty(nameof(IngredientUse.Quantity))?.GetPrecision());
        Assert.Equal(4, ingredientUseType.FindProperty(nameof(IngredientUse.Quantity))?.GetScale());

        var sourceAllocationType = model.FindEntityType(typeof(SourceAllocation));
        Assert.NotNull(sourceAllocationType);
        Assert.Equal(20, sourceAllocationType.FindProperty(nameof(SourceAllocation.Unit))?.GetMaxLength());
        Assert.Equal(18, sourceAllocationType.FindProperty(nameof(SourceAllocation.Quantity))?.GetPrecision());
        Assert.Equal(4, sourceAllocationType.FindProperty(nameof(SourceAllocation.Quantity))?.GetScale());
    }

    [Fact]
    public void ServingHistory_IsAppendOnly_ModifiedThrowsInvalidOperationException()
    {
        using var db = CreateDbContext();
        var mealDay = new MealDay
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "LUNCH"
        };
        var schoolClass = new SchoolClass
        {
            Name = "1A",
            SchoolYear = "2026-2027"
        };
        var serving = new Serving
        {
            MealDay = mealDay,
            Class = schoolClass
        };
        var version = new ServingVersion
        {
            Serving = serving,
            SnapshotToken = "token123",
            ResponsibleStaffId = "staff01"
        };
        db.MealDays.Add(mealDay);
        db.Classes.Add(schoolClass);
        db.Servings.Add(serving);
        db.ServingVersions.Add(version);
        db.SaveChanges();

        // Thử sửa ServingVersion
        version.CorrectionReason = "Modified";
        var ex = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("Serving history is append-only", ex.Message);
    }

    [Fact]
    public void ServingHistory_IsAppendOnly_DeletedThrowsInvalidOperationException()
    {
        using var db = CreateDbContext();
        var mealDay = new MealDay
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MealType = "LUNCH"
        };
        var schoolClass = new SchoolClass
        {
            Name = "1A",
            SchoolYear = "2026-2027"
        };
        var serving = new Serving
        {
            MealDay = mealDay,
            Class = schoolClass
        };
        var version = new ServingVersion
        {
            Serving = serving,
            SnapshotToken = "token123",
            ResponsibleStaffId = "staff01"
        };
        var dish = new ServedDish
        {
            ServingVersion = version
        };
        db.MealDays.Add(mealDay);
        db.Classes.Add(schoolClass);
        db.Servings.Add(serving);
        db.ServingVersions.Add(version);
        db.ServedDishes.Add(dish);
        db.SaveChanges();

        // Thử xóa ServedDish
        db.ServedDishes.Remove(dish);
        var ex = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("Serving history is append-only", ex.Message);
    }
}

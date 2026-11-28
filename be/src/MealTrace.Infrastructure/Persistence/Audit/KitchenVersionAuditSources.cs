using System.Text.Json;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Audit;

internal sealed record VersionChange<T>(T Current, T? Previous) where T : class;

internal sealed class IngredientVersionAuditSource(MealTraceDbContext db) : EfAuditSource<VersionChange<IngredientVersion>>
{
    public override string Name => "ingredient-version";

    protected override IQueryable<AuditProjection<VersionChange<IngredientVersion>>> Events =>
        from version in db.IngredientVersions.AsNoTracking()
        join previous in db.IngredientVersions.AsNoTracking()
            on new { version.IngredientId, Id = version.SupersedesId }
            equals new { previous.IngredientId, Id = (Guid?)previous.Id } into predecessors
        from previous in predecessors.DefaultIfEmpty()
        select new AuditProjection<VersionChange<IngredientVersion>>
        {
            Event = new VersionChange<IngredientVersion>(version, previous),
            Id = version.Id,
            At = version.RecordedAt,
            ActorId = version.RecordedByUserId,
            Action = "kitchen.ingredient.version_recorded",
            EntityType = nameof(Ingredient),
            EntityId = version.IngredientId
        };

    protected override JsonElement? Before(VersionChange<IngredientVersion> row) =>
        row.Previous is { } previous ? Snapshot(previous) : null;

    protected override JsonElement? After(VersionChange<IngredientVersion> row) => Snapshot(row.Current);

    private static JsonElement Snapshot(IngredientVersion version) => Json(new
    {
        version.Id, version.Version, version.SupersedesId, version.EffectiveFrom, version.EffectiveTo,
        version.EnergyKcalPer100G, version.ProteinGPer100G, version.FatGPer100G, version.CarbGPer100G,
        version.EdibleFraction, version.GramsPerMl, version.Source,
        hasSourceReference = !string.IsNullOrWhiteSpace(version.SourceRef),
        actorMissing = version.RecordedByUserId is null || version.RecordedByUserId == Guid.Empty
    });
}

internal sealed class RecipeVersionAuditSource(MealTraceDbContext db) : EfAuditSource<VersionChange<RecipeVersion>>
{
    public override string Name => "recipe-version";

    protected override IQueryable<AuditProjection<VersionChange<RecipeVersion>>> Events =>
        from version in db.RecipeVersions.AsNoTracking()
        join previous in db.RecipeVersions.AsNoTracking()
            on new { version.RecipeId, Version = version.Version - 1 }
            equals new { previous.RecipeId, previous.Version } into predecessors
        from previous in predecessors.DefaultIfEmpty()
        select new AuditProjection<VersionChange<RecipeVersion>>
        {
            Event = new VersionChange<RecipeVersion>(version, previous),
            Id = version.Id,
            At = version.RecordedAt,
            ActorId = version.RecordedByUserId,
            Action = "kitchen.recipe.version_recorded",
            EntityType = nameof(Recipe),
            EntityId = version.RecipeId,
            Reason = version.Reason
        };

    protected override JsonElement? Before(VersionChange<RecipeVersion> row) =>
        row.Previous is { } previous ? Snapshot(previous) : null;

    protected override JsonElement? After(VersionChange<RecipeVersion> row) => Snapshot(row.Current);

    private static JsonElement Snapshot(RecipeVersion version) => Json(new
    {
        version.Id, version.Version, version.ServingBasis, version.RoundingKcalDecimals, version.RoundingMacroDecimals,
        actorMissing = version.RecordedByUserId is null || version.RecordedByUserId == Guid.Empty
    });
}

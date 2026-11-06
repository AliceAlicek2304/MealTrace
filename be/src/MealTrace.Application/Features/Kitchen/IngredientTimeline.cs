using MealTrace.Domain.Entities;

namespace MealTrace.Application.Features.Kitchen;

/// <summary>
/// Official temporal rule for one ingredient's version history (BE1-BR02), kept free of persistence so it can be
/// unit-tested directly.
/// <list type="number">
/// <item>Known: RecordedAt &lt;= knownAt.</item>
/// <item>Live: known and not superseded by another known row (SupersedesId).</item>
/// <item>Applicable: live and EffectiveFrom &lt;= validAt and (EffectiveTo is null or validAt &lt; EffectiveTo).</item>
/// <item>Priority: latest EffectiveFrom, then latest RecordedAt, then highest Version.</item>
/// </list>
/// Write-time guard (IngredientService.AddVersionAsync): a new interval may overlap a live row only if it starts
/// strictly later than that row; otherwise it must supersede it explicitly. So among live rows, priority by
/// EffectiveFrom is unambiguous; the later tie-breakers only protect data written before the guard existed.
/// </summary>
public static class IngredientTimeline
{
    public static IReadOnlyList<IngredientVersion> Live(IEnumerable<IngredientVersion> history, DateTimeOffset knownUtc)
    {
        var known = history.Where(v => v.RecordedAt <= knownUtc).ToList();
        var superseded = known
            .Where(v => v.SupersedesId is not null)
            .Select(v => v.SupersedesId!.Value)
            .ToHashSet();
        return known.Where(v => !superseded.Contains(v.Id)).ToList();
    }

    public static bool Covers(IngredientVersion version, DateTimeOffset validUtc) =>
        version.EffectiveFrom <= validUtc && (version.EffectiveTo is null || validUtc < version.EffectiveTo);

    public static IngredientVersion? PickEffective(
        IEnumerable<IngredientVersion> history, DateTimeOffset validUtc, DateTimeOffset knownUtc) =>
        Live(history, knownUtc)
            .Where(v => Covers(v, validUtc))
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.RecordedAt)
            .ThenByDescending(v => v.Version)
            .FirstOrDefault();
}

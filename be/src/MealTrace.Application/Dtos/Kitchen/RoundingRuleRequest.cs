namespace MealTrace.Application.Dtos.Kitchen;

/// <summary>Decimals (0-3) applied once to the per-serving totals. Omitted parts are inherited (or default for a new recipe).</summary>
public sealed record RoundingRuleRequest(int? KcalDecimals, int? MacroDecimals);

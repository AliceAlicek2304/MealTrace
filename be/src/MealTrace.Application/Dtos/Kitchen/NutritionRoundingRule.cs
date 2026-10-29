namespace MealTrace.Application.Dtos.Kitchen;

public sealed record NutritionRoundingRule(int KcalDecimals, int ProteinDecimals, string Mode)
{
    /// <summary>ProteinDecimals applies to protein, fat and carbohydrate alike; the old name is kept for existing clients.</summary>
    public int MacroDecimals => ProteinDecimals;
}

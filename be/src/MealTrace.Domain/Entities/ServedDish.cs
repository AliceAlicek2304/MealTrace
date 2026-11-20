namespace MealTrace.Domain.Entities;

public sealed class ServedDish
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServingId { get; set; }
    public Serving Serving { get; set; } = null!;
    public Guid PlannedRecipeVersionId { get; set; }
    public Guid ActualRecipeVersionId { get; set; }
    public int ServedPortions { get; set; }
    public string? SubstitutionReason { get; set; }
    public List<IngredientUse> IngredientUses { get; set; } = [];
}

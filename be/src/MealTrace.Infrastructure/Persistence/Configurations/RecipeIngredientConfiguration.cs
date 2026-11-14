using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.Property(x => x.GramsPerPortion).HasPrecision(12, 3);

        // v1.3 (BE1): the quantity as entered. Nullable so lines written before v1.3 stay valid.
        builder.Property(x => x.Quantity).HasPrecision(14, 3);
        builder.Property(x => x.Unit).HasMaxLength(10);
        builder.Property(x => x.WeightBasis).HasMaxLength(10);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RecipeIngredients_Unit", "\"Unit\" IS NULL OR \"Unit\" IN ('g', 'ml')");
            t.HasCheckConstraint("CK_RecipeIngredients_WeightBasis", "\"WeightBasis\" IS NULL OR \"WeightBasis\" IN ('gross', 'edible')");
        });
    }
}

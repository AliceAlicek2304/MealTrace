using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class IngredientVersionConfiguration : IEntityTypeConfiguration<IngredientVersion>
{
    public void Configure(EntityTypeBuilder<IngredientVersion> builder)
    {
        builder.HasIndex(x => new { x.IngredientId, x.Version }).IsUnique();
        builder.Property(x => x.EnergyKcalPer100G).HasPrecision(12, 3);
        builder.Property(x => x.ProteinGPer100G).HasPrecision(12, 3);
        builder.Property(x => x.PricePerKg).HasPrecision(14, 2);
        builder.Property(x => x.EdibleFraction).HasPrecision(5, 4);
    }
}
